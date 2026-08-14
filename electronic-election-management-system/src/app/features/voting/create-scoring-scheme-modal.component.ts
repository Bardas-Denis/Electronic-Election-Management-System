import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormArray } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ScoringSchemeDto, CreateScoringSchemeDto } from '../../core/models/scoring-schemes.model';
import { ScoringSchemesService } from '../../core/services/scoring-schemes.service';
import { trimmedRequired } from '../../core/validators/input.validators';

@Component({
  selector: 'app-create-scoring-scheme-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './create-scoring-scheme-modal.component.html',
  styleUrl: './create-scoring-scheme-modal.component.scss'
})
export class CreateScoringSchemeModalComponent {
  @Input({ required: true }) existingSchemes!: ScoringSchemeDto[];
  @Output() created = new EventEmitter<ScoringSchemeDto>();
  @Output() cancelled = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private scoringSchemesService = inject(ScoringSchemesService);

  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);

  form = this.fb.group({
    name: ['', [trimmedRequired, Validators.maxLength(100)]],
    startFrom: [null as string | null],
    points: this.fb.array([this.fb.control(0, [Validators.required, Validators.min(0)])])
  });

  get pointsArray(): FormArray {
    return this.form.get('points') as FormArray;
  }

  constructor() {
    this.form.get('startFrom')?.valueChanges.subscribe(schemeId => {
      if (schemeId) {
        const scheme = this.existingSchemes.find(s => s.id === schemeId);
        if (scheme) {
          if (scheme.isLinear) {
             this.setPoints(Array.from({length: 10}, (_, i) => 10 - i));
          } else {
             this.setPoints(scheme.points);
          }
        }
      }
    });
  }

  setPoints(points: number[]) {
    this.pointsArray.clear();
    for (const pt of points) {
      this.pointsArray.push(this.fb.control(pt, [Validators.required, Validators.min(0)]));
    }
  }

  addRank() {
    this.pointsArray.push(this.fb.control(0, [Validators.required, Validators.min(0)]));
  }

  removeRank(index: number) {
    if (this.pointsArray.length > 1) {
      this.pointsArray.removeAt(index);
    }
  }

  onCancel() {
    this.cancelled.emit();
  }

  onConfirm() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const dto: CreateScoringSchemeDto = {
      name: this.form.getRawValue().name!,
      points: this.form.getRawValue().points as number[]
    };

    this.scoringSchemesService.createScheme(dto).subscribe({
      next: (scheme) => {
        this.isSubmitting.set(false);
        this.created.emit(scheme);
      },
      error: () => {
        this.isSubmitting.set(false);
        this.errorMessage.set('elections.createScoringSchemeFailed');
      }
    });
  }
}
