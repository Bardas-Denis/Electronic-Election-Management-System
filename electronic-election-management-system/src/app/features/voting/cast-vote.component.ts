import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto } from '../../core/models/voting.model';

@Component({
  selector: 'app-cast-vote',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cast-vote.component.html'
})
export class CastVoteComponent implements OnInit {
  election = signal<ElectionDto | null>(null);
  selectedOptionId = signal<string | null>(null);
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);
  voteConfirmed = signal(false);

  private electionId!: string;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private votingService: VotingService
  ) {}

  ngOnInit(): void {
    this.electionId = this.route.snapshot.paramMap.get('id')!;
    this.votingService.getElectionById(this.electionId).subscribe({
      next: (data) => this.election.set(data),
      error: () => this.errorMessage.set('Nu am putut incarca alegerea.')
    });
  }

  selectOption(optionId: string): void {
    this.selectedOptionId.set(optionId);
  }

  submitVote(): void {
    const optionId = this.selectedOptionId();
    if (!optionId) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.votingService
      .castVote({ electionId: this.electionId, optionId })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.voteConfirmed.set(true);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.errorMessage.set(
            err?.error?.message ?? 'Votul nu a putut fi inregistrat. Poate ai votat deja.'
          );
        }
      });
  }

  goToResults(): void {
    this.router.navigate(['/elections', this.electionId, 'results']);
  }
}
