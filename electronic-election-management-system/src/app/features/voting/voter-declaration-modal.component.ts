import { Component, EventEmitter, Input, OnInit, Output, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ElectionType, VoterDeclarationDto } from '../../core/models/voting.model';
import { PersonalDetailsDto } from '../../core/models/user-details.model';
import { parseCnp } from '../../core/utils/cnp.util';
import { UserDetailsService } from '../../core/services/user-details.service';

// Validates that the CNP typed in actually decodes to a real CNP (checksum + calendar date).
function validCnp(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string;
  if (!value) return null;
  return parseCnp(value) ? null : { invalidCnp: true };
}

/**
 * Popup shown right before a vote is submitted in a non-anonymous election. Which fields it
 * shows depends on the election's type:
 *  - Politic: CNP (auto-derives age/sex/county), full name, residence
 *  - Comercial: sex optional, ID employee (optional), department (optional), job title (optional), company (optional), work email (optional)
 * Never shown at all for anonymous elections - see CastVoteComponent.
 */
@Component({
  selector: 'app-voter-declaration-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './voter-declaration-modal.component.html',
  styleUrl: './voter-declaration-modal.component.scss'
})
export class VoterDeclarationModalComponent implements OnInit {
  @Input({ required: true }) electionType!: ElectionType;
  @Output() confirmed = new EventEmitter<VoterDeclarationDto>();
  @Output() cancelled = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private userDetailsService = inject(UserDetailsService);

  isPolitic = computed(() => this.electionType === 'Politic');

  politicForm = this.fb.group({
    cnp: ['', [Validators.required, Validators.pattern(/^\d{13}$/), validCnp]],
    fullName: ['', [Validators.required, Validators.minLength(3)]],
    residenceCounty: ['', [Validators.required]],
    residenceAddress: ['', [Validators.required, Validators.minLength(3)]],
    residenceCity: [''],
    citizenship: ['']
  });

  comercialForm = this.fb.group({
    gender: [''],
    fullName: [''],
    workEmail: ['', [Validators.email]],
    department: [''],
    jobTitle: [''],
    company: [''],
    employeeId: ['']
  });

  cnpValue = signal('');
  /** True once ngOnInit confirms the user has a non-empty residenceCounty saved in their profile. */
  private hasProfileCounty = signal(false);

  /** Snapshot of profile values as loaded from the API (null = not yet loaded). */
  private profileSnapshot = signal<PersonalDetailsDto | null>(null);

  /** Live mirror of politicForm values, updated on every valueChanges event. */
  private politicFormValues = signal<Partial<PersonalDetailsDto>>({});
  /** Live mirror of comercialForm values, updated on every valueChanges event. */
  private comercialFormValues = signal<Partial<PersonalDetailsDto>>({});

  isSavingProfile = signal(false);
  saveProfileSuccess = signal(false);

  /**
   * True when the currently active form contains at least one field that
   * differs from the saved profile snapshot — this drives the Save button.
   */
  hasProfileChanges = computed(() => {
    const snap = this.profileSnapshot();
    if (!snap) return false;

    if (this.isPolitic()) {
      const v = this.politicFormValues();
      return (
        (v.cnp ?? '') !== (snap.cnp ?? '') ||
        (v.fullName ?? '') !== (snap.fullName ?? '') ||
        (v.residenceCounty ?? '') !== (snap.residenceCounty ?? '') ||
        (v.residenceAddress ?? '') !== (snap.residenceAddress ?? '') ||
        (v.residenceCity ?? '') !== (snap.residenceCity ?? '') ||
        (v.citizenship ?? '') !== (snap.citizenship ?? '')
      );
    } else {
      const v = this.comercialFormValues();
      return (
        (v.gender ?? '') !== (snap.gender ?? '') ||
        (v.fullName ?? '') !== (snap.fullName ?? '') ||
        (v.workEmail ?? '') !== (snap.workEmail ?? '') ||
        (v.department ?? '') !== (snap.department ?? '') ||
        (v.jobTitle ?? '') !== (snap.jobTitle ?? '') ||
        (v.company ?? '') !== (snap.company ?? '') ||
        (v.employeeId ?? '') !== (snap.employeeId ?? '')
      );
    }
  });

  get cnpCtrl() { return this.politicForm.get('cnp')!; }
  get fullNameCtrl() { return this.politicForm.get('fullName')!; }
  get residenceCountyCtrl() { return this.politicForm.get('residenceCounty')!; }
  get residenceAddressCtrl() { return this.politicForm.get('residenceAddress')!; }
  get genderCtrl() { return this.comercialForm.get('gender')!; }
  get workEmailCtrl() { return this.comercialForm.get('workEmail')!; }

  // Live preview derived from the CNP as the person types - never sent to the backend as-is,
  // the backend re-derives it independently from the CNP.
  cnpPreview = computed(() => parseCnp(this.cnpValue()));

