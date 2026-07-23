import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto, VoterDeclarationDto } from '../../core/models/voting.model';
import { VoterDeclarationModalComponent } from './voter-declaration-modal.component';

@Component({
  selector: 'app-cast-vote',
  standalone: true,
  imports: [CommonModule, VoterDeclarationModalComponent, TranslatePipe],
  templateUrl: './cast-vote.component.html',
  styleUrl: './cast-vote.component.scss'
})
export class CastVoteComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private votingService = inject(VotingService);

  election = signal<ElectionDto | null>(null);
  selectedOptionIds = signal<Record<string, string>>({});
  userVoteAnswers = signal<Record<string, string>>({});
  userVoteOptionId = signal<string | null>(null);
  userVoteOptionLabel = signal<string | null>(null);
  isEditingVote = signal(false);
  isDeletingVote = signal(false);
  isSubmitting = signal(false);
  /** Translation key for inline errors — resolved via | translate in the template. */
  errorMessageKey = signal<string | null>(null);
  /** Translation key for inline success messages — resolved via | translate in the template. */
  successMessageKey = signal<string | null>(null);
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
      error: () => this.errorMessageKey.set('elections.loadOneFailed')
    });
  }

  questions(election: ElectionDto) {
    return election.questions?.length
      ? election.questions
      : [{ id: '', text: election.question ?? election.title, displayOrder: 0, options: election.options }];
  }

  selectOption(questionId: string, optionId: string): void {
    if (!this.canSelectOptions()) return;
    this.selectedOptionIds.update(selected => ({ ...selected, [questionId]: optionId }));
  }

  isOptionSelected(questionId: string, optionId: string): boolean {
    return this.selectedOptionIds()[questionId] === optionId;
  }

  hasAllAnswers(): boolean {
    const election = this.election();
    return !!election && this.questions(election).every(q => !!this.selectedOptionIds()[q.id]);
  }

  // "Trimite votul" - anonymous elections vote immediately, non-anonymous ones
  // first collect a voter declaration via the popup (see voter-declaration-modal).
  submitVote(): void {
    if (!this.hasAllAnswers() || !this.canSelectOptions()) return;

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

    if (Object.keys(this.userVoteAnswers()).length)
      this.selectedOptionIds.set({ ...this.userVoteAnswers() });
    this.errorMessageKey.set(null);
    this.successMessageKey.set(null);
    this.isEditingVote.set(true);
  }

  cancelEditVote(): void {
    this.isEditingVote.set(false);
    this.errorMessageKey.set(null);
    this.selectedOptionIds.set({ ...this.userVoteAnswers() });
  }

  deleteVote(): void {
    const e = this.election();
    if (!e || !e.hasUserVoted || e.isExpired || this.isDeletingVote() || !this.canEditVote()) return;

    this.isDeletingVote.set(true);
    this.errorMessageKey.set(null);
    this.successMessageKey.set(null);

    this.votingService.deleteMyVote(this.electionId).subscribe({
      next: () => {
        this.isDeletingVote.set(false);
        this.isEditingVote.set(false);
        this.userVoteOptionId.set(null);
        this.userVoteOptionLabel.set(null);
        this.selectedOptionIds.set({});
        this.userVoteAnswers.set({});
        // Deleting consumes the same one-time change budget as editing does - if this stayed
        // true, someone could delete + revote in a loop to bypass the limit entirely.
        this.canEditVote.set(false);
        this.successMessageKey.set('vote.deleteSuccess');
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
        const code: string | undefined = err?.error?.errorCode;
        this.errorMessageKey.set(code ? `errors.${code}` : 'vote.deleteFailed');
      }
    });
  }

  optionLabelById(optionId: string | null): string | null {
    if (!optionId) return null;
    const option = this.election()
      ? this.questions(this.election()!).flatMap(q => q.options).find(o => o.id === optionId)
      : undefined;
    return option?.label ?? null;
  }

  private syncUserVoteFromElection(election: ElectionDto): void {
    if (!election.hasUserVoted) return;
    if (election.userVoteOptionId) {
      this.userVoteOptionId.set(election.userVoteOptionId);
      const answers = { [this.questions(election)[0]?.id ?? '']: election.userVoteOptionId };
      this.selectedOptionIds.set(answers);
      this.userVoteAnswers.set(answers);
    }
    if (election.userVoteOptionLabel) {
      this.userVoteOptionLabel.set(election.userVoteOptionLabel);
    }
  }

  private loadMyVote(): void {
    this.votingService.getMyVote(this.electionId).subscribe({
      next: (vote) => {
        this.userVoteOptionId.set(vote.optionId);
        if (vote.answers?.length) {
          const answers = Object.fromEntries(vote.answers.map(answer => [answer.questionId, answer.optionId]));
          this.selectedOptionIds.set(answers);
          this.userVoteAnswers.set(answers);
        } else {
          const election = this.election();
          const answers = { [election ? this.questions(election)[0]?.id ?? '' : '']: vote.optionId };
          this.selectedOptionIds.set(answers);
          this.userVoteAnswers.set(answers);
        }
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
    const optionIds = Object.values(this.selectedOptionIds());
    if (!this.hasAllAnswers()) return;
    const optionId = optionIds[0];

    this.isSubmitting.set(true);
    this.errorMessageKey.set(null);
    this.successMessageKey.set(null);

    const payload = { electionId: this.electionId, optionId, optionIds, voterDeclaration };
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
        this.successMessageKey.set(wasEditing ? 'vote.updateSuccess' : 'vote.castSuccess');
      },
      error: (err) => {
        // Note: on failure while editing (e.g. the one-time edit limit was already used),
        // we deliberately do NOT fall back to delete+recast - that would let someone bypass
        // the one-edit limit by just retrying. The error is shown as-is instead.
        this.isSubmitting.set(false);
        const code: string | undefined = err?.error?.errorCode;
        this.errorMessageKey.set(
          code
            ? `errors.${code}`
            : (wasEditing ? 'vote.updateFailed' : 'vote.castFailed')
        );
      }
    });
  }

  private applyVoteLocally(optionId: string): void {
    this.userVoteOptionId.set(optionId);
    this.userVoteAnswers.set({ ...this.selectedOptionIds() });
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
