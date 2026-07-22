import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';
import { AuthService } from '../../core/services/auth.service';
import { ElectionDto } from '../../core/models/voting.model';

@Component({
  selector: 'app-election-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './election-list.component.html',
  styleUrl: './election-list.component.scss'
})
export class ElectionListComponent implements OnInit {
  private votingService = inject(VotingService);
  readonly authService = inject(AuthService); // template checks canManageElections() for manager actions

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);

  // alegerile active se afiseaza normal; cele expirate stau ascunse sub un buton
  activeElections = computed(() => this.elections().filter((e) => !e.isExpired));
  expiredElections = computed(() => this.elections().filter((e) => e.isExpired));
  showExpired = signal(false);

  toggleExpired(): void {
    this.showExpired.set(!this.showExpired());
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
        error: () => {
          // Keep existing list data; details are optional.
        }
      });
    }
  }
}