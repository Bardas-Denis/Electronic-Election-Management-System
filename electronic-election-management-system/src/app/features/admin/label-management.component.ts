import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LabelService } from '../../core/services/label.service';
import { Label, CreateLabelRequest } from '../../core/models/label.model';

@Component({
  selector: 'app-label-management',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './label-management.component.html',
  styleUrl: './label-management.component.scss'
})
export class LabelManagementComponent implements OnInit {
  private labelService = inject(LabelService);

  labels = signal<Label[]>([]);
  isLoading = signal(true);
  isSaving = signal(false);
  errorKey = signal<string | null>(null);
  successKey = signal<string | null>(null);

  // Inline create form state
  showCreateForm = signal(false);
  newName = signal('');
  newCategory = signal('');

  ngOnInit(): void {
    this.loadLabels();
  }

  loadLabels(): void {
    this.isLoading.set(true);
    this.errorKey.set(null);

    this.labelService.getAllLabels().subscribe({
      next: (data) => {
        this.labels.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorKey.set('labels.loadFailed');
        this.isLoading.set(false);
      }
    });
  }

  openCreateForm(): void {
    this.showCreateForm.set(true);
    this.newName.set('');
    this.newCategory.set('');
    this.errorKey.set(null);
    this.successKey.set(null);
  }

  cancelCreate(): void {
    this.showCreateForm.set(false);
    this.errorKey.set(null);
  }

  createLabel(): void {
    const name = this.newName().trim();
    if (!name) {
      this.errorKey.set('labels.nameRequired');
      return;
    }

    const request: CreateLabelRequest = {
      name,
      category: this.newCategory().trim() || null
    };

    this.isSaving.set(true);
    this.errorKey.set(null);
    this.successKey.set(null);

    this.labelService.createLabel(request).subscribe({
      next: (created) => {
        this.labels.update(list => [...list, created].sort((a, b) => a.name.localeCompare(b.name)));
        this.isSaving.set(false);
        this.showCreateForm.set(false);
        this.successKey.set('labels.createSuccess');
        setTimeout(() => this.successKey.set(null), 3000);
      },
      error: (err) => {
        this.isSaving.set(false);
        const code: string | undefined = err?.error?.errorCode;
        this.errorKey.set(code ? `errors.${code}` : 'labels.createFailed');
      }
    });
  }

  deleteLabel(label: Label): void {
    if (!confirm(`Delete label "${label.name}"? This will remove it from all users.`)) {
      return;
    }

    this.labelService.deleteLabel(label.id).subscribe({
      next: () => {
        this.labels.update(list => list.filter(l => l.id !== label.id));
      },
      error: (err) => {
        const code: string | undefined = err?.error?.errorCode;
        this.errorKey.set(code ? `errors.${code}` : 'labels.deleteFailed');
      }
    });
  }
}
