import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { ActivatedRoute } from '@angular/router';
import { ResultsService } from '../../core/services/results.service';
import { ElectionResultsDto, OptionResultDto, QuestionResultDto } from '../../core/models/results.model';

// One pie slice, precomputed from an option's results.
// `path` is an SVG path `d` attribute (viewBox 0 0 100 100); `isFullCircle`
// covers the one case a path arc can't express - a single option holding
// every vote, i.e. a 360-degree slice.
interface PieSegment {
  optionId: string;
  label: string;
  voteCount: number;
  percent: number;
  colorVar: string;
  path: string;
  isFullCircle: boolean;
}

@Component({
  selector: 'app-results-dashboard',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './results-dashboard.component.html',
  styleUrl: './results-dashboard.component.scss'
})
export class ResultsDashboardComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private resultsService = inject(ResultsService);

  readonly pieCenter = 50;
  readonly pieRadius = 42;
  // Angular gap between adjacent slices, in degrees - only applied when 2+
  // options actually have votes (see pieSegments).
  private readonly padAngleDeg = 1.6;

  // Key of the segment/row currently under the pointer, shared between the
  // pie chart and the legend, so hovering either one also highlights its pair.
  hoveredSlice = signal<string | null>(null);

  isLoading = signal(true);
  snapshot = signal<ElectionResultsDto | null>(null);

  // liveResults vine direct din serviciu (SignalR); folosim computed
  // ca sa afisam mereu cea mai recenta versiune (live daca a venit, altfel snapshot-ul initial)
  displayedResults = computed(() => this.resultsService.liveResults() ?? this.snapshot());

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

  questions(results: ElectionResultsDto): QuestionResultDto[] {
    return results.questions?.length
      ? results.questions
      : [{ questionId: '', text: results.title, totalVotes: results.totalVotes, results: results.results }];
  }

  percentFor(voteCount: number, total: number): number {
    return total > 0 ? Math.round((voteCount / total) * 100) : 0;
  }

  // true daca aceasta optiune e in frunte (folosit probabil pt highlight in UI)
  isLeading(voteCount: number, question: QuestionResultDto): boolean {
    if (question.totalVotes === 0 || voteCount === 0) return false;
    return voteCount === Math.max(...question.results.map((r) => r.voteCount));
  }

  sliceKey(questionId: string, optionId: string): string {
    return `${questionId}:${optionId}`;
  }

  // Turns a question's options into pie slices, as SVG path wedges with a
  // small angular gap between them. Path-based wedges (rather than stroking
  // a circle with stroke-dasharray) avoid a seam artefact where the circle's
  // own start/end point meets - with the dasharray trick that seam silently
  // swallows one side of the gap whenever two slices meet exactly there.
  pieSegments(question: QuestionResultDto): PieSegment[] {
    const total = question.totalVotes;
    const optionsWithVotes = question.results.filter((r) => r.voteCount > 0).length;
    // Only cut a gap when at least two options actually have votes - a single
    // option holding 100% must render as one unbroken circle, not a wedge
    // with a stray sliver where the (empty) neighbour would be.
    const padAngle = optionsWithVotes > 1 ? (this.padAngleDeg * Math.PI) / 180 : 0;

    let angleCursor = -Math.PI / 2; // start at 12 o'clock

    return question.results.map((option: OptionResultDto, index: number) => {
      const fraction = total > 0 ? option.voteCount / total : 0;
      const sweep = fraction * 2 * Math.PI;
      const start = angleCursor;
      angleCursor += sweep;

      const isFullCircle = sweep > 0 && optionsWithVotes === 1;
      const a0 = start + padAngle / 2;
      const a1 = angleCursor - padAngle / 2;
      const path = !isFullCircle && sweep > 0 && a1 > a0 ? this.wedgePath(a0, a1) : '';

      return {
        optionId: option.optionId,
        label: option.label,
        voteCount: option.voteCount,
        percent: this.percentFor(option.voteCount, total),
        colorVar: `var(--series-${(index % 8) + 1})`,
        path,
        isFullCircle
      };
    });
  }

  private wedgePath(a0: number, a1: number): string {
    const { pieCenter: c, pieRadius: r } = this;
    const x0 = c + r * Math.cos(a0);
    const y0 = c + r * Math.sin(a0);
    const x1 = c + r * Math.cos(a1);
    const y1 = c + r * Math.sin(a1);
    const largeArc = a1 - a0 > Math.PI ? 1 : 0;
    return `M${c},${c} L${x0},${y0} A${r},${r} 0 ${largeArc} 1 ${x1},${y1} Z`;
  }
}
