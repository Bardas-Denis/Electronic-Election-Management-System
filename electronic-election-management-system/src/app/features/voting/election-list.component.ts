import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { AuthService } from '../../core/services/auth.service';
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

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);
  searchQuery = signal<string>('');

  selectedFilters = signal<{
    politic: boolean;
    comercial: boolean;
    anonim: boolean;
    neanonim: boolean;
    votate: boolean;
    nevotate: boolean;
    expirate: boolean;
    active: boolean;
    saptamanaAceasta: boolean;
    maiMultDeOLuna: boolean;
  }>({
    politic: false,
    comercial: false,
    anonim: false,
    neanonim: false,
    votate: false,
    nevotate: false,
    expirate: false,
    active: false,
    saptamanaAceasta: false,
    maiMultDeOLuna: false
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
      const typeSelected = filters.politic || filters.comercial;
      if (typeSelected) {
        const isPolitic = election.type?.toLowerCase() === 'politic' || election.type?.toLowerCase() === 'political';
        const isComercial = election.type?.toLowerCase() === 'comercial' || election.type?.toLowerCase() === 'commercial';

        let matchesType = false;
        if (filters.politic && isPolitic) matchesType = true;
        if (filters.comercial && isComercial) matchesType = true;

        if (!matchesType) return false;
      }

      // 3. Anonymity Filter (Anonymous / Non-Anonymous)
      const anonSelected = filters.anonim || filters.neanonim;
      if (anonSelected) {
        let matchesAnon = false;
        if (filters.anonim && election.isAnonymous) matchesAnon = true;
        if (filters.neanonim && !election.isAnonymous) matchesAnon = true;
        if (!matchesAnon) return false;
      }

      // 4. Vote / Participation Filter
      const voteSelected = filters.votate || filters.nevotate;
      if (voteSelected) {
        let matchesVote = false;
        if (filters.votate && election.hasUserVoted) matchesVote = true;
        if (filters.nevotate && !election.hasUserVoted && !election.isExpired) matchesVote = true;
        if (!matchesVote) return false;
      }

      // 5. Time Status Filter (Active / Expired)
      const statusSelected = filters.active || filters.expirate;
      if (statusSelected) {
        let matchesStatus = false;
        if (filters.active && !election.isExpired) matchesStatus = true;
        if (filters.expirate && election.isExpired) matchesStatus = true;
        if (!matchesStatus) return false;
      } else {
        // Default: hide expired elections from the front page
        if (election.isExpired) return false;
      }

      // 6. Time Filter (This week / In more than a month)
      const timeSelected = filters.saptamanaAceasta || filters.maiMultDeOLuna;
      if (timeSelected) {
        const electionDateStr = (election as any).startDate || (election as any).startsAt || (election as any).date || (election as any).createdAt;
        if (electionDateStr) {
          const eDate = new Date(electionDateStr);
          const diffTime = eDate.getTime() - now.getTime();
          const diffDays = diffTime / (1000 * 3600 * 24);

          let timeMatched = false;

          // This week (between -7 and 7 days)
          if (filters.saptamanaAceasta && diffDays >= -7 && diffDays <= 7) {
            timeMatched = true;
          }

          // In more than a month (estimated >= 28 days)
          if (filters.maiMultDeOLuna && diffDays >= 28) {
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