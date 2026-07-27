import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import {
  CreateElectionQuestionDto,
  ElectionDto,
  InvitationCandidateDto,
  InvitationLabelDto
} from '../../core/models/voting.model';
import {
  dateRangeValidator,
  INPUT_LIMITS,
  trimmedRequired,
  uniqueOptionLabels
} from '../../core/validators/input.validators';

// This component handles both creation (/elections/new) and editing
// (/elections/:id/edit).
@Component({
  selector: 'app-create-election',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './create-election.component.html',
  styleUrl: './create-election.component.scss'
})
export class CreateElectionComponent implements OnInit {
  private fb = inject(FormBuilder);
  private votingService = inject(VotingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  isSubmitting = signal(false);
  isLoading = signal(false);
  /** Translation key for inline errors — resolved via | translate in the template. */
  errorMessageKey = signal<string | null>(null);
  // Editing is blocked after the election receives its first vote.
  isLocked = signal(false);
  invitationCandidates = signal<InvitationCandidateDto[]>([]);
  invitationCandidatesLoading = signal(false);
  invitationCandidatesErrorKey = signal<string | null>(null);
  invitationLabels = signal<InvitationLabelDto[]>([]);
  invitationLabelsLoading = signal(false);
  invitationLabelsErrorKey = signal<string | null>(null);
  invitedEmails = signal<string[]>([]);
  inviteEmailControl = this.fb.control('', [
    Validators.email,
    Validators.maxLength(INPUT_LIMITS.email)
  ]);
  labelSearchControl = this.fb.control('');
  candidateSearchControl = this.fb.control('');
  labelPickerOpen = signal(false);
  candidatePickerOpen = signal(false);
  private invitationCandidatesLoaded = false;
  private invitationLabelsLoaded = false;

  // A route ID indicates edit mode.
  private editingElectionId: string | null = null;
  isEditMode = signal(false);

  form = this.fb.group({
    title: ['', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.title)]],
    description: ['', Validators.maxLength(INPUT_LIMITS.description)],
    type: ['Politic', Validators.required],
    isAnonymous: [true],
    // Kept in the form payload even before the invitation UI is added, so editing
    // an existing closed election never accidentally makes it public.
    isClosed: [false],
    invitedUserIds: this.fb.control<string[]>([]),
    invitedEmails: this.fb.control<string[]>([]),
    invitedLabelIds: this.fb.control<string[]>([]),
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    questions: this.createQuestionsArray()
  }, { validators: dateRangeValidator });

  get isPoliticalElection(): boolean {
    return this.form.get('type')?.value === 'Politic';
  }

  get isClosedElection(): boolean {
    return this.form.get('isClosed')?.value === true;
  }

  ngOnInit(): void {
    this.form.get('type')?.valueChanges.subscribe((type) => {
      this.syncAnonymousState(type);
    });
    this.form.get('isClosed')?.valueChanges.subscribe((isClosed) => {
      if (isClosed && !this.isEditMode()) {
        this.loadInvitationCandidates();
        this.loadInvitationLabels();
      } else if (!isClosed) {
        this.clearInvitations();
      }
    });

    this.syncAnonymousState(this.form.get('type')?.value);

    this.editingElectionId = this.route.snapshot.paramMap.get('id');
    if (!this.editingElectionId) {
      return;
    }

    this.isEditMode.set(true);
    this.isLoading.set(true);

    this.votingService.getElectionById(this.editingElectionId).subscribe({
      next: (election) => {
        // Replace the complete array with controls initialized from the response.
        // Patching blank groups and then clearing/pushing their nested arrays could leave
        // the rendered form directives attached to the old empty controls.
        this.form.setControl(
          'questions',
          this.createQuestionsArray(normalizeEditableQuestions(election))
        );

        this.form.patchValue({
          title: election.title,
          description: election.description ?? '',
          type: election.type,
          isAnonymous: election.isAnonymous,
          isClosed: election.isClosed,
          startsAt: toDatetimeLocal(election.startsAt),
          endsAt: toDatetimeLocal(election.endsAt)
        });

        this.syncAnonymousState(this.form.get('type')?.value);

        // Once a vote has been recorded, the election can no longer be edited.
        // The backend also rejects the request; this disables the UI immediately.
        if (election.hasVotes) {
          this.form.disable({ emitEvent: false });
          this.isLocked.set(true);
          this.errorMessageKey.set('elections.lockedByVotes');
        }

        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Nu am putut incarca alegerea pentru editare:', err);
        this.errorMessageKey.set('elections.loadEditFailed');
        this.isLoading.set(false);
      }
    });
  }

  get questions(): FormArray {
    return this.form.get('questions') as FormArray;
  }

  questionOptions(questionIndex: number): FormArray {
    return this.questions.at(questionIndex).get('options') as FormArray;
  }

  private createOptionGroup(option?: { label?: string; description?: string; imageDataUrl?: string }) {
    return this.fb.group({
      label: [option?.label ?? '', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.shortText)]],
      description: [option?.description ?? '', Validators.maxLength(INPUT_LIMITS.description)],
      imageDataUrl: [option?.imageDataUrl ?? '']
    });
  }

  private createQuestionGroup(question?: CreateElectionQuestionDto) {
    const suppliedOptions = question?.options ?? [];
    const optionGroups = suppliedOptions.map(option => this.createOptionGroup(option));
    while (optionGroups.length < 2) optionGroups.push(this.createOptionGroup());

    return this.fb.group({
      text: [question?.text ?? '', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.question)]],
      options: this.fb.array(
        optionGroups,
        [
          Validators.minLength(2),
          Validators.maxLength(INPUT_LIMITS.maxOptionsPerQuestion),
          uniqueOptionLabels
        ]
      )
    });
  }

  private createQuestionsArray(questions: CreateElectionQuestionDto[] = []): FormArray {
    const groups = questions.length
      ? questions.map(question => this.createQuestionGroup(question))
      : [this.createQuestionGroup()];

    return this.fb.array(
      groups,
      [Validators.minLength(1), Validators.maxLength(INPUT_LIMITS.maxQuestions)]
    );
  }

  private syncAnonymousState(type: string | null | undefined): void {
    const anonymousControl = this.form.get('isAnonymous');

    if (!anonymousControl) {
      return;
    }

    if (type === 'Politic') {
      anonymousControl.setValue(false, { emitEvent: false });
      anonymousControl.disable({ emitEvent: false });
    } else {
      anonymousControl.enable({ emitEvent: false });
    }
  }

  addQuestion(): void {
    if (this.questions.length >= INPUT_LIMITS.maxQuestions) return;
    this.questions.push(this.createQuestionGroup());
  }

  removeQuestion(index: number): void {
    if (this.questions.length > 1) this.questions.removeAt(index);
  }

  addOption(questionIndex: number): void {
    const options = this.questionOptions(questionIndex);
    if (options.length >= INPUT_LIMITS.maxOptionsPerQuestion) return;
    options.push(this.createOptionGroup());
  }

  // Every question must retain at least two options.
  removeOption(questionIndex: number, optionIndex: number): void {
    const options = this.questionOptions(questionIndex);
    if (options.length > 2) {
      options.removeAt(optionIndex);
    }
  }

  onOptionImageSelected(event: Event, questionIndex: number, optionIndex: number): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const allowedTypes = new Set(['image/png', 'image/jpeg', 'image/webp', 'image/gif']);
    if (!allowedTypes.has(file.type) || file.size > 2_000_000) {
      this.errorMessageKey.set('elections.optionImageInvalid');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => this.questionOptions(questionIndex).at(optionIndex)
      .get('imageDataUrl')?.setValue(reader.result as string);
    reader.readAsDataURL(file);
  }

  removeOptionImage(questionIndex: number, optionIndex: number): void {
    this.questionOptions(questionIndex).at(optionIndex).get('imageDataUrl')?.setValue('');
  }

  addInviteEmail(): void {
    const normalizedEmail = this.inviteEmailControl.value?.trim().toLowerCase() ?? '';
    if (!normalizedEmail || this.inviteEmailControl.invalid) {
      this.inviteEmailControl.markAsTouched();
      return;
    }

    if (!this.invitedEmails().includes(normalizedEmail)) {
      const emails = [...this.invitedEmails(), normalizedEmail];
      this.invitedEmails.set(emails);
      this.form.controls.invitedEmails.setValue(emails);
    }

    this.inviteEmailControl.reset('');
  }

  removeInviteEmail(email: string): void {
    const emails = this.invitedEmails().filter(item => item !== email);
    this.invitedEmails.set(emails);
    this.form.controls.invitedEmails.setValue(emails);
  }

  filteredInvitationCandidates(): InvitationCandidateDto[] {
    const query = this.candidateSearchControl.value?.trim().toLowerCase() ?? '';
    if (!query) {
      return this.invitationCandidates();
    }
    return this.invitationCandidates().filter(candidate =>
      candidate.email.toLowerCase().includes(query)
    );
  }

  selectedInvitationCandidates(): InvitationCandidateDto[] {
    const selectedIds = new Set(this.form.controls.invitedUserIds.value ?? []);
    return this.invitationCandidates().filter(candidate => selectedIds.has(candidate.id));
  }

  isInvitationCandidateSelected(candidateId: string): boolean {
    return (this.form.controls.invitedUserIds.value ?? []).includes(candidateId);
  }

  toggleCandidatePicker(): void {
    this.candidatePickerOpen.update(open => !open);
    if (!this.candidatePickerOpen()) {
      this.candidateSearchControl.reset('');
    }
  }

  toggleInvitationCandidate(candidateId: string, selected: boolean): void {
    const currentIds = this.form.controls.invitedUserIds.value ?? [];
    const nextIds = selected
      ? [...new Set([...currentIds, candidateId])]
      : currentIds.filter(id => id !== candidateId);
    this.form.controls.invitedUserIds.setValue(nextIds);
  }

  removeInvitationCandidate(candidateId: string): void {
    this.toggleInvitationCandidate(candidateId, false);
  }

  allInvitationCandidatesSelected(): boolean {
    const candidates = this.filteredInvitationCandidates();
    const selectedIds = new Set(this.form.controls.invitedUserIds.value ?? []);
    return candidates.length > 0 && candidates.every(candidate => selectedIds.has(candidate.id));
  }

  toggleAllInvitationCandidates(): void {
    const visibleIds = new Set(this.filteredInvitationCandidates().map(candidate => candidate.id));
    const currentIds = this.form.controls.invitedUserIds.value ?? [];

    const userIds = this.allInvitationCandidatesSelected()
      ? currentIds.filter(id => !visibleIds.has(id))
      : [...new Set([...currentIds, ...visibleIds])];

    this.form.controls.invitedUserIds.setValue(userIds);
  }

  retryInvitationCandidates(): void {
    this.invitationCandidatesErrorKey.set(null);
    this.invitationCandidatesLoaded = false;
    this.loadInvitationCandidates();
  }

  retryInvitationLabels(): void {
    this.invitationLabelsErrorKey.set(null);
    this.invitationLabelsLoaded = false;
    this.loadInvitationLabels();
  }

  isInvitationLabelSelected(labelId: string): boolean {
    return (this.form.controls.invitedLabelIds.value ?? []).includes(labelId);
  }

  filteredInvitationLabels(): InvitationLabelDto[] {
    const query = this.labelSearchControl.value?.trim().toLowerCase() ?? '';
    if (!query) {
      return this.invitationLabels();
    }
    return this.invitationLabels().filter(label =>
      label.name.toLowerCase().includes(query) ||
      label.category?.toLowerCase().includes(query)
    );
  }

  selectedInvitationLabels(): InvitationLabelDto[] {
    const selectedIds = new Set(this.form.controls.invitedLabelIds.value ?? []);
    return this.invitationLabels().filter(label => selectedIds.has(label.id));
  }

  toggleLabelPicker(): void {
    this.labelPickerOpen.update(open => !open);
    if (!this.labelPickerOpen()) {
      this.labelSearchControl.reset('');
    }
  }

  toggleInvitationLabel(labelId: string, selected: boolean): void {
    const currentIds = this.form.controls.invitedLabelIds.value ?? [];
    const nextIds = selected
      ? [...new Set([...currentIds, labelId])]
      : currentIds.filter(id => id !== labelId);
    this.form.controls.invitedLabelIds.setValue(nextIds);
  }

  removeInvitationLabel(labelId: string): void {
    this.toggleInvitationLabel(labelId, false);
  }

  allVisibleInvitationLabelsSelected(): boolean {
    const availableLabels = this.filteredInvitationLabels()
      .filter(label => label.userCount > 0);
    const selectedIds = new Set(this.form.controls.invitedLabelIds.value ?? []);
    return availableLabels.length > 0 &&
      availableLabels.every(label => selectedIds.has(label.id));
  }

  toggleAllInvitationLabels(): void {
    const visibleIds = new Set(
      this.filteredInvitationLabels()
        .filter(label => label.userCount > 0)
        .map(label => label.id)
    );
    const currentIds = this.form.controls.invitedLabelIds.value ?? [];
    const nextIds = this.allVisibleInvitationLabelsSelected()
      ? currentIds.filter(id => !visibleIds.has(id))
      : [...new Set([...currentIds, ...visibleIds])];
    this.form.controls.invitedLabelIds.setValue(nextIds);
  }

  private loadInvitationCandidates(): void {
    if (this.invitationCandidatesLoaded || this.invitationCandidatesLoading()) {
      return;
    }

    this.invitationCandidatesLoading.set(true);
    this.invitationCandidatesErrorKey.set(null);
    this.votingService.getInvitationCandidates().subscribe({
      next: (candidates) => {
        this.invitationCandidates.set(candidates);
        this.invitationCandidatesLoaded = true;
        this.invitationCandidatesLoading.set(false);
      },
      error: () => {
        this.invitationCandidatesErrorKey.set('elections.inviteCandidatesLoadFailed');
        this.invitationCandidatesLoading.set(false);
      }
    });
  }

  private loadInvitationLabels(): void {
    if (this.invitationLabelsLoaded || this.invitationLabelsLoading()) {
      return;
    }

    this.invitationLabelsLoading.set(true);
    this.invitationLabelsErrorKey.set(null);
    this.votingService.getInvitationLabels().subscribe({
      next: (labels) => {
        this.invitationLabels.set(labels);
        this.invitationLabelsLoaded = true;
        this.invitationLabelsLoading.set(false);
      },
      error: () => {
        this.invitationLabelsErrorKey.set('elections.inviteLabelsLoadFailed');
        this.invitationLabelsLoading.set(false);
      }
    });
  }

  private clearInvitations(): void {
    this.form.controls.invitedUserIds.setValue([]);
    this.form.controls.invitedEmails.setValue([]);
    this.form.controls.invitedLabelIds.setValue([]);
    this.invitedEmails.set([]);
    this.inviteEmailControl.reset('');
    this.labelSearchControl.reset('');
    this.candidateSearchControl.reset('');
    this.labelPickerOpen.set(false);
    this.candidatePickerOpen.set(false);
  }

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.hasError('invalidDateRange')) {
      this.errorMessageKey.set('errors.invalidDateRange');
      return;
    }

    if (this.isClosedElection && !this.editingElectionId && this.inviteEmailControl.value?.trim()) {
      if (this.inviteEmailControl.invalid) {
        this.inviteEmailControl.markAsTouched();
        return;
      }
      this.addInviteEmail();
    }

    if (this.form.invalid || this.isLocked()) {
      this.errorMessageKey.set('elections.validationError');
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessageKey.set(null);

    const payload = this.form.getRawValue() as any;
    payload.question = payload.questions[0].text;
    payload.options = payload.questions[0].options;
    if (!payload.isClosed || this.editingElectionId) {
      // Existing invitation membership is managed by the invitation endpoints.
      payload.invitedUserIds = [];
      payload.invitedEmails = [];
      payload.invitedLabelIds = [];
    }
    // Ensure the datetime-local values are sent as UTC ISO strings so server comparisons use UTC correctly
    try {
      payload.startsAt = new Date(payload.startsAt).toISOString();
      payload.endsAt = new Date(payload.endsAt).toISOString();
    } catch { /* fall back to raw values if parsing fails */ }
    const request$ = this.editingElectionId
      ? this.votingService.updateElection(this.editingElectionId, payload)
      : this.votingService.createElection(payload);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const code: string | undefined = err?.error?.errorCode;
        this.errorMessageKey.set(
          code
            ? `errors.${code}`
            : (this.editingElectionId ? 'elections.saveFailed' : 'elections.createFailed')
        );
      }
    });
  }
}

// Converts the backend ISO date into the format expected by <input type="datetime-local">.
function toDatetimeLocal(isoDate: string): string {
  const date = new Date(isoDate);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * Converts both current multi-question responses and legacy single-question
 * responses into the exact shape used by the edit form.
 */
export function normalizeEditableQuestions(election: ElectionDto): CreateElectionQuestionDto[] {
  if (Array.isArray(election.questions) && election.questions.length > 0) {
    return election.questions.map((question, index) => ({
      text: question.text || (index === 0 ? election.question : '') || '',
      options: Array.isArray(question.options) && question.options.length > 0
        ? question.options.map(option => ({
            label: option.label ?? '',
            description: option.description ?? '',
            imageDataUrl: option.imageDataUrl ?? ''
          }))
        : index === 0
          ? (election.options ?? []).map(option => ({
              label: option.label ?? '',
              description: option.description ?? '',
              imageDataUrl: option.imageDataUrl ?? ''
            }))
          : []
    }));
  }

  return [{
    text: election.question || election.title || '',
    options: (election.options ?? []).map(option => ({
      label: option.label ?? '',
      description: option.description ?? '',
      imageDataUrl: option.imageDataUrl ?? ''
    }))
  }];
}