  constructor() {
    this.cnpValue.set(this.cnpCtrl.value ?? '');
    this.cnpCtrl.valueChanges.subscribe(value => this.cnpValue.set(value ?? ''));

    // Keep live mirrors in sync so hasProfileChanges reacts to every keystroke.
    this.politicForm.valueChanges.subscribe(v => this.politicFormValues.set(v as Partial<PersonalDetailsDto>));
    this.comercialForm.valueChanges.subscribe(v => this.comercialFormValues.set(v as Partial<PersonalDetailsDto>));

    effect(() => {
      const preview = this.cnpPreview();
      // Only auto-fill from CNP if the profile did not supply a residenceCounty
      // AND the user hasn't manually typed a value in the field yet.
      if (preview && !this.hasProfileCounty() && !this.residenceCountyCtrl.dirty) {
        this.residenceCountyCtrl.setValue(preview.countyName);
      }
    });
  }

  ngOnInit(): void {
    this.userDetailsService.getMyDetails().subscribe({
      next: (dto) => {
        if (!dto) return;

        // Prefill the Politic form with saved personal details.
        this.politicForm.patchValue({
          cnp: dto.cnp ?? '',
          fullName: dto.fullName ?? '',
          residenceCounty: dto.residenceCounty ?? '',
          residenceAddress: dto.residenceAddress ?? '',
          residenceCity: dto.residenceCity ?? '',
          citizenship: dto.citizenship ?? ''
        });

        // If the profile already has a county, mark it so the CNP effect doesn't overwrite it.
        if (dto.residenceCounty) {
          this.hasProfileCounty.set(true);
        }

        // Sync the cnpValue signal so the live CNP preview works immediately.
        this.cnpValue.set(dto.cnp ?? '');

        // Prefill the Comercial form with saved professional details.
        this.comercialForm.patchValue({
          gender: dto.gender ?? '',
          fullName: dto.fullName ?? '',
          workEmail: dto.workEmail ?? '',
          department: dto.department ?? '',
          jobTitle: dto.jobTitle ?? '',
          company: dto.company ?? '',
          employeeId: dto.employeeId ?? ''
        });

        // Store the baseline snapshot used to detect unsaved changes.
        this.profileSnapshot.set({ ...dto });

        // Seed the live mirrors with the freshly patched values.
        this.politicFormValues.set(this.politicForm.value as Partial<PersonalDetailsDto>);
        this.comercialFormValues.set(this.comercialForm.value as Partial<PersonalDetailsDto>);
      },
      // If no details are saved yet (204 No Content), silently ignore.
      error: () => { }
    });
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  onSaveToProfile(): void {
    if (this.isSavingProfile()) return;
    this.isSavingProfile.set(true);
    this.saveProfileSuccess.set(false);

    // Build a merged DTO from both forms plus any existing snapshot fields.
    const snap = this.profileSnapshot() ?? {} as PersonalDetailsDto;
    const politic = this.politicForm.getRawValue();
    const comercial = this.comercialForm.getRawValue();

    const merged: PersonalDetailsDto = {
      cnp: politic.cnp ?? snap.cnp ?? '',
      fullName: politic.fullName ?? comercial.fullName ?? snap.fullName ?? '',
      residenceCounty: politic.residenceCounty ?? snap.residenceCounty ?? '',
      residenceAddress: politic.residenceAddress ?? snap.residenceAddress ?? '',
      residenceCity: politic.residenceCity ?? snap.residenceCity ?? '',
      citizenship: politic.citizenship ?? snap.citizenship ?? '',
      gender: comercial.gender ?? snap.gender ?? '',
      workEmail: comercial.workEmail ?? snap.workEmail ?? '',
      department: comercial.department ?? snap.department ?? '',
      jobTitle: comercial.jobTitle ?? snap.jobTitle ?? '',
      company: comercial.company ?? snap.company ?? '',
      employeeId: comercial.employeeId ?? snap.employeeId ?? ''
    };

    this.userDetailsService.saveMyDetails(merged).subscribe({
      next: (saved) => {
        this.profileSnapshot.set({ ...saved });
        this.isSavingProfile.set(false);
        this.saveProfileSuccess.set(true);
        setTimeout(() => this.saveProfileSuccess.set(false), 3000);
      },
      error: () => {
        this.isSavingProfile.set(false);
      }
    });
  }

  onConfirm(): void {
    if (this.isPolitic()) {
      this.politicForm.markAllAsTouched();
      if (this.politicForm.invalid) return;

      this.cnpValue.set(this.cnpCtrl.value ?? '');
      const { cnp, fullName, residenceCounty, residenceAddress } = this.politicForm.getRawValue();
      this.confirmed.emit({
        cnp: cnp!,
        fullName: fullName!,
        residenceCounty: residenceCounty!,
        residenceAddress: residenceAddress!
      });
      return;
    }

    this.comercialForm.markAllAsTouched();
    if (this.comercialForm.invalid) return;

    const { gender, fullName, workEmail, department, jobTitle, company, employeeId } = this.comercialForm.getRawValue();
    this.confirmed.emit({
      gender: gender || undefined,
      fullName: fullName || undefined,
      workEmail: workEmail || undefined,
      department: department || undefined,
      jobTitle: jobTitle || undefined,
      company: company || undefined,
      employeeId: employeeId || undefined
    });
  }
}
