import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto, VoterDeclarationDto } from '../../core/models/voting.model';
import { VoterDeclarationModalComponent } from './voter-declaration-modal.component';

@Component({
  selector: 'app-cast-vote',
  standalone: true,
  imports: [CommonModule, VoterDeclarationModalComponent],
  templateUrl: './cast-vote.component.html',
  styleUrl: './cast-vote.component.scss'
})
export class CastVoteComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private votingService = inject(VotingService);

  election = signal<ElectionDto | null>(null);
  selectedOptionId = signal<string | null>(null);
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);
  voteConfirmed = signal(false);
  showDeclarationModal = signal(false);

  private electionId!: string;

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

  // "Trimite votul" - anonymous elections vote immediately, non-anonymous ones
  // first collect a voter declaration via the popup (see voter-declaration-modal).
  submitVote(): void {
    if (!this.selectedOptionId()) return;

    if (this.election()?.isAnonymous) {
      this.castVote();
    } else {
      this.showDeclarationModal.set(true);
    }
  }

  onDeclarationConfirmed(declaration: VoterDeclarationDto): void {
    this.showDeclarationModal.set(false);
    this.castVote(declaration);
  }

  onDeclarationCancelled(): void {
    this.showDeclarationModal.set(false);
  }

  private castVote(voterDeclaration?: VoterDeclarationDto): void {
    const optionId = this.selectedOptionId();
    if (!optionId) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.votingService
      .castVote({ electionId: this.electionId, optionId, voterDeclaration })
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