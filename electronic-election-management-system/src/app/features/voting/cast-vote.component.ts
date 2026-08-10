import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CdkDragDrop, CdkDrag, CdkDropList, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto, VoterDeclarationDto, OptionDto } from '../../core/models/voting.model';
import { VoterDeclarationModalComponent } from './voter-declaration-modal.component';
import { INPUT_LIMITS } from '../../core/validators/input.validators';

@Component({
  selector: 'app-cast-vote',
  standalone: true,
  imports: [CommonModule, VoterDeclarationModalComponent, TranslatePipe, CdkDropList, CdkDrag],
  templateUrl: './cast-vote.component.html',
  styleUrl: './cast-vote.component.scss'
})
export class CastVoteComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private votingService = inject(VotingService);

  readonly freeTextAnswerMaxLength = INPUT_LIMITS.freeTextAnswer;
  readonly otherAnswerMaxLength = INPUT_LIMITS.otherAnswer;

  election = signal<ElectionDto | null>(null);
  // One or more selected option ids per question - a single-answer question
  // never holds more than one entry, but the shape stays an array either way
  // so multi-answer questions don't need a separate code path here.
  selectedOptionIds = signal<Record<string, string[]>>({});
  userVoteAnswers = signal<Record<string, string[]>>({});
  // Typed answers for FreeText questions, and for Choice questions' "Other"
  // answer, keyed by questionId - mirrors the selectedOptionIds/userVoteAnswers
  // pair, but for text instead of option picks.
  textAnswers = signal<Record<string, string>>({});
  userTextAnswers = signal<Record<string, string>>({});
  // Whether the "Other" pseudo-option is active for a Choice question, keyed
  // by questionId. Irrelevant for FreeText questions (always "on").
  otherSelected = signal<Record<string, boolean>>({});
  userOtherSelected = signal<Record<string, boolean>>({});

  availableRankingOptions = signal<Record<string, OptionDto[]>>({});
  userAvailableRankingOptions = signal<Record<string, OptionDto[]>>({});
  rankedOptions = signal<Record<string, OptionDto[]>>({});
  userRankedOptions = signal<Record<string, OptionDto[]>>({});

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
      : [{
        id: '', text: election.question ?? election.title, displayOrder: 0, isRequired: true,
        allowMultipleAnswers: false, questionType: 'Choice' as const, allowOtherOption: false,
        options: election.options
      }];
  }

  // Single-answer question: picking an option replaces whatever was selected before.
  // For an optional question, clicking the already-selected option again clears it back
  // to "no answer" - a required question always keeps exactly one selected once touched,
  // since it can never legitimately end up with zero.
  selectOption(questionId: string, optionId: string, isRequired: boolean = true): void {
    if (!this.canSelectOptions()) return;
    // Picking a real option always deselects "Other" for a single-answer question.
    this.otherSelected.update(current => ({ ...current, [questionId]: false }));
    this.selectedOptionIds.update(selected => {
      const current = selected[questionId] ?? [];
      if (!isRequired && current.includes(optionId)) {
        return { ...selected, [questionId]: [] };
      }
      return { ...selected, [questionId]: [optionId] };
    });
  }

  // Multiple-answer question: picking an option adds/removes it from the set.
  toggleOption(questionId: string, optionId: string): void {
    if (!this.canSelectOptions()) return;
    this.selectedOptionIds.update(selected => {
      const current = selected[questionId] ?? [];
      const next = current.includes(optionId)
        ? current.filter(id => id !== optionId)
        : [...current, optionId];
      return { ...selected, [questionId]: next };
    });
  }

  isOptionSelected(questionId: string, optionId: string): boolean {
    return (this.selectedOptionIds()[questionId] ?? []).includes(optionId);
  }

  isOtherSelected(questionId: string): boolean {
    return this.otherSelected()[questionId] ?? false;
  }

  // Toggles the "Other" pseudo-option for a Choice question. For a single-answer
  // question it behaves like a radio pick (mutually exclusive with real options,
  // clicking it again while required keeps it selected); for a multiple-answer
  // question it's an independent checkbox alongside any selected options.
  toggleOther(questionId: string, isMultiple: boolean, isRequired: boolean = true): void {
    if (!this.canSelectOptions()) return;
    if (isMultiple) {
      this.otherSelected.update(current => ({ ...current, [questionId]: !current[questionId] }));
      return;
    }
    const isOn = this.isOtherSelected(questionId);
    if (!isRequired && isOn) {
      this.otherSelected.update(current => ({ ...current, [questionId]: false }));
      return;
    }
    this.otherSelected.update(current => ({ ...current, [questionId]: true }));
    this.selectedOptionIds.update(selected => ({ ...selected, [questionId]: [] }));
  }

  setTextAnswer(questionId: string, text: string): void {
    if (!this.canSelectOptions()) return;
    this.textAnswers.update(current => ({ ...current, [questionId]: text }));
  }

  getTextAnswer(questionId: string): string {
    return this.textAnswers()[questionId] ?? '';
  }

  /** Fills a FreeText question's answer with a suggestion chip's text - not a
   * selection, just a convenience prefill the voter can still edit freely. */
  applySuggestion(questionId: string, suggestionText: string): void {
    this.setTextAnswer(questionId, suggestionText);
  }

  /** True when a Choice question's "Other" text exactly matches one of its own
   * fixed options - that would silently split one option's tally between the
   * real pick and a redundant "Other" answer, so it's treated as invalid. */
  isOtherAnswerDuplicate(question: { id: string; options: { label: string }[] }): boolean {
    const text = this.getTextAnswer(question.id).trim().toLocaleLowerCase();
    if (!text) return false;
    return question.options.some(option => option.label.trim().toLocaleLowerCase() === text);
  }

  dropRankingOption(event: CdkDragDrop<OptionDto[]>, questionId: string): void {
    if (!this.canSelectOptions()) return;
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex,
      );
    }
    this.rankedOptions.update(r => ({ ...r }));
    this.availableRankingOptions.update(a => ({ ...a }));
  }

  hasAllAnswers(): boolean {
    const election = this.election();
    return !!election && this.questions(election).every(q => {
      if (q.questionType !== 'FreeText' && q.allowOtherOption && this.isOtherSelected(q.id) &&
        this.isOtherAnswerDuplicate(q)) {
        return false;
      }
      if (q.isRequired === false) return true;
      if (q.questionType === 'Ranking') {
        return (this.rankedOptions()[q.id]?.length ?? 0) > 0;
      }
      if (q.questionType === 'FreeText') {
        return this.getTextAnswer(q.id).trim().length > 0;
      }
      const optionCount = this.selectedOptionIds()[q.id]?.length ?? 0;
      const hasOtherAnswer = q.allowOtherOption && this.isOtherSelected(q.id) &&
        this.getTextAnswer(q.id).trim().length > 0;
      return optionCount > 0 || hasOtherAnswer;
    });
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

  private resetToUserVote(): void {
    if (Object.keys(this.userVoteAnswers()).length)
      this.selectedOptionIds.set({ ...this.userVoteAnswers() });
    this.textAnswers.set({ ...this.userTextAnswers() });
    this.otherSelected.set({ ...this.userOtherSelected() });

    const availableOpts: Record<string, OptionDto[]> = {};
    const rankedOpts: Record<string, OptionDto[]> = {};
    for (const [k, v] of Object.entries(this.userAvailableRankingOptions())) availableOpts[k] = [...v];
    for (const [k, v] of Object.entries(this.userRankedOptions())) rankedOpts[k] = [...v];
    this.availableRankingOptions.set(availableOpts);
    this.rankedOptions.set(rankedOpts);
  }

  startEditVote(): void {
    const e = this.election();
    if (!e || e.isExpired || !e.hasUserVoted || !this.canEditVote()) return;

    this.resetToUserVote();
    this.errorMessageKey.set(null);
    this.successMessageKey.set(null);
    this.isEditingVote.set(true);
  }

  cancelEditVote(): void {
    this.isEditingVote.set(false);
    this.errorMessageKey.set(null);
    this.resetToUserVote();
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
        this.textAnswers.set({});
        this.userTextAnswers.set({});
        this.otherSelected.set({});
        this.userOtherSelected.set({});
        
        const current = this.election();
        if (current) {
          this.syncUserVoteFromElection({ ...current, hasUserVoted: false });
        }
        
        // Deleting consumes the same one-time change budget as editing does - if this stayed
        // true, someone could delete + revote in a loop to bypass the limit entirely.
        this.canEditVote.set(false);
        this.successMessageKey.set('vote.deleteSuccess');
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
    const availableOpts: Record<string, OptionDto[]> = {};
    const rankedOpts: Record<string, OptionDto[]> = {};
    
    election.questions?.forEach(q => {
      if (q.questionType === 'Ranking') {
        availableOpts[q.id] = [...q.options];
        rankedOpts[q.id] = [];
      }
    });
    
    this.availableRankingOptions.set(availableOpts);
    this.rankedOptions.set(rankedOpts);

    if (!election.hasUserVoted) return;
    if (election.userVoteOptionId) {
      this.userVoteOptionId.set(election.userVoteOptionId);
      const answers = { [this.questions(election)[0]?.id ?? '']: [election.userVoteOptionId] };
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
        this.userVoteOptionId.set(vote.optionId ?? null);
        if (vote.answers?.length) {
          // A multiple-answer question can contribute more than one answer row
          // with the same questionId - group them instead of overwriting. A
          // FreeText/Other answer carries text instead of an optionId.
          const election = this.election();
          const questionsById = new Map((election ? this.questions(election) : []).map(q => [q.id, q]));
          const answers: Record<string, string[]> = {};
          const texts: Record<string, string> = {};
          const others: Record<string, boolean> = {};
          const ranked: Record<string, { option: OptionDto; rank: number }[]> = {};
          
          election?.questions?.filter(q => q.questionType === 'Ranking').forEach(q => ranked[q.id] = []);

          for (const answer of vote.answers) {
            if (answer.rank !== undefined && answer.rank !== null && answer.optionId) {
              const option = questionsById.get(answer.questionId)?.options.find(o => o.id === answer.optionId);
              if (option) {
                ranked[answer.questionId].push({ option, rank: answer.rank });
              }
            } else if (answer.text !== undefined && answer.text !== null) {
              texts[answer.questionId] = answer.text;
              if (questionsById.get(answer.questionId)?.questionType === 'Choice') {
                others[answer.questionId] = true;
              }
            } else if (answer.optionId) {
              (answers[answer.questionId] ??= []).push(answer.optionId);
            }
          }
          
          const availableOpts = { ...this.availableRankingOptions() };
          const rankedOpts: Record<string, OptionDto[]> = {};
          for (const qId in ranked) {
            rankedOpts[qId] = ranked[qId].sort((a, b) => a.rank - b.rank).map(x => x.option);
            const allOpts = questionsById.get(qId)?.options ?? [];
            const rankedIds = new Set(rankedOpts[qId].map(o => o.id));
            availableOpts[qId] = allOpts.filter(o => !rankedIds.has(o.id));
          }
          
          this.availableRankingOptions.set(availableOpts);
          this.userAvailableRankingOptions.set(structuredClone(availableOpts));
          this.rankedOptions.set(rankedOpts);
          this.userRankedOptions.set(structuredClone(rankedOpts));

          this.selectedOptionIds.set(answers);
          this.userVoteAnswers.set(answers);
          this.textAnswers.set(texts);
          this.userTextAnswers.set(texts);
          this.otherSelected.set(others);
          this.userOtherSelected.set(others);
        } else {
          const election = this.election();
          const answers = vote.optionId
            ? { [election ? this.questions(election)[0]?.id ?? '' : '']: [vote.optionId] }
            : {};
          this.selectedOptionIds.set(answers);
          this.userVoteAnswers.set(answers);
        }
        this.userVoteOptionLabel.set(vote.optionLabel ?? this.optionLabelById(vote.optionId ?? null));
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
    const optionIds = Object.values(this.selectedOptionIds()).flat();
    if (!this.hasAllAnswers()) return;
    const optionId = optionIds[0];
    const election = this.election();
    // Only send text for a FreeText question (always), or a Choice question
    // where "Other" is actively selected - a stale typed value left over from
    // a since-deselected "Other" must not be submitted.
    const textAnswers = election
      ? this.questions(election)
          .filter(q => q.questionType === 'FreeText' || (q.allowOtherOption && this.isOtherSelected(q.id)))
          .map(q => ({ questionId: q.id, text: this.getTextAnswer(q.id).trim() }))
          .filter(answer => answer.text.length > 0)
      : [];

    const rankedOptionsPayload = election ? this.questions(election)
      .filter(q => q.questionType === 'Ranking')
      .flatMap(q => (this.rankedOptions()[q.id] ?? []).map((opt, index) => ({
        optionId: opt.id,
        rank: index + 1
      }))) : [];

    this.isSubmitting.set(true);
    this.errorMessageKey.set(null);
    this.successMessageKey.set(null);

    const payload = { electionId: this.electionId, optionId, optionIds, textAnswers, rankedOptions: rankedOptionsPayload, voterDeclaration };
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
    this.userVoteOptionId.set(optionId ?? null);
    this.userVoteAnswers.set({ ...this.selectedOptionIds() });
    this.userTextAnswers.set({ ...this.textAnswers() });
    this.userOtherSelected.set({ ...this.otherSelected() });
    this.userVoteOptionLabel.set(this.optionLabelById(optionId));

    this.userAvailableRankingOptions.set(structuredClone(this.availableRankingOptions()));
    this.userRankedOptions.set(structuredClone(this.rankedOptions()));

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
