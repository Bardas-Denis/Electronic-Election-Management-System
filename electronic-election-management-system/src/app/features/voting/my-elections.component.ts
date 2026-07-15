import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';
import { ElectionDto } from '../../core/models/voting.model';

// Management view: shows only the elections owned by the current user.
// Accessible to Admin and ElectionManager via electionManagerGuard.
// Edit/Delete actions are safe without a per-item ownership check because
// the backend endpoint (/elections/mine) already filters by CreatedByUserId.
@Component({
  selector: 'app-my-elections',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-elections.component.html',
  styleUrl: './my-elections.component.scss'
})
export class MyElectionsComponent implements OnInit {
  private votingService = inject(VotingService);

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);
  errorMessage = signal<string | null>(null);

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
    this.errorMessage.set(null);
    this.votingService.getMyElections().subscribe({
      next: (data) => {
        this.elections.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Nu am putut incarca alegerile tale.');
        this.isLoading.set(false);
      }
    });
  }

  deleteElection(id: string, title: string): void {
    if (!confirm(`Stergi alegerea "${title}"? Aceasta actiune nu poate fi anulata.`)) {
      return;
    }

    this.votingService.deleteElection(id).subscribe({
      next: () => this.loadElections(),
      error: (err) => alert(err?.error?.message ?? 'Nu am putut sterge alegerea.')
    });
  }
}
