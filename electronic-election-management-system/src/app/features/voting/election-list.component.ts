import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { AuthService } from '../../core/services/auth.service';
import { ElectionListFiltersService } from '../../core/services/election-list-filters.service';
import { ElectionDto } from '../../core/models/voting.model';

type FilterCategory = 'status' | 'timing' | 'participation' | 'type' | 'visibility';

@Component({
  selector: 'app-election-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TranslatePipe],
  templateUrl: './election-list.component.html',
  styleUrl: './election-list.component.scss'
})
export class ElectionListComponent implements OnInit {
  private votingService = inject(VotingService);
  readonly authService = inject(AuthService);
  public translateService = inject(TranslateService);
  private filtersService = inject(ElectionListFiltersService);

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);

  // Backed by a root-provided service (not local state) so the selection
  // survives leaving this page (vote/results) and coming back.
  searchQuery = this.filtersService.searchQuery;
  selectedFilters = this.filtersService.selectedFilters;

  // Which accordion sections are open. Local to this component on purpose:
  // collapse state is a view preference, not something that should follow
  // the user across navigation the way the filter selections do.
  expandedGroups = signal<Record<string, boolean>>({
    status: true,
    timing: true,
    participation: true,
    type: true,
    visibility: true
  });

  // Count of currently active (checked) filters, used in the sidebar
  // heading and to decide whether to show "Clear all".
  activeFilterCount = computed(() => {
    const filters = this.selectedFilters();
    return Object.values(filters).filter(Boolean).length;
  });

  // Elections narrowed only by the search box, ignoring checkbox filters.
  // This is the base pool that both the main list and the sidebar counts
  // are computed from.
  private searchFilteredElections = computed(() => {
    const query = this.searchQuery().toLowerCase().trim();
    const list = this.elections();
    if (!query) return list;
    return list.filter((e) => e.title.toLowerCase().includes(query));
  });

  // Single source of truth for "does this election match the current
  // filters". Both filteredElections and filterCounts call this, so the
  // two can never drift apart the way two separately-maintained filter
  // blocks eventually do.
  //
  // ignoreCategory skips that one category's own restriction, so a count
  // for an option WITHIN a category answers "how many results if this
  // option were picked instead of (or alongside) its siblings", while
  // every OTHER category's current selection still applies normally.
  private matchesElection(election: any, filters: ReturnType<typeof this.selectedFilters>, now: Date, ignoreCategory?: FilterCategory): boolean {
    // Status: active / expired
    if (ignoreCategory !== 'status') {
      const statusSelected = filters.active || filters.expired;
      if (statusSelected) {
        const matchesStatus =
          (filters.active && !election.isExpired) || (filters.expired && election.isExpired);
        if (!matchesStatus) return false;
      } else if (election.isExpired) {
        // Default: hide expired elections when no status box is checked.
        return false;
      }
    }

    // Timing: this week / more than a month
    if (ignoreCategory !== 'timing') {
      const timeSelected = filters.thisWeek || filters.moreThanAMonth;
      if (timeSelected) {
        const dateStr = election.startDate || election.startsAt || election.date || election.createdAt;
        if (!dateStr) return false;
        const diffDays = (new Date(dateStr).getTime() - now.getTime()) / (1000 * 3600 * 24);
        const matchesTime =
          (filters.thisWeek && diffDays >= -7 && diffDays <= 7) ||
          (filters.moreThanAMonth && diffDays >= 28);
        if (!matchesTime) return false;
      }
    }

    // Participation: voted / not voted
    if (ignoreCategory !== 'participation') {
      const voteSelected = filters.voted || filters.unvoted;
      if (voteSelected) {
        const matchesVote =
          (filters.voted && election.hasUserVoted) ||
          (filters.unvoted && !election.hasUserVoted && !election.isExpired);
        if (!matchesVote) return false;
      }
    }

    // Type: political / commercial
    if (ignoreCategory !== 'type') {
      const typeSelected = filters.political || filters.commercial;
      if (typeSelected) {
        const type = election.type?.toLowerCase();
        const isPolitic = type === 'politic' || type === 'political';
        const isComercial = type === 'comercial' || type === 'commercial';
        const matchesType = (filters.political && isPolitic) || (filters.commercial && isComercial);
        if (!matchesType) return false;
      }
    }

    // Visibility: anonymous / non-anonymous
    if (ignoreCategory !== 'visibility') {
      const anonSelected = filters.anonymous || filters.nonAnonymous;
      if (anonSelected) {
        const matchesAnon =
          (filters.anonymous && election.isAnonymous) || (filters.nonAnonymous && !election.isAnonymous);
        if (!matchesAnon) return false;
      }
    }

    return true;
  }

  // Live counts shown next to each checkbox. Because this reads from the
  // same computed() signals as filteredElections, it recalculates the
  // instant any checkbox, search term, or the underlying election list
  // changes — there's no manual refresh or polling involved.
  filterCounts = computed(() => {
    const list = this.searchFilteredElections();
    const filters = this.selectedFilters();
    const now = new Date();

    const countWhere = (category: FilterCategory, extra: (e: any) => boolean) =>
      list.filter((election: any) => this.matchesElection(election, filters, now, category) && extra(election)).length;

    const isType = (election: any, ...values: string[]) => values.includes(election.type?.toLowerCase());

    return {
      active: countWhere('status', (e) => !e.isExpired),
      expired: countWhere('status', (e) => e.isExpired),
      thisWeek: countWhere('timing', (e) => {
        const dateStr = e.startDate || e.startsAt || e.date || e.createdAt;
        if (!dateStr) return false;
        const diffDays = (new Date(dateStr).getTime() - now.getTime()) / (1000 * 3600 * 24);
        return diffDays >= -7 && diffDays <= 7;
      }),
      moreThanAMonth: countWhere('timing', (e) => {
        const dateStr = e.startDate || e.startsAt || e.date || e.createdAt;
        if (!dateStr) return false;
        const diffDays = (new Date(dateStr).getTime() - now.getTime()) / (1000 * 3600 * 24);
        return diffDays >= 28;
      }),
      voted: countWhere('participation', (e) => e.hasUserVoted),
      unvoted: countWhere('participation', (e) => !e.hasUserVoted && !e.isExpired),
      political: countWhere('type', (e) => isType(e, 'politic', 'political')),
      commercial: countWhere('type', (e) => isType(e, 'comercial', 'commercial')),
      anonymous: countWhere('visibility', (e) => e.isAnonymous),
      nonAnonymous: countWhere('visibility', (e) => !e.isAnonymous)
    };
  });

  filteredElections = computed(() => {
    const filters = this.selectedFilters();
    const query = this.searchQuery().toLowerCase().trim();
    const list = this.elections();
    const now = new Date();

    return list.filter((election: any) => {
      if (query && !election.title.toLowerCase().includes(query)) return false;
      return this.matchesElection(election, filters, now);
    });
  });

  toggleFilter(key: keyof ReturnType<typeof this.selectedFilters>): void {
    const count = this.filterCounts()[key] ?? 0;
    if (count === 0) {
      return;
    }
    this.selectedFilters.update(current => {
      return { ...current, [key]: !current[key] };
    });
  }

  clearAllFilters(): void {
    this.selectedFilters.update(current => {
      const cleared = { ...current };
      for (const key of Object.keys(cleared) as (keyof typeof cleared)[]) {
        cleared[key] = false;
      }
      return cleared;
    });
  }

  toggleGroup(key: string): void {
    this.expandedGroups.update((current) => ({ ...current, [key]: !current[key] }));
  }

  isGroupExpanded(key: string): boolean {
    return this.expandedGroups()[key] ?? true;
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
  }

  switchLanguage(lang: string): void {
    this.translateService.use(lang);
  }

  votedOptionLabel(election: ElectionDto): string | null {
    if (election.userVoteOptionLabel) return election.userVoteOptionLabel;
    if (!election.userVoteOptionId) return null;
    const option = election.options.find((o) => o.id === election.userVoteOptionId);
    return option?.label ?? null;
  }

  ngOnInit(): void {
    this.loadElections();
  }

  loadElections(): void {
    this.isLoading.set(true);
    this.votingService.getElections().subscribe({
      next: (data) => {
        this.elections.set(data);
        this.loadUserVoteDetails(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  isUpcoming(election: any): boolean {
    const startDate = new Date(election.startDate || election.startsAt);
    return startDate > new Date();
  }

  private loadUserVoteDetails(elections: ElectionDto[]): void {
    const votedElections = elections.filter((e) => e.hasUserVoted);
    for (const election of votedElections) {
      this.votingService.getMyVote(election.id).subscribe({
        next: (vote) => {
          this.elections.update((current) =>
            current.map((item) =>
              item.id === election.id
                ? {
                    ...item,
                    userVoteOptionId: vote.optionId,
                    userVoteOptionLabel:
                      vote.optionLabel ??
                      item.options.find((o) => o.id === vote.optionId)?.label
                  }
                : item
            )
          );
        },
        error: () => {}
      });
    }
  }
}