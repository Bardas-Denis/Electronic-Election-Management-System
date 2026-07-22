import { Component, EventEmitter, Input, Output, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ElectionType, VoterDeclarationDto } from '../../core/models/voting.model';
import { parseCnp } from '../../core/utils/cnp.util';

// Validates that the CNP typed in actually decodes to a real CNP (checksum + calendar date).
function validCnp(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string;
  if (!value) return null;
  return parseCnp(value) ? null : { invalidCnp: true };
}

/**
 * Popup shown right before a vote is submitted in a non-anonymous election. Which fields it
 * shows depends on the election's type:
 *  - Politic: CNP (auto-derives vârstă/sex/județ), nume complet, domiciliu
 *  - Comercial: sex opțional, ID angajat (opțional), departament (opțional), funcție, companie, email muncă
 * Never shown at all for anonymous elections - see CastVoteComponent.
 */
@Component({
  selector: 'app-voter-declaration-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './voter-declaration-modal.component.html',
  styleUrl: './voter-declaration-modal.component.scss'
})
export class VoterDeclarationModalComponent {
  @Input({ required: true }) electionType!: ElectionType;
  @Output() confirmed = new EventEmitter<VoterDeclarationDto>();
  @Output() cancelled = new EventEmitter<void>();

  private fb = inject(FormBuilder);

  isPolitic = computed(() => this.electionType === 'Politic');

  politicForm = this.fb.group({
    cnp: ['', [Validators.required, Validators.pattern(/^\d{13}$/), validCnp]],
    fullName: ['', [Validators.required, Validators.minLength(3)]],
    domiciliuJudet: ['', [Validators.required]],
    domiciliuAdresa: ['', [Validators.required, Validators.minLength(3)]],
    domiciliuLocalitate: [''],
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

  get cnpCtrl() { return this.politicForm.get('cnp')!; }
  get fullNameCtrl() { return this.politicForm.get('fullName')!; }
  get judetCtrl() { return this.politicForm.get('domiciliuJudet')!; }
  get adresaCtrl() { return this.politicForm.get('domiciliuAdresa')!; }
  get genderCtrl() { return this.comercialForm.get('gender')!; }
  get workEmailCtrl() { return this.comercialForm.get('workEmail')!; }

  // Live preview derived from the CNP as the person types - never sent to the backend as-is,
  // the backend re-derives it independently from the CNP.
  cnpPreview = computed(() => parseCnp(this.cnpValue()));

  constructor() {
    this.cnpValue.set(this.cnpCtrl.value ?? '');
    this.cnpCtrl.valueChanges.subscribe(value => this.cnpValue.set(value ?? ''));

    effect(() => {
      const preview = this.cnpPreview();
      if (preview && !this.judetCtrl.dirty) {
        this.judetCtrl.setValue(preview.countyName);
      }
    });
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  onConfirm(): void {
    if (this.isPolitic()) {
      this.politicForm.markAllAsTouched();
      if (this.politicForm.invalid) return;

      this.cnpValue.set(this.cnpCtrl.value ?? '');
      const { cnp, fullName, domiciliuJudet, domiciliuAdresa } = this.politicForm.getRawValue();
      this.confirmed.emit({
        cnp: cnp!,
        fullName: fullName!,
        domiciliuJudet: domiciliuJudet!,
        domiciliuAdresa: domiciliuAdresa!
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
