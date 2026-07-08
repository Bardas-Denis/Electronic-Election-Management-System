import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VotingService } from '../../core/services/voting.service';
import { AuthService } from '../../core/services/auth.service';
import { ElectionDto } from '../../core/models/voting.model';

@Component({
  selector: 'app-election-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './election-list.component.html'
})
export class ElectionListComponent implements OnInit {
  private votingService = inject(VotingService);
  readonly authService = inject(AuthService);

  elections = signal<ElectionDto[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadElections();
  }

  loadElections(): void {
    this.isLoading.set(true);
    this.votingService.getElections().subscribe({
      next: (data) => {
        this.elections.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  deleteElection(id: string, title: string): void {
    if (!confirm(`Stergi alegerea "${title}"? Aceasta actiune nu poate fi anulata.`)) {
      return;
    }

    this.votingService.deleteElection(id).subscribe({
      next: () => this.loadElections(),
      error: () => alert('Nu am putut sterge alegerea.')
    });
  }
}
