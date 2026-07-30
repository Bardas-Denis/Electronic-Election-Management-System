import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { AuthService } from '../../core/services/auth.service';
import { ElectionListFiltersService } from '../../core/services/election-list-filters.service';
import { ElectionDto } from '../../core/models/voting.model';

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
  // This is the base list the per-option counts are computed from, so a
  // count answers "how many results if I also check this box".
  private searchFilteredElections = computed(() => {
    const query = this.searchQuery().toLowerCase().trim();
    const list = this.elections();
    if (!query) return list;
    return list.filter((e) => e.title.toLowerCase().includes(query));
  });

  filterCounts = computed(() => {
    const list = this.searchFilteredElections();
    const now = new Date();

    const matchesTiming = (election: any, kind: 'week' | 'month') => {
      const dateStr = election.startDate || election.startsAt || election.date || election.createdAt;
      if (!dateStr) return false;
      const diffDays = (new Date(dateStr).getTime() - now.getTime()) / (1000 * 3600 * 24);
      return kind === 'week' ? diffDays >= -7 && diffDays <= 7 : diffDays >= 28;
    };

    return {
      active: list.filter((e) => !e.isExpired).length,
      expired: list.filter((e) => e.isExpired).length,
      thisWeek: list.filter((e) => matchesTiming(e, 'week')).length,
      moreThanAMonth: list.filter((e) => matchesTiming(e, 'month')).length,
      voted: list.filter((e) => e.hasUserVoted).length,
      unvoted: list.filter((e) => !e.hasUserVoted && !e.isExpired).length,
      political: list.filter(
        (e) => e.type?.toLowerCase() === 'politic' || e.type?.toLowerCase() === 'political'
      ).length,
      commercial: list.filter(
        (e) => e.type?.toLowerCase() === 'comercial' || e.type?.toLowerCase() === 'commercial'
      ).length,
      anonymous: list.filter((e) => e.isAnonymous).length,
      nonAnonymous: list.filter((e) => !e.isAnonymous).length
    };
  });

  filteredElections = computed(() => {
    const filters = this.selectedFilters();
    const query = this.searchQuery().toLowerCase().trim();
    const list = this.elections();
    const now = new Date();

    return list.filter((election) => {
      // 1. Search by title
      if (query && !election.title.toLowerCase().includes(query)) {
        return false;
      }

      // 2. Category Filter (Political / Commercial)
      const typeSelected = filters.political || filters.commercial;
      if (typeSelected) {
        const isPolitic = election.type?.toLowerCase() === 'politic' || election.type?.toLowerCase() === 'political';
        const isComercial = election.type?.toLowerCase() === 'comercial' || election.type?.toLowerCase() === 'commercial';

        let matchesType = false;
        if (filters.political && isPolitic) matchesType = true;
        if (filters.commercial && isComercial) matchesType = true;

        if (!matchesType) return false;
      }

      // 3. Anonymity Filter (Anonymous / Non-Anonymous)
      const anonSelected = filters.anonymous || filters.nonAnonymous;
      if (anonSelected) {
        let matchesAnon = false;
        if (filters.anonymous && election.isAnonymous) matchesAnon = true;
        if (filters.nonAnonymous && !election.isAnonymous) matchesAnon = true;
        if (!matchesAnon) return false;
      }

      // 4. Vote / Participation Filter
      const voteSelected = filters.voted || filters.unvoted;
      if (voteSelected) {
        let matchesVote = false;
        if (filters.voted && election.hasUserVoted) matchesVote = true;
        if (filters.unvoted && !election.hasUserVoted && !election.isExpired) matchesVote = true;
        if (!matchesVote) return false;
      }

      // 5. Time Status Filter (Active / Expired)
      const statusSelected = filters.active || filters.expired;
      if (statusSelected) {
        let matchesStatus = false;
        if (filters.active && !election.isExpired) matchesStatus = true;
        if (filters.expired && election.isExpired) matchesStatus = true;
        if (!matchesStatus) return false;
      } else {
        // Default: hide expired elections from the front page
        if (election.isExpired) return false;
      }

      // 6. Time Filter (This week / More than a month)
      const timeSelected = filters.thisWeek || filters.moreThanAMonth;
      if (timeSelected) {
        const electionDateStr = (election as any).startDate || (election as any).startsAt || (election as any).date || (election as any).createdAt;
        if (electionDateStr) {
          const eDate = new Date(electionDateStr);
          const diffTime = eDate.getTime() - now.getTime();
          const diffDays = diffTime / (1000 * 3600 * 24);

          let timeMatched = false;

          // This week (between -7 and 7 days)
          if (filters.thisWeek && diffDays >= -7 && diffDays <= 7) {
            timeMatched = true;
          }

          // More than a month (estimated >= 28 days)
          if (filters.moreThanAMonth && diffDays >= 28) {
            timeMatched = true;
          }

          if (!timeMatched) return false;
        } else {
          return false;
        }
      }

      return true;
    });
  });

  toggleFilter(key: keyof ReturnType<typeof this.selectedFilters>): void {
    this.selectedFilters.update(current => {
      return { ...current, [key]: !current[key] };
    });
  }

  // Resets every filter checkbox to unchecked without touching the search
  // query, so the grid falls back to the default "active only" view.
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