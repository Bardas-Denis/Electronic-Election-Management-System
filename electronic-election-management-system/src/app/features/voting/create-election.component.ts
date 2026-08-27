import { ChangeDetectorRef, Component, OnInit, inject, signal, ElementRef, HostListener, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { VotingService } from '../../core/services/voting.service';
import { ScoringSchemesService } from '../../core/services/scoring-schemes.service';
import { ElectionImageService } from '../../core/services/election-image.service';
import { ElectionImageDirective } from '../../core/directives/election-image.directive';
import { ScoringSchemeDto } from '../../core/models/scoring-schemes.model';
import { CdkDragDrop, CdkDropList, CdkDrag, CdkDragHandle } from '@angular/cdk/drag-drop';
import {
  AudienceConditionDto,
  AudienceGroupDto,
  CreateElectionQuestionDto,
  ElectionDto,
  ElectionInvitationDto,
  InvitationCandidateDto,
  InvitationLabelDto,
  QuestionType
} from '../../core/models/voting.model';
import {
  atLeastOneRequiredQuestion,
  dateRangeValidator,
  INPUT_LIMITS,
  optionsRequiredForChoiceQuestion,
  rankCountWithinOptions,
  trimmedRequired,
  uniqueOptionLabels
}
 
from '../../core/validators/input.validators';
import { CreateScoringSchemeModalComponent } from './create-scoring-scheme-modal.component';

// This component handles both creation (/elections/new) and editing
// (/elections/:id/edit).
@Component({
  selector: 'app-create-election',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CreateScoringSchemeModalComponent,
    ElectionImageDirective,
    CdkDropList,
    CdkDrag,
    CdkDragHandle
  ],
  templateUrl: './create-election.component.html',
  styleUrl: './create-election.component.scss'
})
export class CreateElectionComponent implements OnInit {
  private fb = inject(FormBuilder);
  private votingService = inject(VotingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private translate = inject(TranslateService);
  private scoringSchemesService = inject(ScoringSchemesService);
  private images = inject(ElectionImageService);
  private changeDetector = inject(ChangeDetectorRef);

  scoringSchemes = signal<ScoringSchemeDto[]>([]);
  scoringSchemesLoading = signal(false);
  scoringSchemesErrorKey = signal<string | null>(null);
  activeQuestionIndexForScheme = signal<number | null>(null);

  isSubmitting = signal(false);
  isUploadingImage = signal(false);
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
  excludedGroupUserIds = signal<Set<string>>(new Set());
  showInfoPopover = signal(false);
  showExcludedUsersDrawer = signal(false);
  inviteEmailControl = this.fb.control('', [
    Validators.email,
    Validators.maxLength(INPUT_LIMITS.email)
  ]);
  labelSearchControl = this.fb.control('');
  candidateSearchControl = this.fb.control('');
  labelPickerOpen = signal(false);
  candidatePickerOpen = signal(false);
  showAllCandidateChips = signal(false);
  showAllExcludedChips = signal(false);
  readonly CHIP_PREVIEW = 5;
  private invitationCandidatesLoaded = false;
  private invitationLabelsLoaded = false;
  /**
   * Tracks which condition picker is open: [groupIndex, conditionIndex | -1 for "add new"].
   * -1 for conditionIndex means the "add condition" picker for that group is open.
   */
  openConditionPicker = signal<{ groupIndex: number; conditionIndex: number } | null>(null);
  conditionSearchControl = this.fb.control('');
  private elRef = inject(ElementRef);

  /** Existing invitations loaded when entering edit mode for a closed election. */
  existingInvitations = signal<ElectionInvitationDto[]>([]);

  @ViewChild('labelPickerRef') labelPickerRef?: ElementRef;
  @ViewChild('candidatePickerRef') candidatePickerRef?: ElementRef;

  // A route ID indicates edit mode.
  private editingElectionId: string | null = null;
  isEditMode = signal(false);

  // ── FUNCȚIONALITATE NOUĂ: Plierea cardurilor (Collapse / Ranking Mode) ──
  collapsedQuestions = signal<Set<number>>(new Set());

  toggleCollapse(index: number) {
    const current = new Set(this.collapsedQuestions());
    if (current.has(index)) {
      current.delete(index);
    } else {
      current.add(index);
    }
    this.collapsedQuestions.set(current);
  }
  onDragStarted(index: number): void {
    const current = new Set(this.collapsedQuestions());
    current.add(index);
    this.collapsedQuestions.set(current);
  }

  isCollapsed(index: number): boolean {
    return this.collapsedQuestions().has(index);
  }
  // --------------------------------------------------------------------------

  form = this.fb.group({
    title: ['', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.title)]],
    description: ['', Validators.maxLength(INPUT_LIMITS.description)],
    type: ['Politic', Validators.required],
    isAnonymous: [true],
    // Kept in the form payload even before the invitation UI is added, so editing
    // an existing closed election never accidentally makes it public.
    isClosed: [false],
    // Defaults to true: elections are immediately visible unless the owner explicitly unchecks.
    // When false, the election is hidden from voters until the owner clicks Start.
    isVisible: [true],
    invitedUserIds: this.fb.control<string[]>([]),
    invitedEmails: this.fb.control<string[]>([]),
    audienceGroups: this.fb.array<ReturnType<CreateElectionComponent['createGroupArray']>>([]),
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    questions: this.createQuestionsArray()
  }, { validators: dateRangeValidator });

  get audienceGroupsArray(): FormArray {
    return this.form.get('audienceGroups') as FormArray;
  }

  get isPoliticalElection(): boolean {
    return this.form.get('type')?.value === 'Politic';
  }

  get isClosedElection(): boolean {
    return this.form.get('isClosed')?.value === true;
  }

  ngOnInit(): void {
    this.loadScoringSchemes();

    this.form.get('type')?.valueChanges.subscribe((type) => {
      this.syncAnonymousState(type);
    });
    this.form.get('isClosed')?.valueChanges.subscribe((isClosed) => {
      if (isClosed) {
        this.loadInvitationCandidates();
        this.loadInvitationLabels();
      } else {
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

    // Load the election details and its existing invitations in parallel
    forkJoin({
      election: this.votingService.getElectionById(this.editingElectionId),
      invitations: this.votingService.getElectionInvitations(this.editingElectionId).pipe(
        catchError(() => of([] as ElectionInvitationDto[]))
      )
    }).subscribe({
      next: ({ election, invitations }) => {
        // Replace the complete array with controls initialized from the response.
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
          isVisible: election.isVisible,
          startsAt: toDatetimeLocal(election.startsAt),
          endsAt: toDatetimeLocal(election.endsAt)
        });

        this.syncAnonymousState(this.form.get('type')?.value);

        // Once a vote has been recorded, the election can no longer be edited.
        // The backend also rejects the request; this disables the UI immediately.
        if (election.hasVotes) {
          this.form.disable({ emitEvent: false });
          this.isLocked.set(true);
        }

        // Pre-populate invitation pickers and audience group rules from existing election data
        if (election.isClosed) {
          this.setupEditInvitations(election, invitations);
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

  // Added method for the back to choices button
  goBackToMyElections(): void {
    this.router.navigate(['/elections/mine']);
  }

  get questions(): FormArray {
    return this.form.get('questions') as FormArray;
  }

  questionOptions(questionIndex: number): FormArray {
    return this.questions.at(questionIndex).get('options') as FormArray;
  }

  dropQuestion(event: CdkDragDrop<any>): void {
    if (this.isLocked() || event.previousIndex === event.currentIndex) return;

    const formGroup = this.questions.at(event.previousIndex);
    this.questions.removeAt(event.previousIndex);
    this.questions.insert(event.currentIndex, formGroup);
    
    this.syncCollapsedStateOnMove(event.previousIndex, event.currentIndex);

    this.questions.updateValueAndValidity();
  }
  
  onHandleKeydown(event: KeyboardEvent, index: number): void {
    if (this.isLocked()) return;

    if (event.key === 'ArrowUp' && index > 0) {
      event.preventDefault();
      
      const formGroup = this.questions.at(index);
      this.questions.removeAt(index);
      this.questions.insert(index - 1, formGroup);
      
      this.syncCollapsedStateOnMove(index, index - 1);
      
      this.questions.updateValueAndValidity();
      this.focusHandleAt(index - 1);
    } else if (event.key === 'ArrowDown' && index < this.questions.length - 1) {
      event.preventDefault();
      
      const formGroup = this.questions.at(index);
      this.questions.removeAt(index);
      this.questions.insert(index + 1, formGroup);
      
      this.syncCollapsedStateOnMove(index, index + 1);
      
      this.questions.updateValueAndValidity();
      this.focusHandleAt(index + 1);
    }
  }

  private focusHandleAt(index: number): void {
    queueMicrotask(() => {
      const handles = document.querySelectorAll<HTMLElement>('.question-drag-handle');
      if (handles[index]) {
        handles[index].focus();
      }
    });
  }
  
  private syncCollapsedStateOnMove(oldIndex: number, newIndex: number): void {
    const currentCollapsed = new Set(this.collapsedQuestions());
    const newCollapsed = new Set<number>();

    currentCollapsed.forEach(collapsedIndex => {
      if (collapsedIndex === oldIndex) {
        newCollapsed.add(newIndex);
      } else if (oldIndex < newIndex && collapsedIndex > oldIndex && collapsedIndex <= newIndex) {
        newCollapsed.add(collapsedIndex - 1);
      } else if (oldIndex > newIndex && collapsedIndex >= newIndex && collapsedIndex < oldIndex) {
        newCollapsed.add(collapsedIndex + 1);
      } else {
        newCollapsed.add(collapsedIndex);
      }
    });

    this.collapsedQuestions.set(newCollapsed);
  }

  private createOptionGroup(option?: { label?: string; description?: string; imageId?: string | null }) {
    return this.fb.group({
      label: [option?.label ?? '', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.shortText)]],
      description: [option?.description ?? '', Validators.maxLength(INPUT_LIMITS.description)],
      imageId: [option?.imageId ?? null]
    });
  }

  private createQuestionGroup(question?: CreateElectionQuestionDto) {
    const suppliedOptions = question?.options ?? [];
    const optionGroups = suppliedOptions.map(option => this.createOptionGroup(option));
    while (optionGroups.length < 2) optionGroups.push(this.createOptionGroup());

    return this.fb.group({
      text: [question?.text ?? '', [trimmedRequired, Validators.maxLength(INPUT_LIMITS.question)]],
      isRequired: [question?.isRequired ?? true],
      allowMultipleAnswers: [question?.allowMultipleAnswers ?? false],
      questionType: [question?.questionType ?? 'Choice'],
      allowOtherOption: [question?.allowOtherOption ?? false],
      // Whether the rule is on is tracked apart from the number itself. Deriving it from
      // the number instead would tear the input out of the DOM the moment the field goes
      // empty - which is exactly what happens while retyping a value.
      limitRankCount: [question?.requiredRankCount != null],
      requiredRankCount: [question?.requiredRankCount ?? null],
      scoringSchemeId: [question?.scoringSchemeId ?? null],
      imageId: [question?.imageId ?? null],
      options: this.fb.array(
        optionGroups,
        [
          optionsRequiredForChoiceQuestion,
          Validators.maxLength(INPUT_LIMITS.maxOptionsPerQuestion),
          uniqueOptionLabels
        ]
      )
    }, { validators: rankCountWithinOptions });
  }

  getQuestionType(questionIndex: number): QuestionType {
    return this.questions.at(questionIndex).get('questionType')?.value as QuestionType;
  }

  /**
   * Sets a question's type and applies the side effects.
   */
  setQuestionType(questionIndex: number, type: QuestionType): void {
    if (this.isLocked()) return; // Prevents modification in preview
    const group = this.questions.at(questionIndex);
    group.get('questionType')?.setValue(type);

    const sideEffects = freeTextTypeSideEffects(type);
    if (sideEffects) {
      group.patchValue(sideEffects);
    }

    if (type === 'Ranking' && !group.get('scoringSchemeId')?.value) {
      if (this.scoringSchemes().length > 0) {
        group.get('scoringSchemeId')?.setValue(this.scoringSchemes()[0].id);
      }
    }

    if (type === 'FreeText') {
      const optionsArray = this.questionOptions(questionIndex);
      while (optionsArray.length > 0) {
        optionsArray.removeAt(0);
      }
    } else {
      
      const optionsArray = this.questionOptions(questionIndex);
      if (optionsArray.length < 2) {
        while (optionsArray.length < 2) {
          optionsArray.push(this.createOptionGroup());
        }
      }
    }

    if (type !== 'Ranking') {
      // The count only means something on a Ranking question, and its input is hidden
      // off one - clear it rather than submit a value the creator can no longer see.
      group.get('limitRankCount')?.setValue(false);
      group.get('requiredRankCount')?.setValue(null);
    }

    this.questionOptions(questionIndex).updateValueAndValidity();
  }

  isRankCountLimited(questionIndex: number): boolean {
    return this.questions.at(questionIndex).get('limitRankCount')?.value === true;
  }

  /**
   * Turns the "rank exactly N" rule on or off. Switching it on starts at 3, or at the
   * option count when there are fewer than 3 to rank.
   */
  toggleRankCountLimit(questionIndex: number): void {
    if (this.isLocked()) return;
    const group = this.questions.at(questionIndex);
    const limit = group.get('limitRankCount');
    const count = group.get('requiredRankCount');
    if (!limit || !count) return;

    const enabled = !limit.value;
    limit.setValue(enabled);
    count.setValue(enabled ? Math.min(3, this.rankCountMax(questionIndex)) : null);
  }

  rankCountMax(questionIndex: number): number {
    return this.questionOptions(questionIndex).length;
  }

  hasRankCountError(questionIndex: number): boolean {
    const group = this.questions.at(questionIndex);
    return group.hasError('rankCountOutOfRange') && group.touched;
  }

  private createQuestionsArray(questions: CreateElectionQuestionDto[] = []): FormArray {
    const groups = questions.length
      ? questions.map(question => this.createQuestionGroup(question))
      : [this.createQuestionGroup()];

    return this.fb.array(
      groups,
      [
        Validators.minLength(1),
        Validators.maxLength(INPUT_LIMITS.maxQuestions),
        atLeastOneRequiredQuestion
      ]
    );
  }

  private syncAnonymousState(type: string | null | undefined): void {
    const anonymousControl = this.form.get('isAnonymous');
    if (!anonymousControl) return;

    if (type === 'Politic') {
      anonymousControl.setValue(false, { emitEvent: false });
      anonymousControl.disable({ emitEvent: false });
    } else {
      if (!this.isLocked()) {
        anonymousControl.enable({ emitEvent: false });
      }
    }
  }

  addQuestion(): void {
    if (this.isLocked() || this.questions.length >= INPUT_LIMITS.maxQuestions) return;
    this.questions.push(this.createQuestionGroup());
  }

  removeQuestion(index: number): void {
    if (this.isLocked() || this.questions.length <= 1) return;
    this.questions.removeAt(index);
    const currentCollapsed = new Set(this.collapsedQuestions());
    currentCollapsed.delete(index);
    this.collapsedQuestions.set(currentCollapsed);
  }

  addOption(questionIndex: number): void {
    if (this.isLocked()) return;
    const options = this.questionOptions(questionIndex);
    if (options.length >= INPUT_LIMITS.maxOptionsPerQuestion) return;
    options.push(this.createOptionGroup());
  }

  // A Choice question must retain at least two options. The options UI is only
  // ever shown for a Choice question, so this never runs for a FreeText one.
  removeOption(questionIndex: number, optionIndex: number): void {
    if (this.isLocked()) return;
    const options = this.questionOptions(questionIndex);
    if (options.length > 2) {
      options.removeAt(optionIndex);
    }
  }

  onOptionImageSelected(event: Event, questionIndex: number, optionIndex: number): void {
    const control = this.questionOptions(questionIndex).at(optionIndex).get('imageId');
    this.uploadInto(event, control);
  }

  removeOptionImage(questionIndex: number, optionIndex: number): void {
    if (this.isLocked()) return;
    this.questionOptions(questionIndex).at(optionIndex).get('imageId')?.setValue(null);
  }

  onQuestionImageSelected(event: Event, questionIndex: number): void {
    const questionGroup = this.questions.at(questionIndex);
    this.uploadInto(event, questionGroup.get('imageId'), () => questionGroup.markAsDirty());
  }

  removeQuestionImage(questionIndex: number): void {
    if (this.isLocked()) return;
    const questionGroup = this.questions.at(questionIndex);
    questionGroup.get('imageId')?.setValue(null);
    questionGroup.markAsDirty();
  }

  // The picked file is uploaded straight away and only its id is kept in the form, so saving
  // the election sends identifiers rather than image data.
  private uploadInto(event: Event, control: AbstractControl | null, onDone?: () => void): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Clearing the input lets the same file be picked again after a failed upload.
    input.value = '';

    if (this.isLocked() || !file || !control) return;

    const allowedTypes = new Set(['image/png', 'image/jpeg', 'image/webp', 'image/gif']);
    if (!allowedTypes.has(file.type) || file.size > INPUT_LIMITS.imageMaxUploadBytes) {
      this.errorMessageKey.set('elections.optionImageInvalid');
      return;
    }

    this.isUploadingImage.set(true);
    this.errorMessageKey.set(null);
    this.images.upload(file).subscribe({
      next: (result) => {
        control.setValue(result.id);
        onDone?.();
        this.isUploadingImage.set(false);
        // The app is zoneless and the template reads the id off the form rather than a signal,
        // so nothing would repaint the preview without this.
        this.changeDetector.markForCheck();
      },
      error: (response) => {
        this.errorMessageKey.set(
          response?.error?.errorCode === 'imageTooLarge'
            ? 'elections.optionImageTooLarge'
            : 'elections.optionImageUploadFailed'
        );
        this.isUploadingImage.set(false);
        this.changeDetector.markForCheck();
      }
    });
  }

  addInviteEmail(): void {
    if (this.isLocked()) return;
    const normalizedEmail = this.inviteEmailControl.value?.trim().toLowerCase() ?? '';
    if (!normalizedEmail || this.inviteEmailControl.invalid) {
      this.inviteEmailControl.markAsTouched();
      return;
    }

    if (!this.invitedEmails().includes(normalizedEmail)) {
      const emails = [...this.invitedEmails(), normalizedEmail];
      this.invitedEmails.set(emails);
      this.form.controls['invitedEmails'].setValue(emails);
    }

    this.inviteEmailControl.reset('');
  }

  removeInviteEmail(email: string): void {
    if (this.isLocked()) return;
    const emails = this.invitedEmails().filter(item => item !== email);
    this.invitedEmails.set(emails);
    this.form.controls['invitedEmails'].setValue(emails);
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

  // Audience group builder

  /** Build a FormGroup for a single condition {labelId, isExcluded}. */
  private createConditionGroup(labelId: string = '', isExcluded: boolean = false) {
    return this.fb.group({
      labelId: [labelId, Validators.required],
      isExcluded: [isExcluded]
    });
  }

  /** Build a FormArray for one AND-group (a list of conditions). */
  private createGroupArray(
    conditions: { labelId: string; isExcluded: boolean }[] = []
  ) {
    return this.fb.array(conditions.map(c => this.createConditionGroup(c.labelId, c.isExcluded)));
  }

  /** Adds a blank AND-group to the audienceGroups FormArray. */
  addGroup(): void {
    if (this.isLocked()) return;
    this.audienceGroupsArray.push(this.createGroupArray());
  }

  /** Cleans up any user exclusions associated with a label when that label is no longer used in any group. */
  private cleanExclusionsForLabel(labelId: string): void {
    if (!labelId) return;
    const label = this.invitationLabels().find(l => l.id === labelId);
    if (!label || !label.userIds || label.userIds.length === 0) return;

    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];
    const isStillUsed = rawGroups.some(group => group.some(c => c.labelId === labelId));

    if (!isStillUsed) {
      const nextExclusions = new Set(this.excludedGroupUserIds());
      for (const userId of label.userIds) {
        nextExclusions.delete(userId);
      }
      this.excludedGroupUserIds.set(nextExclusions);
    }
  }

  /** Removes the AND-group at the given index. */
  removeGroup(groupIndex: number): void {
    if (this.isLocked()) return;
    const group = this.groupConditions(groupIndex);
    const labelIds = (group.value as { labelId: string }[]).map(c => c.labelId).filter(Boolean);
    this.audienceGroupsArray.removeAt(groupIndex);

    for (const labelId of labelIds) {
      this.cleanExclusionsForLabel(labelId);
    }
  }

  /** Returns the conditions FormArray for a specific group. */
  groupConditions(groupIndex: number): FormArray {
    return this.audienceGroupsArray.at(groupIndex) as FormArray;
  }

  /**
   * Adds a condition to an existing group.
   * If conditionIndex is -1, a new condition is appended; otherwise it replaces the
   * condition at that index (used when the user re-picks a label for an existing slot).
   */
  addConditionToGroup(
    groupIndex: number,
    labelId: string,
    isExcluded: boolean = false,
    conditionIndex: number = -1
  ): void {
    if (this.isLocked()) return;
    const group = this.groupConditions(groupIndex);
    const oldLabelId = conditionIndex >= 0 ? group.at(conditionIndex)?.get('labelId')?.value : null;
    const newCondition = this.createConditionGroup(labelId, isExcluded);

    if (conditionIndex >= 0) {
      group.setControl(conditionIndex, newCondition);
    } else {
      group.push(newCondition);
    }

    this.openConditionPicker.set(null);
    this.conditionSearchControl.reset('');

    if (oldLabelId && oldLabelId !== labelId) {
      this.cleanExclusionsForLabel(oldLabelId);
    }
  }

  /** Removes a single condition from a group. Removes the whole group if it becomes empty. */
  removeCondition(groupIndex: number, conditionIndex: number): void {
    if (this.isLocked()) return;
    const group = this.groupConditions(groupIndex);
    const labelId = group.at(conditionIndex)?.get('labelId')?.value;
    group.removeAt(conditionIndex);

    if (group.length === 0) {
      this.audienceGroupsArray.removeAt(groupIndex);
    }

    if (labelId) {
      this.cleanExclusionsForLabel(labelId);
    }
  }

  /** Toggles the isExcluded flag on a condition chip. */
  toggleConditionExclusion(groupIndex: number, conditionIndex: number): void {
    if (this.isLocked()) return;
    const ctrl = this.groupConditions(groupIndex).at(conditionIndex).get('isExcluded');
    ctrl?.setValue(!ctrl.value);
  }

  openConditionPickerFor(groupIndex: number, conditionIndex: number): void {
    if (this.isLocked()) return;
    const current = this.openConditionPicker();
    if (current?.groupIndex === groupIndex && current?.conditionIndex === conditionIndex) {
      this.openConditionPicker.set(null);
      this.conditionSearchControl.reset('');
    } else {
      this.openConditionPicker.set({ groupIndex, conditionIndex });
      this.conditionSearchControl.reset('');
    }
  }

  isConditionPickerOpen(groupIndex: number, conditionIndex: number): boolean {
    const p = this.openConditionPicker();
    return p?.groupIndex === groupIndex && p?.conditionIndex === conditionIndex;
  }

  filteredConditionLabels(): InvitationLabelDto[] {
    const query = this.conditionSearchControl.value?.trim().toLowerCase() ?? '';
    if (!query) return this.invitationLabels();
    return this.invitationLabels().filter(l =>
      l.name.toLowerCase().includes(query) ||
      l.category?.toLowerCase().includes(query)
    );
  }

  /** Returns a label object by ID (for display in a condition chip). */
  labelById(labelId: string): InvitationLabelDto | undefined {
    return this.invitationLabels().find(l => l.id === labelId);
  }

  /**
   * Returns assigned labels for a given candidate user ID.
   * Splits into the first 2 visible labels, hidden count, and all assigned labels.
   */
  candidateLabels(candidateId: string): { visible: InvitationLabelDto[]; hiddenCount: number; all: InvitationLabelDto[] } {
    const assigned = this.invitationLabels().filter(l => l.userIds?.includes(candidateId));
    const visible = assigned.slice(0, 2);
    const hiddenCount = Math.max(0, assigned.length - 2);
    return { visible, hiddenCount, all: assigned };
  }

  /** Set of candidate user IDs whose labels are currently expanded via click/tap. */
  expandedCandidateLabelIds = signal<Set<string>>(new Set());

  isCandidateLabelsExpanded(candidateId: string): boolean {
    return this.expandedCandidateLabelIds().has(candidateId);
  }

  toggleCandidateLabelsExpanded(candidateId: string, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    this.expandedCandidateLabelIds.update(set => {
      const next = new Set(set);
      if (next.has(candidateId)) {
        next.delete(candidateId);
      } else {
        next.add(candidateId);
      }
      return next;
    });
  }

  onUserItemLabelsClick(candidateId: string, hasOverflow: boolean, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (hasOverflow) {
      this.toggleCandidateLabelsExpanded(candidateId);
    }
  }

  toggleInfoPopover(): void {
    this.showInfoPopover.update(v => !v);
  }

  toggleExcludedUsersDrawer(): void {
    this.showExcludedUsersDrawer.update(v => !v);
  }

  excludedUserCandidates(): InvitationCandidateDto[] {
    const excluded = this.excludedGroupUserIds();
    return this.invitationCandidates().filter(c => excluded.has(c.id));
  }

  restoreExcludedUser(candidateId: string): void {
    if (this.isLocked()) return;
    const nextExclusions = new Set(this.excludedGroupUserIds());
    nextExclusions.delete(candidateId);
    this.excludedGroupUserIds.set(nextExclusions);
  }

  restoreAllExclusions(): void {
    if (this.isLocked()) return;
    this.excludedGroupUserIds.set(new Set());
  }

  toggleShowAllExcludedChips(): void {
    this.showAllExcludedChips.update(v => !v);
  }

  visibleExcludedUserChips(): InvitationCandidateDto[] {
    const list = this.excludedUserCandidates();
    return this.showAllExcludedChips() ? list : list.slice(0, this.CHIP_PREVIEW);
  }

  hiddenExcludedChipCount(): number {
    return Math.max(0, this.excludedUserCandidates().length - this.CHIP_PREVIEW);
  }

  /**
   * Generates a readable natural-language summary of the audience group rules in human language,
   * translated into the active UI locale.
   * Example (EN): "Inviting voters with Cluj and Finance"
   * Example (RO): "Se invită alegătorii cu Cluj și Finance"
   */
  audienceSummaryText(): string {
    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];
    const groupSummaries: string[] = [];

    const andWord = this.translate.instant('elections.andLabel');
    const allVotersWord = this.translate.instant('elections.audienceSummaryAllVoters');

    for (const conditions of rawGroups) {
      if (!conditions || conditions.length === 0) continue;
      const validConditions = conditions.filter(c => c.labelId);
      if (validConditions.length === 0) continue;

      const positiveNames = validConditions
        .filter(c => !c.isExcluded)
        .map(c => this.labelById(c.labelId)?.name ?? c.labelId);

      const excludedNames = validConditions
        .filter(c => c.isExcluded)
        .map(c => this.labelById(c.labelId)?.name ?? c.labelId);

      let text = '';
      if (positiveNames.length === 1) {
        text = positiveNames[0];
      } else if (positiveNames.length > 1) {
        text = positiveNames.slice(0, -1).join(', ') + ` ${andWord} ` + positiveNames[positiveNames.length - 1];
      } else {
        text = allVotersWord;
      }

      if (excludedNames.length === 1) {
        text += ` ` + this.translate.instant('elections.audienceSummaryExcluding', { names: excludedNames[0] });
      } else if (excludedNames.length > 1) {
        const exclStr = excludedNames.slice(0, -1).join(', ') + ` ${andWord} ` + excludedNames[excludedNames.length - 1];
        text += ` ` + this.translate.instant('elections.audienceSummaryExcluding', { names: exclStr });
      }

      groupSummaries.push(text);
    }

    if (groupSummaries.length === 0) return '';
    if (groupSummaries.length === 1) {
      return this.translate.instant('elections.audienceSummarySingleGroup', { group: groupSummaries[0] });
    }
    const orWord = ` ${this.translate.instant('elections.orLabel').toUpperCase()} `;
    return this.translate.instant('elections.audienceSummaryMultiGroup', { groups: groupSummaries.join(`] ${orWord} [`) });
  }


  // AND/OR/NOT audience evaluation (mirrors ExpandAudienceGroupsAsync server-side)

  /**
   * Evaluates all audience groups client-side using the same AND/OR/NOT logic as
   * ExpandAudienceGroupsAsync. Returns the set of user IDs that would be invited
   * via the group rule (not counting manually selected users or emails).
   */
  audienceGroupMemberIds(): string[] {
    const labels = this.invitationLabels();
    const labelMap = new Map<string, Set<string>>();
    for (const label of labels) {
      labelMap.set(label.id, new Set(label.userIds ?? []));
    }

    const result = new Set<string>();
    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];

    for (const conditions of rawGroups) {
      if (!conditions || conditions.length === 0) continue;

      const positive = conditions.filter(c => !c.isExcluded && c.labelId);
      const excluded = conditions.filter(c => c.isExcluded && c.labelId);

      if (positive.length === 0) continue;

      // Intersect user-sets for all positive conditions.
      let candidates: Set<string> | null = null;
      for (const cond of positive) {
        const users = labelMap.get(cond.labelId);
        if (!users) { candidates = new Set(); break; }
        if (candidates === null) {
          candidates = new Set(users);
        } else {
          for (const id of [...candidates]) {
            if (!users.has(id)) candidates.delete(id);
          }
        }
      }

      if (!candidates || candidates.size === 0) continue;

      // Remove users that have any excluded label.
      for (const cond of excluded) {
        const users = labelMap.get(cond.labelId);
        if (users) for (const id of users) candidates.delete(id);
      }

      for (const id of candidates) result.add(id);
    }

    return [...result];
  }

  /**
   * Returns the effective set of user IDs to invite based on:
   * (manual user IDs + group member user IDs) minus individual exclusions (excludedGroupUserIds).
   */
  selectedCandidateIds(): Set<string> {
    const manualIds = new Set<string>(this.form.controls['invitedUserIds'].value as string[] ?? []);
    const groupIds = new Set(this.audienceGroupMemberIds());
    const excluded = this.excludedGroupUserIds();

    const result = new Set<string>();
    for (const id of manualIds) {
      if (!excluded.has(id)) result.add(id);
    }
    for (const id of groupIds) {
      if (!excluded.has(id)) result.add(id);
    }
    return result;
  }

  /**
   * True when the given candidate is invited solely because of the audience groups
   * (not manually selected). Used to show a group-badge on the candidate chip.
   */
  isCandidateFromGroup(candidateId: string): boolean {
    const manualIds = new Set<string>(this.form.controls['invitedUserIds'].value as string[] ?? []);
    if (manualIds.has(candidateId)) return false;
    return this.audienceGroupMemberIds().includes(candidateId);
  }

  /**
   * Total number of unique people that will be invited:
   * union of manually selected registered users + audience group members + free-text emails minus individual exclusions.
   */
  totalUniqueInvitees(): number {
    const emailCount = (this.form.controls['invitedEmails'].value ?? []).length;
    return this.selectedCandidateIds().size + emailCount;
  }

  selectedInvitationCandidates(): InvitationCandidateDto[] {
    const selectedIds = this.selectedCandidateIds();
    return this.invitationCandidates().filter(candidate => selectedIds.has(candidate.id));
  }

  isInvitationCandidateSelected(candidateId: string): boolean {
    return this.selectedCandidateIds().has(candidateId);
  }

  toggleCandidatePicker(): void {
    if (this.isLocked()) return;
    this.candidatePickerOpen.update(open => !open);
    if (!this.candidatePickerOpen()) {
      this.candidateSearchControl.reset('');
    }
  }

  /** Close either picker when the user clicks anywhere outside the respective picker element. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.labelPickerOpen() && this.labelPickerRef && !this.labelPickerRef.nativeElement.contains(event.target)) {
      this.labelPickerOpen.set(false);
      this.labelSearchControl.reset('');
    }
    if (this.candidatePickerOpen() && this.candidatePickerRef && !this.candidatePickerRef.nativeElement.contains(event.target)) {
      this.candidatePickerOpen.set(false);
      this.candidateSearchControl.reset('');
    }
  }

  toggleShowAllCandidateChips(): void {
    this.showAllCandidateChips.update(v => !v);
  }

  /** The slice of selected candidates actually rendered as chips. */
  visibleCandidateChips(): InvitationCandidateDto[] {
    const all = this.selectedInvitationCandidates();
    return this.showAllCandidateChips() ? all : all.slice(0, this.CHIP_PREVIEW);
  }

  hiddenCandidateChipCount(): number {
    const total = this.selectedInvitationCandidates().length;
    return this.showAllCandidateChips() ? 0 : Math.max(0, total - this.CHIP_PREVIEW);
  }

  toggleInvitationCandidate(candidateId: string, selected?: boolean): void {
    if (this.isLocked()) return;
    if (selected === undefined) {
      selected = !this.isInvitationCandidateSelected(candidateId);
    }
    const groupIds = new Set(this.audienceGroupMemberIds());
    const currentManual = this.form.controls['invitedUserIds'].value ?? [];

    if (!selected) {
      if (groupIds.has(candidateId)) {
        const nextExclusions = new Set(this.excludedGroupUserIds());
        nextExclusions.add(candidateId);
        this.excludedGroupUserIds.set(nextExclusions);
      }
      if (currentManual.includes(candidateId)) {
        this.form.controls['invitedUserIds'].setValue(currentManual.filter((id: string) => id !== candidateId));
      }
    } else {
      if (this.excludedGroupUserIds().has(candidateId)) {
        const nextExclusions = new Set(this.excludedGroupUserIds());
        nextExclusions.delete(candidateId);
        this.excludedGroupUserIds.set(nextExclusions);
      }
      if (!groupIds.has(candidateId) && !currentManual.includes(candidateId)) {
        this.form.controls['invitedUserIds'].setValue([...currentManual, candidateId]);
      }
    }
  }

  removeInvitationCandidate(candidateId: string): void {
    if (this.isLocked()) return;
    this.toggleInvitationCandidate(candidateId, false);
  }

  allInvitationCandidatesSelected(): boolean {
    const candidates = this.filteredInvitationCandidates();
    const selectedIds = this.selectedCandidateIds();
    return candidates.length > 0 && candidates.every(candidate => selectedIds.has(candidate.id));
  }

  toggleAllInvitationCandidates(): void {
    if (this.isLocked()) return;
    const visibleCandidates = this.filteredInvitationCandidates();
    const allSelected = this.allInvitationCandidatesSelected();

    for (const candidate of visibleCandidates) {
      this.toggleInvitationCandidate(candidate.id, !allSelected);
    }
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
    // A label is "selected" if it appears in any condition of any group.
    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];
    return rawGroups.some(group => group.some(c => c.labelId === labelId));
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

  /**
   * Evaluates a single audience group (AND/NOT conditions) and returns:
   * - total: total user IDs matched by the group logic
   * - effective: user IDs matched minus individual user exclusions (excludedGroupUserIds)
   */
  groupCoverage(groupIndex: number): { effective: number; total: number } {
    const labels = this.invitationLabels();
    const labelMap = new Map<string, Set<string>>();
    for (const label of labels) {
      labelMap.set(label.id, new Set(label.userIds ?? []));
    }

    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];
    const groupConditions = rawGroups[groupIndex];
    if (!groupConditions || groupConditions.length === 0) {
      return { effective: 0, total: 0 };
    }

    const positive = groupConditions.filter(c => !c.isExcluded && c.labelId);
    const excluded = groupConditions.filter(c => c.isExcluded && c.labelId);
    if (positive.length === 0) return { effective: 0, total: 0 };

    let candidates: Set<string> | null = null;
    for (const cond of positive) {
      const users = labelMap.get(cond.labelId);
      if (!users) { candidates = new Set(); break; }
      if (candidates === null) {
        candidates = new Set(users);
      } else {
        for (const id of [...candidates]) {
          if (!users.has(id)) candidates.delete(id);
        }
      }
    }

    if (!candidates || candidates.size === 0) return { effective: 0, total: 0 };

    for (const cond of excluded) {
      const users = labelMap.get(cond.labelId);
      if (users) for (const id of users) candidates.delete(id);
    }

    const total = candidates.size;
    const excludedSet = this.excludedGroupUserIds();
    let effective = 0;
    for (const id of candidates) {
      if (!excludedSet.has(id)) effective++;
    }

    return { effective, total };
  }



  toggleLabelPicker(): void {
    if (this.isLocked()) return;
    this.labelPickerOpen.update(open => !open);
    if (!this.labelPickerOpen()) {
      this.labelSearchControl.reset('');
    }
  }

  removeInvitationLabel(labelId: string): void {
    if (this.isLocked()) return;
    const arr = this.audienceGroupsArray;
    for (let gi = arr.length - 1; gi >= 0; gi--) {
      const group = arr.at(gi) as FormArray;
      for (let ci = group.length - 1; ci >= 0; ci--) {
        if (group.at(ci).get('labelId')?.value === labelId) {
          group.removeAt(ci);
        }
      }
      if (group.length === 0) arr.removeAt(gi);
    }
    this.cleanExclusionsForLabel(labelId);
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

  private onLabelsLoadedCallbacks: Array<() => void> = [];

  private loadInvitationLabels(): void {
    if (this.invitationLabelsLoaded) {
      const callbacks = [...this.onLabelsLoadedCallbacks];
      this.onLabelsLoadedCallbacks = [];
      callbacks.forEach(cb => cb());
      return;
    }

    if (this.invitationLabelsLoading()) {
      return;
    }

    this.invitationLabelsLoading.set(true);
    this.invitationLabelsErrorKey.set(null);
    this.votingService.getInvitationLabels().subscribe({
      next: (labels) => {
        this.invitationLabels.set(labels);
        this.invitationLabelsLoaded = true;
        this.invitationLabelsLoading.set(false);
        const callbacks = [...this.onLabelsLoadedCallbacks];
        this.onLabelsLoadedCallbacks = [];
        callbacks.forEach(cb => cb());
      },
      error: () => {
        this.invitationLabelsErrorKey.set('elections.inviteLabelsLoadFailed');
        this.invitationLabelsLoading.set(false);
      }
    });
  }

  private runWhenLabelsLoaded(callback: () => void): void {
    if (this.invitationLabelsLoaded) {
      callback();
    } else {
      this.onLabelsLoadedCallbacks.push(callback);
      this.loadInvitationLabels();
    }
  }

  private setupEditInvitations(election: ElectionDto, invitations: ElectionInvitationDto[]): void {
    if (!election.isClosed) {
      return;
    }

    if (invitations.length > 0) {
      this.existingInvitations.set(invitations);

      const existingEmails = invitations
        .filter(inv => !inv.userId)
        .map(inv => inv.email);
      this.invitedEmails.set(existingEmails);
      this.form.controls.invitedEmails.setValue(existingEmails);
    }

    if (election.audienceGroups && election.audienceGroups.length > 0) {
      this.audienceGroupsArray.clear();
      for (const group of election.audienceGroups) {
        this.audienceGroupsArray.push(this.createGroupArray(group.conditions));
      }
    }

    const existingUserIds = invitations
      .filter(inv => !!inv.userId)
      .map(inv => inv.userId!);

    const partitionUsers = () => {
      if (this.audienceGroupsArray.length > 0) {
        const groupMemberIds = new Set(this.audienceGroupMemberIds());
        const existingSet = new Set(existingUserIds);

        // Users in existing invitations that were NOT generated by audience groups -> manual
        const manualUserIds = existingUserIds.filter(id => !groupMemberIds.has(id));
        this.form.controls.invitedUserIds.setValue(manualUserIds);

        // Group members that were NOT in existing invitations -> previously excluded
        const excludedIds = Array.from(groupMemberIds).filter(id => !existingSet.has(id));
        if (excludedIds.length > 0) {
          this.excludedGroupUserIds.set(new Set(excludedIds));
        }
      } else {
        this.form.controls.invitedUserIds.setValue(existingUserIds);
      }
    };

    if (this.audienceGroupsArray.length > 0 || existingUserIds.length > 0) {
      this.runWhenLabelsLoaded(partitionUsers);
    }
  }

  private clearInvitations(): void {
    this.form.controls['invitedUserIds'].setValue([]);
    this.form.controls['invitedEmails'].setValue([]);
    this.audienceGroupsArray.clear();
    this.invitedEmails.set([]);
    this.excludedGroupUserIds.set(new Set());
    this.inviteEmailControl.reset('');
    this.labelSearchControl.reset('');
    this.candidateSearchControl.reset('');
    this.conditionSearchControl.reset('');
    this.labelPickerOpen.set(false);
    this.candidatePickerOpen.set(false);
    this.openConditionPicker.set(null);
  }

  onSubmit(): void {
    if (this.isLocked()) return;
    
    for (let i = 0; i < this.questions.length; i++) {
      const qGroup = this.questions.at(i);
      if (qGroup.get('questionType')?.value === 'FreeText') {
        const optsArray = qGroup.get('options') as FormArray;
        while (optsArray.length > 0) {
          optsArray.removeAt(0);
        }
      }
    }

    this.form.markAllAsTouched();
    if (this.form.hasError('invalidDateRange')) {
      this.errorMessageKey.set('errors.invalidDateRange');
      return;
    }

    if (this.questions.hasError('noRequiredQuestion')) {
      this.errorMessageKey.set('elections.atLeastOneRequiredQuestionError');
      return;
    }

    if (this.isClosedElection && this.inviteEmailControl.value?.trim()) {
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

    // limitRankCount only drives the form - the API reads requiredRankCount, where null
    // already carries "no limit".
    payload.questions = payload.questions.map((question: any) => {
      const { limitRankCount, ...cleanQuestion } = question;

      if (cleanQuestion.questionType === 'FreeText') {
        cleanQuestion.options = [];
      }
      // The API binds Guid?, which rejects the empty string an untouched control holds.
      cleanQuestion.imageId = cleanQuestion.imageId || null;
      cleanQuestion.options = cleanQuestion.options.map((option: any) => ({
        ...option,
        imageId: option.imageId || null
      }));
      return cleanQuestion;
    });

    // Build invitedAudienceGroups from the groups FormArray.
    // The form groups contain raw {labelId, isExcluded} objects; map to the DTO shape.
    const rawGroups = this.audienceGroupsArray.value as { labelId: string; isExcluded: boolean }[][];
    payload.invitedAudienceGroups = rawGroups
      .filter(group => group.some(c => !c.isExcluded && c.labelId))
      .map(group => ({
        conditions: group
          .filter(c => c.labelId)
          .map(c => ({ labelId: c.labelId, isExcluded: c.isExcluded }))
      }));

    // In create mode for a closed election: resolve audience groups + manual user IDs - exclusions
    // into invitedUserIds, while preserving invitedAudienceGroups for the backend ruleset snapshot.
    if (!this.editingElectionId && this.isClosedElection) {
      const finalUserIds = Array.from(this.selectedCandidateIds());
      payload.invitedUserIds = finalUserIds;
    }

    // In edit mode, invitations are managed via dedicated endpoints — clear from PUT body
    if (this.editingElectionId) {
      payload.invitedUserIds = [];
      payload.invitedEmails = [];
      payload.invitedAudienceGroups = [];
    }

    // Ensure the datetime-local values are sent as UTC ISO strings so server comparisons use UTC correctly
    try {
      payload.startsAt = new Date(payload.startsAt).toISOString();
      payload.endsAt = new Date(payload.endsAt).toISOString();
    } catch { /* fall back */ }

    if (this.editingElectionId && this.isClosedElection) {
      // Edit mode for a closed election: PUT the details, then diff invitations
      this.votingService.updateElection(this.editingElectionId, payload).subscribe({
        next: () => {
          this.syncInvitationsOnEdit().then(() => {
            this.isSubmitting.set(false);
            this.router.navigate(['/elections']);
          }).catch(() => {
            this.isSubmitting.set(false);
            this.errorMessageKey.set('elections.invitationSyncFailed');
          });
        },
        error: (err) => {
          this.isSubmitting.set(false);
          const code: string | undefined = err?.error?.errorCode;
          this.errorMessageKey.set(code ? `errors.${code}` : 'elections.saveFailed');
        }
      });
    } else {
      // Create mode or editing a public election
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

  /**
   * In edit mode, computes the diff between current invitation state and the
   * pre-loaded existing invitations, then calls add/remove endpoints as needed.
   */
  private async syncInvitationsOnEdit(): Promise<void> {
    const electionId = this.editingElectionId!;
    const desiredUserIds = Array.from(this.selectedCandidateIds());
    const { toAdd, toRemove } = computeInvitationDiff(
      this.existingInvitations(),
      desiredUserIds,
      this.invitedEmails()
    );

    const promises: Promise<void>[] = [];

    if (toAdd.userIds.length > 0 || toAdd.emails.length > 0) {
      promises.push(
        new Promise<void>((resolve, reject) => {
          this.votingService.inviteToElection(electionId, {
            userIds: toAdd.userIds,
            emails: toAdd.emails
          }).subscribe({ next: () => resolve(), error: reject });
        })
      );
    }

    for (const invitationId of toRemove) {
      promises.push(
        new Promise<void>((resolve, reject) => {
          this.votingService.removeElectionInvitation(electionId, invitationId)
            .subscribe({ next: () => resolve(), error: reject });
        })
      );
    }

    await Promise.all(promises);
  }

  private loadScoringSchemes(): void {
    if (this.scoringSchemesLoading()) return;
    this.scoringSchemesLoading.set(true);
    this.scoringSchemesErrorKey.set(null);
    this.scoringSchemesService.getSchemes().subscribe({
      next: (schemes) => {
        this.scoringSchemes.set(schemes);
        this.scoringSchemesLoading.set(false);
        for (let i = 0; i < this.questions.length; i++) {
          const group = this.questions.at(i);
          if (group.get('questionType')?.value === 'Ranking' && !group.get('scoringSchemeId')?.value) {
            if (schemes.length > 0) {
              group.get('scoringSchemeId')?.setValue(schemes[0].id);
            }
          }
        }
      },
      error: () => {
        this.scoringSchemesErrorKey.set('elections.loadEditFailed');
        this.scoringSchemesLoading.set(false);
      }
    });
  }

  retryScoringSchemes(): void {
    this.loadScoringSchemes();
  }

  openCreateSchemeModal(questionIndex: number): void {
    this.activeQuestionIndexForScheme.set(questionIndex);
  }

  closeCreateSchemeModal(): void {
    this.activeQuestionIndexForScheme.set(null);
  }

  onSchemeCreated(scheme: ScoringSchemeDto): void {
    this.scoringSchemes.update(schemes => [...schemes, scheme]);
    const index = this.activeQuestionIndexForScheme();
    if (index !== null) {
      this.questions.at(index).get('scoringSchemeId')?.setValue(scheme.id);
    }
    this.closeCreateSchemeModal();
  }
}

export function expandAudienceGroups(
  groups: { conditions: { labelId: string; isExcluded: boolean }[] }[],
  labels: { id: string; userIds?: string[] }[]
): string[] {
  const labelMap = new Map<string, Set<string>>();
  for (const label of labels) {
    labelMap.set(label.id, new Set(label.userIds ?? []));
  }

  const result = new Set<string>();
  for (const group of groups) {
    if (!group.conditions || group.conditions.length === 0) continue;
    const positive = group.conditions.filter(c => !c.isExcluded && c.labelId);
    const excluded = group.conditions.filter(c => c.isExcluded && c.labelId);
    if (positive.length === 0) continue;

    let candidates: Set<string> | null = null;
    for (const cond of positive) {
      const users = labelMap.get(cond.labelId);
      if (!users) { candidates = new Set(); break; }
      if (candidates === null) {
        candidates = new Set(users);
      } else {
        for (const id of [...candidates]) {
          if (!users.has(id)) candidates.delete(id);
        }
      }
    }
    if (!candidates || candidates.size === 0) continue;
    for (const cond of excluded) {
      const users = labelMap.get(cond.labelId);
      if (users) for (const id of users) candidates.delete(id);
    }
    for (const id of candidates) result.add(id);
  }
  return [...result];
}

/**
 * Pure function: computes which invitations need to be added/removed when
 * saving a closed election in edit mode. Exported for unit-testing.
 *
 * @param existing   Invitations currently stored on the backend.
 * @param desiredUserIds  Combined list of manually selected users + label members.
 * @param desiredEmails  Free-text email chips (may be mixed-case).
 */
export function computeInvitationDiff(
  existing: ElectionInvitationDto[],
  desiredUserIds: string[],
  desiredEmails: string[]
): { toAdd: { userIds: string[]; emails: string[] }; toRemove: string[] } {
  const wantedUsers = new Set(desiredUserIds);
  const wantedEmails = new Set(desiredEmails.map(e => e.toLowerCase()));

  const existingByUserId = new Map<string, ElectionInvitationDto>();
  const existingByEmail = new Map<string, ElectionInvitationDto>();
  for (const inv of existing) {
    if (inv.userId) existingByUserId.set(inv.userId, inv);
    else existingByEmail.set(inv.email.toLowerCase(), inv);
  }

  const newUserIds = [...wantedUsers].filter(id => !existingByUserId.has(id));
  const newEmails = [...wantedEmails].filter(email => !existingByEmail.has(email));

  const toRemove: string[] = [];
  for (const [userId, inv] of existingByUserId) {
    if (!wantedUsers.has(userId)) toRemove.push(inv.id);
  }
  for (const [email, inv] of existingByEmail) {
    if (!wantedEmails.has(email)) toRemove.push(inv.id);
  }

  return { toAdd: { userIds: newUserIds, emails: newEmails }, toRemove };
}

/**
 * Pure function: the form-field patch to apply when a question's type changes.
 * allowMultipleAnswers and allowOtherOption only make sense for a Choice question
 * (picking among fixed options), so switching to FreeText clears both - otherwise
 * a stale allowMultipleAnswers: true would still show the "multiple answers"
 * badge to voters on a single-textarea question. Exported for unit-testing.
 */
export function freeTextTypeSideEffects(
  type: QuestionType
): { allowMultipleAnswers: boolean; allowOtherOption: boolean } | null {
  return type === 'FreeText' || type === 'Ranking'
    ? { allowMultipleAnswers: false, allowOtherOption: false }
    : null;
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
    return election.questions.map((question, index) => {
      const recoveredOptions = Array.isArray(question.options) && question.options.length > 0
        ? question.options.map(option => ({
          label: option.label ?? '',
          description: option.description ?? '',
          imageId: option.imageId ?? null
        }))
        : index === 0
          ? (election.options ?? []).map(option => ({
            label: option.label ?? '',
            description: option.description ?? '',
            imageId: option.imageId ?? null
          }))
          : [];

      if (question.questionType === 'FreeText' && recoveredOptions.length > 0 && recoveredOptions[0].label === 'FreeText_Image') {
        recoveredImage = recoveredOptions[0].imageDataUrl ?? ''; 
        recoveredOptions = []; 
      }
      

      return {
        text: question.text || (index === 0 ? election.question : '') || '',
        isRequired: question.isRequired ?? true,
        allowMultipleAnswers: question.allowMultipleAnswers ?? false,
        questionType: question.questionType ?? 'Choice',
        allowOtherOption: question.allowOtherOption ?? false,
        requiredRankCount: question.requiredRankCount ?? null,
        scoringSchemeId: question.scoringSchemeId ?? undefined,
        imageId: question.imageId ?? null,
        options: recoveredOptions
      };
    });
  }

  return [{
    text: election.question || election.title || '',
    isRequired: true,
    allowMultipleAnswers: false,
    questionType: 'Choice',
    allowOtherOption: false,
    requiredRankCount: null,
    scoringSchemeId: undefined,
    imageId: null,
    options: (election.options ?? []).map(option => ({
      label: option.label ?? '',
      description: option.description ?? '',
      imageId: option.imageId ?? null
    }))
  }];
}