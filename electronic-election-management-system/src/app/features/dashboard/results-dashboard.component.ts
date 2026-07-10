import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ResultsService } from '../../core/services/results.service';
import { ElectionResultsDto } from '../../core/models/results.model';

@Component({
  selector: 'app-results-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './results-dashboard.component.html',
  styleUrl: './results-dashboard.component.scss'
})
export class ResultsDashboardComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private resultsService = inject(ResultsService);

  isLoading = signal(true);
  snapshot = signal<ElectionResultsDto | null>(null);

  // liveResults vine direct din serviciu (SignalR); folosim computed
  // ca sa afisam mereu cea mai recenta versiune (live daca a venit, altfel snapshot-ul initial)
  displayedResults = computed(() => this.resultsService.liveResults() ?? this.snapshot());

  maxVotes = computed(() => {
    const results = this.displayedResults()?.results ?? [];
    return Math.max(1, ...results.map((r) => r.voteCount));
  });

  private electionId!: string;

  ngOnInit(): void {
    this.electionId = this.route.snapshot.paramMap.get('id')!;

    // 1. Snapshot initial prin HTTP, ca dashboard-ul sa nu fie gol la incarcare
    this.resultsService.getResultsSnapshot(this.electionId).subscribe({
      next: (data) => {
        this.snapshot.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });

    // 2. Conectare SignalR pentru update-uri live dupa fiecare vot nou
    this.resultsService.connectToLiveResults(this.electionId);
  }

  // important: inchide conexiunea SignalR la parasirea paginii
  ngOnDestroy(): void {
    this.resultsService.disconnect();
  }

  percentFor(voteCount: number): number {
    const total = this.displayedResults()?.totalVotes ?? 0;
    return total > 0 ? Math.round((voteCount / total) * 100) : 0;
  }

  // true daca aceasta optiune e in frunte (folosit probabil pt highlight in UI)
  isLeading(voteCount: number): boolean {
    const results = this.displayedResults()?.results ?? [];
    const total = this.displayedResults()?.totalVotes ?? 0;
    if (total === 0 || voteCount === 0) return false;
    return voteCount === Math.max(...results.map((r) => r.voteCount));
  }
}