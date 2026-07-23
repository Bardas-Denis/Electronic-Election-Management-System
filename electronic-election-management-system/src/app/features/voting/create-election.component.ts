import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { InvitationCandidateDto } from '../../core/models/voting.model';

// Componenta e folosita atat pentru creare (ruta /elections/new)
// cat si pentru editare (ruta /elections/:id/edit) - CRUD complet cerut in Etapa 2.
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
  // true cand alegerea are deja cel putin un vot inregistrat - editarea e blocata
  isLocked = signal(false);
  invitationCandidates = signal<InvitationCandidateDto[]>([]);
  invitationCandidatesLoading = signal(false);
  invitationCandidatesErrorKey = signal<string | null>(null);
  invitedEmails = signal<string[]>([]);
  inviteEmailControl = this.fb.control('', Validators.email);
  candidateSearchControl = this.fb.control('');
  candidatePickerOpen = signal(false);
  private invitationCandidatesLoaded = false;

  // daca exista, suntem in mod editare
  private editingElectionId: string | null = null;
  isEditMode = signal(false);

  form = this.fb.group({
    title: ['', Validators.required],
    description: [''],
    type: ['Politic', Validators.required],
    isAnonymous: [true],
    // Kept in the form payload even before the invitation UI is added, so editing
    // an existing closed election never accidentally makes it public.
    isClosed: [false],
    invitedUserIds: this.fb.control<string[]>([]),
    invitedEmails: this.fb.control<string[]>([]),
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    questions: this.fb.array([this.createQuestionGroup()])
  });

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
        // reconstruim FormArray-ul de optiuni cu numarul exact de optiuni existente primite de la server
        this.questions.clear();
        const fetchedQuestions = election.questions?.length
          ? election.questions
          : [{ id: '', text: election.question ?? '', displayOrder: 0, options: election.options ?? [] }];
        fetchedQuestions.forEach(question => {
          const group = this.createQuestionGroup();
          group.patchValue({ text: question.text });
          const options = group.get('options') as FormArray;
          options.clear();
          question.options.forEach(option => options.push(this.createOptionGroup(option)));
          while (options.length < 2) options.push(this.createOptionGroup());
          this.questions.push(group);
        });

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

        // Odata ce a fost inregistrat cel putin un vot, alegerea devine needitabila
        // (backend-ul respinge oricum PUT-ul; aici blocam si UI-ul din start).
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
      label: [option?.label ?? '', Validators.required],
      description: [option?.description ?? ''],
      imageDataUrl: [option?.imageDataUrl ?? '']
    });
  }

  private createQuestionGroup() {
    return this.fb.group({
      text: ['', Validators.required],
      options: this.fb.array([this.createOptionGroup(), this.createOptionGroup()])
    });
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
    this.questions.push(this.createQuestionGroup());
  }

  removeQuestion(index: number): void {
    if (this.questions.length > 1) this.questions.removeAt(index);
  }

  addOption(questionIndex: number): void {
    this.questionOptions(questionIndex).push(this.createOptionGroup());
  }

  // minim 2 optiuni obligatorii
  removeOption(questionIndex: number, optionIndex: number): void {
    const options = this.questionOptions(questionIndex);
    if (options.length > 2) {
      options.removeAt(optionIndex);
    }
  }

  onOptionImageSelected(event: Event, questionIndex: number, optionIndex: number): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 2_000_000) {
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

  private clearInvitations(): void {
    this.form.controls.invitedUserIds.setValue([]);
    this.form.controls.invitedEmails.setValue([]);
    this.invitedEmails.set([]);
    this.inviteEmailControl.reset('');
    this.candidateSearchControl.reset('');
    this.candidatePickerOpen.set(false);
  }

  onSubmit(): void {
    if (this.isClosedElection && !this.editingElectionId && this.inviteEmailControl.value?.trim()) {
      if (this.inviteEmailControl.invalid) {
        this.inviteEmailControl.markAsTouched();
        return;
      }
      this.addInviteEmail();
    }

    if (this.form.invalid || this.isLocked()) return;

    this.isSubmitting.set(true);
    this.errorMessageKey.set(null);

    const payload = this.form.getRawValue() as any;
    payload.question = payload.questions[0].text;
    payload.options = payload.questions[0].options;
    if (!payload.isClosed || this.editingElectionId) {
      // Existing invitation membership is managed by the invitation endpoints.
      payload.invitedUserIds = [];
      payload.invitedEmails = [];
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

// Convertoare intre formatul ISO al backend-ului si formatul asteptat de <input type="datetime-local">
function toDatetimeLocal(isoDate: string): string {
  const date = new Date(isoDate);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
