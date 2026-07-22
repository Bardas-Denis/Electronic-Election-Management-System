import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto } from '../../core/models/voting.model';

// Management view: shows only the elections owned by the current user.
// Accessible to Admin and ElectionManager via electionManagerGuard.
// Edit/Delete actions are safe without a per-item ownership check because
// the backend endpoint (/elections/mine) already filters by CreatedByUserId.
@Component({
  selector: 'app-my-elections',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe],
  templateUrl: './my-elections.component.html',
  styleUrl: './my-elections.component.scss'
})
export class MyElectionsComponent implements OnInit {
  private votingService = inject(VotingService);
  private translateService = inject(TranslateService);

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);
  /** Translation key for inline errors — resolved via | translate in the template. */
  errorMessageKey = signal<string | null>(null);

  // alegerile active se afiseaza normal; cele expirate stau ascunse sub un buton, mai jos
  activeElections = computed(() => this.elections().filter((e) => !e.isExpired));
  expiredElections = computed(() => this.elections().filter((e) => e.isExpired));
  showExpired = signal(false);

  toggleExpired(): void {
    this.showExpired.set(!this.showExpired());
  }

  ngOnInit(): void {
    this.loadElections();
  }

  loadElections(): void {
    this.isLoading.set(true);
    this.errorMessageKey.set(null);
    this.votingService.getMyElections().subscribe({
      next: (data) => {
        this.elections.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessageKey.set('elections.loadFailed');
        this.isLoading.set(false);
      }
    });
  }

  deleteElection(id: string, title: string): void {
    const msg = this.translateService.instant('elections.confirmDelete', { title });
    if (!confirm(msg)) {
      return;
    }

    this.votingService.deleteElection(id).subscribe({
      next: () => this.loadElections(),
      error: (err) => {
        const code: string | undefined = err?.error?.errorCode;
        const key = code ? `errors.${code}` : 'elections.deleteFailed';
        alert(this.translateService.instant(key));
      }
    });
  }
}
