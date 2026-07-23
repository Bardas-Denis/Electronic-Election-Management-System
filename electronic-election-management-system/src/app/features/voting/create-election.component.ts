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
    question: ['', Validators.required],
    type: ['Politic', Validators.required],
    isAnonymous: [true],
    // Kept in the form payload even before the invitation UI is added, so editing
    // an existing closed election never accidentally makes it public.
    isClosed: [false],
    invitedUserIds: this.fb.control<string[]>([]),
    invitedEmails: this.fb.control<string[]>([]),
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    options: this.fb.array([
      this.fb.group({
        label: ['', Validators.required],
        description: ['']
      }),
      this.fb.group({
        label: ['', Validators.required],
        description: ['']
      })
    ])
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
        this.options.clear();
        const fetchedOptions = election.options ?? [];
        fetchedOptions.forEach((option) =>
          this.options.push(this.fb.group({
            label: [option.label ?? '', Validators.required],
            description: [option.description ?? '']
          }))
        );
        // daca serverul nu a intors nicio optiune (nu ar trebui sa se intample), pastram
        // macar doua randuri goale ca sa nu ramana formularul fara campuri de optiuni
        if (fetchedOptions.length === 0) {
          this.addOption();
          this.addOption();
        }

        this.form.patchValue({
          title: election.title,
          description: election.description ?? '',
          question: election.question ?? '',
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

  get options(): FormArray {
    return this.form.get('options') as FormArray;
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

  addOption(): void {
    this.options.push(this.fb.group({
      label: ['', Validators.required],
      description: ['']
    }));
  }

  // minim 2 optiuni obligatorii
  removeOption(index: number): void {
    if (this.options.length > 2) {
      this.options.removeAt(index);
    }
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
