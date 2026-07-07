import { Component, OnInit, signal } from '@angular/core';
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
  templateUrl: './create-election.component.html'
})
export class CreateElectionComponent implements OnInit {
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
    optionLabels: this.fb.array([
      this.fb.control('', Validators.required),
      this.fb.control('', Validators.required)
    ])
  });

  constructor(
    private fb: FormBuilder,
    private votingService: VotingService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.editingElectionId = this.route.snapshot.paramMap.get('id');
    if (!this.editingElectionId) {
      return;
    }

    this.isEditMode.set(true);
    this.isLoading.set(true);

    this.votingService.getElectionById(this.editingElectionId).subscribe({
      next: (election) => {
        // reconstruim FormArray-ul de optiuni cu numarul exact de optiuni existente
        this.optionLabels.clear();
        election.options.forEach((option) =>
          this.optionLabels.push(this.fb.control(option.label, Validators.required))
        );

        this.form.patchValue({
          title: election.title,
          description: election.description ?? '',
          type: election.type,
          isAnonymous: election.isAnonymous,
          startsAt: toDatetimeLocal(election.startsAt),
          endsAt: toDatetimeLocal(election.endsAt)
        });

        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Nu am putut incarca alegerea pentru editare.');
        this.isLoading.set(false);
      }
    });
  }

  get optionLabels(): FormArray {
    return this.form.get('optionLabels') as FormArray;
  }

  addOption(): void {
    this.optionLabels.push(this.fb.control('', Validators.required));
  }

  removeOption(index: number): void {
    if (this.optionLabels.length > 2) {
      this.optionLabels.removeAt(index);
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
        this.router.navigate(['/elections', result.id]);
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
