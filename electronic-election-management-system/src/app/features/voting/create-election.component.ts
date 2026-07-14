import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';

// Componenta e folosita atat pentru creare (ruta /elections/new)
// cat si pentru editare (ruta /elections/:id/edit) - CRUD complet cerut in Etapa 2.
@Component({
  selector: 'app-create-election',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
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
  errorMessage = signal<string | null>(null);

  // daca exista, suntem in mod editare
  private editingElectionId: string | null = null;
  isEditMode = signal(false);

  form = this.fb.group({
    title: ['', Validators.required],
    description: [''],
    type: ['Politic', Validators.required],
    isAnonymous: [true],
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

  ngOnInit(): void {
    this.form.get('type')?.valueChanges.subscribe((type) => {
      this.syncAnonymousState(type);
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
        // reconstruim FormArray-ul de optiuni cu numarul exact de optiuni existente
        this.options.clear();
        election.options.forEach((option) =>
          this.options.push(this.fb.group({
            label: [option.label, Validators.required],
            description: [option.description ?? '']
          }))
        );

        this.form.patchValue({
          title: election.title,
          description: election.description ?? '',
          type: election.type,
          isAnonymous: election.isAnonymous,
          startsAt: toDatetimeLocal(election.startsAt),
          endsAt: toDatetimeLocal(election.endsAt)
        });

        this.syncAnonymousState(this.form.get('type')?.value);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Nu am putut incarca alegerea pentru editare.');
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

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const payload = this.form.getRawValue() as any;

    const request$ = this.editingElectionId
      ? this.votingService.updateElection(this.editingElectionId, payload)
      : this.votingService.createElection(payload);

    request$.subscribe({
      next: (result) => {
        this.isSubmitting.set(false);
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(
          err?.error?.message ?? (this.editingElectionId ? 'Nu am putut salva modificarile.' : 'Nu am putut crea alegerea.')
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