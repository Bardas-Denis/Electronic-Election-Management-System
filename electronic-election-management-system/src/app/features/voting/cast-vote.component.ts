import { Component, OnInit, inject, signal, computed } from '@angular/core';
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
  userVoteOptionId = signal<string | null>(null);
  userVoteOptionLabel = signal<string | null>(null);
  isEditingVote = signal(false);
  isDeletingVote = signal(false);
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  showDeclarationModal = signal(false);
  // Whether the current vote can still be edited - false once the one allowed edit is used.
  // Starts true optimistically; loadMyVote() corrects it as soon as the real vote loads.
  canEditVote = signal(true);

  canSelectOptions = computed(() => {
    const e = this.election();
    if (!e || e.isExpired) return false;
    if (this.isSubmitting() || this.isDeletingVote()) return false;
    return !e.hasUserVoted || this.isEditingVote();
  });

  private electionId!: string;

  ngOnInit(): void {
    this.electionId = this.route.snapshot.paramMap.get('id')!;
    this.votingService.getElectionById(this.electionId).subscribe({
      next: (data) => {
        this.election.set(data);
        this.syncUserVoteFromElection(data);
        if (data.hasUserVoted) {
          this.loadMyVote();
        }
      },
      error: () => this.errorMessage.set('Nu am putut incarca alegerea.')
    });
  }

  selectOption(optionId: string): void {
    if (!this.canSelectOptions()) return;
    this.selectedOptionId.set(optionId);
  }

  // "Trimite votul" - anonymous elections vote immediately, non-anonymous ones
  // first collect a voter declaration via the popup (see voter-declaration-modal).
  submitVote(): void {
    if (!this.selectedOptionId() || !this.canSelectOptions()) return;

    if (this.election()?.isAnonymous) {
      this.castOrUpdateVote();
    } else {
      this.showDeclarationModal.set(true);
    }
  }

  onDeclarationConfirmed(declaration: VoterDeclarationDto): void {
    this.showDeclarationModal.set(false);
    this.castOrUpdateVote(declaration);
  }

  onDeclarationCancelled(): void {
    this.showDeclarationModal.set(false);
  }

  startEditVote(): void {
    const e = this.election();
    if (!e || e.isExpired || !e.hasUserVoted || !this.canEditVote()) return;

    const votedOptionId = this.userVoteOptionId() ?? e.userVoteOptionId ?? null;
    if (votedOptionId) {
      this.selectedOptionId.set(votedOptionId);
    }
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isEditingVote.set(true);
  }

  cancelEditVote(): void {
    this.isEditingVote.set(false);
    this.errorMessage.set(null);
    const votedOptionId = this.userVoteOptionId() ?? this.election()?.userVoteOptionId ?? null;
    this.selectedOptionId.set(votedOptionId);
  }

  deleteVote(): void {
    const e = this.election();
    if (!e || !e.hasUserVoted || e.isExpired || this.isDeletingVote() || !this.canEditVote()) return;

    this.isDeletingVote.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.votingService.deleteMyVote(this.electionId).subscribe({
      next: () => {
        this.isDeletingVote.set(false);
        this.isEditingVote.set(false);
        this.userVoteOptionId.set(null);
        this.userVoteOptionLabel.set(null);
        this.selectedOptionId.set(null);
        // Deleting consumes the same one-time change budget as editing does - if this stayed
        // true, someone could delete + revote in a loop to bypass the limit entirely.
        this.canEditVote.set(false);
        this.successMessage.set('Răspunsul tău a fost șters.');
        const current = this.election();
        if (current) {
          this.election.set({
            ...current,
            hasUserVoted: false,
            userVoteOptionId: undefined,
            userVoteOptionLabel: undefined
          });
        }
      },
      error: (err) => {
        this.isDeletingVote.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Răspunsul nu a putut fi șters.');
      }
    });
  }

  optionLabelById(optionId: string | null): string | null {
    if (!optionId) return null;
    const option = this.election()?.options.find((o) => o.id === optionId);
    return option?.label ?? null;
  }

  private syncUserVoteFromElection(election: ElectionDto): void {
    if (!election.hasUserVoted) return;
    if (election.userVoteOptionId) {
      this.userVoteOptionId.set(election.userVoteOptionId);
      this.selectedOptionId.set(election.userVoteOptionId);
    }
    if (election.userVoteOptionLabel) {
      this.userVoteOptionLabel.set(election.userVoteOptionLabel);
    }
  }

  private loadMyVote(): void {
    this.votingService.getMyVote(this.electionId).subscribe({
      next: (vote) => {
        this.userVoteOptionId.set(vote.optionId);
        this.selectedOptionId.set(vote.optionId);
        this.userVoteOptionLabel.set(vote.optionLabel ?? this.optionLabelById(vote.optionId));
        this.canEditVote.set(vote.canEdit ?? true);
      },
      error: () => {
        if (!this.userVoteOptionLabel()) {
          this.userVoteOptionLabel.set(this.optionLabelById(this.userVoteOptionId()));
        }
      }
    });
  }

  private castOrUpdateVote(voterDeclaration?: VoterDeclarationDto): void {
    const optionId = this.selectedOptionId();
    if (!optionId) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const payload = { electionId: this.electionId, optionId, voterDeclaration };
    const wasEditing = this.isEditingVote();
    const request$ = wasEditing
      ? this.votingService.updateMyVote(payload)
      : this.votingService.castVote(payload);

    request$.subscribe({
      next: () => {
        this.applyVoteLocally(optionId);
        this.isSubmitting.set(false);
        if (wasEditing) {
          this.canEditVote.set(false);
        }
        this.isEditingVote.set(false);
        this.successMessage.set(wasEditing ? 'Răspunsul a fost actualizat.' : 'Votul tău a fost înregistrat.');
      },
      error: (err) => {
        // Note: on failure while editing (e.g. the one-time edit limit was already used),
        // we deliberately do NOT fall back to delete+recast - that would let someone bypass
        // the one-edit limit by just retrying. The error is shown as-is instead.
        this.isSubmitting.set(false);
        this.errorMessage.set(
          err?.error?.message ??
            (wasEditing
              ? 'Răspunsul nu a putut fi actualizat.'
              : 'Votul nu a putut fi înregistrat. Poate ai votat deja.')
        );
      }
    });
  }

  private applyVoteLocally(optionId: string): void {
    this.userVoteOptionId.set(optionId);
    this.userVoteOptionLabel.set(this.optionLabelById(optionId));

    const current = this.election();
    if (!current) return;

    this.election.set({
      ...current,
      hasUserVoted: true,
      userVoteOptionId: optionId,
      userVoteOptionLabel: this.optionLabelById(optionId) ?? undefined
    });
  }

  goToResults(): void {
    this.router.navigate(['/elections', this.electionId, 'results']);
  }
}