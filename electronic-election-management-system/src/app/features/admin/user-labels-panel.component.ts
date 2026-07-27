import { Component, Input, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LabelService } from '../../core/services/label.service';
import { Label, UserLabel } from '../../core/models/label.model';

/**
 * Displays a chip-based label editor for a specific user.
 * Used inside the admin users-management page (expanded row panel).
 * Loads labels lazily when the panel is first opened.
 */
@Component({
  selector: 'app-user-labels-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './user-labels-panel.component.html',
  styleUrl: './user-labels-panel.component.scss'
})
export class UserLabelsPanelComponent implements OnInit {
  @Input({ required: true }) userId!: string;

  private labelService = inject(LabelService);

  // The user's currently assigned labels
  userLabels = signal<UserLabel[]>([]);
  // All available labels (for the add dropdown)
  allLabels = signal<Label[]>([]);

  isLoading = signal(true);
  isAdding = signal(false);
  errorKey = signal<string | null>(null);

  // The label id selected in the add-label dropdown
  selectedLabelId = signal<string>('');

  /** Labels not yet assigned to this user — drives the add dropdown. */
  availableLabels = computed(() => {
    const assigned = new Set(this.userLabels().map(ul => ul.labelId));
    return this.allLabels().filter(l => !assigned.has(l.id));
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.errorKey.set(null);

    // Load both in parallel using a simple counter
    let pending = 2;
    const done = () => { if (--pending === 0) this.isLoading.set(false); };

    this.labelService.getUserLabels(this.userId).subscribe({
      next: (data) => { this.userLabels.set(data); done(); },
      error: () => { this.errorKey.set('labels.loadFailed'); done(); }
    });

    this.labelService.getAllLabels().subscribe({
      next: (data) => { this.allLabels.set(data); done(); },
      error: () => { done(); }
    });
  }

  addLabel(): void {
    const labelId = this.selectedLabelId();
    if (!labelId) return;

    this.isAdding.set(true);
    this.errorKey.set(null);

    this.labelService.assignLabelsToUser(this.userId, { labelIds: [labelId] }).subscribe({
      next: (updated) => {
        this.userLabels.set(updated);
        this.selectedLabelId.set('');
        this.isAdding.set(false);
      },
      error: (err) => {
        this.isAdding.set(false);
        const code: string | undefined = err?.error?.errorCode;
        this.errorKey.set(code ? `errors.${code}` : 'labels.assignFailed');
      }
    });
  }

  removeLabel(userLabel: UserLabel): void {
    this.labelService.removeLabelFromUser(this.userId, userLabel.labelId).subscribe({
      next: () => {
        this.userLabels.update(list => list.filter(ul => ul.labelId !== userLabel.labelId));
      },
      error: (err) => {
        const code: string | undefined = err?.error?.errorCode;
        this.errorKey.set(code ? `errors.${code}` : 'labels.removeFailed');
      }
    });
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('ro-RO', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }
}
