import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { ActivatedRoute } from '@angular/router';
import { ResultsService } from '../../core/services/results.service';
import { ElectionResultsDto, OptionResultDto, OptionVoterDto, OptionVotersDto, QuestionResultDto } from '../../core/models/results.model';
import { ScoringSchemesService } from '../../core/services/scoring-schemes.service';
import { ScoringSchemeDto } from '../../core/models/scoring-schemes.model';
import { AuthService } from '../../core/services/auth.service';
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
  isOtherOption: boolean;
}

// One ring for a multiple-answer question - each option gets its own
// independent 0-100% ring instead of sharing one pie, since a respondent can
// pick several options and the shares don't sum to a whole.
interface OptionMeter {
  optionId: string;
  label: string;
  voteCount: number;
  percent: number;
  colorVar: string;
  dasharray: string;
  isOtherOption: boolean;
}

// One distinct Other answer plus how many voters wrote it. Answers are matched
// case-insensitively after trimming, the same way the backend refuses an Other
// answer that duplicates an existing option label, so "Pizza" and "pizza " are
// one entry rather than two.
interface TextAnswerGroup {
  text: string;
  count: number;
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
  private scoringSchemesService = inject(ScoringSchemesService);
  private auth = inject(AuthService);

  readonly pieCenter = 50;
  readonly pieRadius = 42;
  // Angular gap between adjacent slices, in degrees - only applied when 2+
  // options actually have votes (see pieSegments).
  private readonly padAngleDeg = 1.6;

  readonly meterRadius = 40;
  private readonly meterCircumference = 2 * Math.PI * this.meterRadius;

  // How many free-text "Other" answers to show before collapsing the rest
  // behind a "+N more" button, same pattern as the invite-users picker.
  readonly otherAnswersPreviewCount = 4;

  // Which questions' Other-answers list the user has expanded to show
  // everything. Keyed by questionId since a page can have several
  // questions, each with its own independent expand state.
  private expandedOtherAnswers = signal<ReadonlySet<string>>(new Set());

  // Key of the segment/row currently under the pointer, shared between the
  // pie chart and the legend, so hovering either one also highlights its pair.
  // Which slice the pointer is over, carried as the pair rather than a joined
  // key so that "is this hover in my question?" stays a comparison instead of
  // string surgery.
  hoveredSlice = signal<{ questionId: string; optionId: string } | null>(null);

  // One selection per question, keyed by questionId. A slice stays lit after a
  // click, unlike hoveredSlice which only survives while the pointer is over it
  // - touch devices have no hover, so without a sticky selection the legend does
  // nothing at all on a phone. Per question rather than per page because a
  // dashboard is read side by side, and picking an answer in one question should
  // not silently clear what was picked in another.
  selectedSlices = signal<Record<string, string>>({});

  isLoading = signal(true);
  snapshot = signal<ElectionResultsDto | null>(null);

  // liveResults vine direct din serviciu (SignalR); folosim computed
  // ca sa afisam mereu cea mai recenta versiune (live daca a venit, altfel snapshot-ul initial)
  displayedResults = computed(() => this.resultsService.liveResults() ?? this.snapshot());

  // How many voters a group shows before collapsing the rest behind "+N more".
  // Per group rather than across the panel: one global limit would spend itself on
  // the first answer and leave the last one invisible.
  readonly voterPreviewCount = 4;

  // Keyed by questionId - each question's panel opens on its own.
  votersPanelOpen = signal<Record<string, boolean>>({});
  votersByQuestion = signal<Record<string, OptionVotersDto[]>>({});
  votersLoading = signal<Record<string, boolean>>({});
  votersErrorKey = signal<Record<string, string | null>>({});
  private expandedVoterGroups = signal<ReadonlySet<string>>(new Set());

  isFromMyElections = signal(false);
  rankingFilters = signal<Record<string, number>>({});
  
  availableSchemes = signal<ScoringSchemeDto[]>([]);
  simulatedSchemes = signal<Record<string, ScoringSchemeDto>>({});

  private electionId!: string;

  ngOnInit(): void {
    this.isFromMyElections.set(this.route.snapshot.queryParamMap.get('from') === 'my-elections');
    this.electionId = this.route.snapshot.paramMap.get('id')!;

    if (this.isFromMyElections()) {
      this.scoringSchemesService.getSchemes().subscribe({
        next: (schemes) => this.availableSchemes.set(schemes)
      });
    }

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

  questionTotal(question: QuestionResultDto): number {
    if (question.questionType === 'Ranking') {
      return question.results.reduce((sum, opt) => sum + this.getEffectiveVoteCount(opt, question), 0);
    }
    return question.totalVotes;
  }

  onRankingFilterChange(questionId: string, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.rankingFilters.update(current => ({
      ...current,
      [questionId]: value ? parseInt(value, 10) : 0
    }));
  }

  getRankingDropdownOptions(requiredCount: number): number[] {
    return Array.from({ length: requiredCount - 1 }, (_, i) => requiredCount - 1 - i);
  }

  onSchemeSimulationChange(question: QuestionResultDto, event: Event): void {
    const selectedId = (event.target as HTMLSelectElement).value;
    const originalId = question.scoringScheme?.id;

    if (selectedId === originalId || (!selectedId && !originalId)) {
      this.simulatedSchemes.update(current => {
        const next = { ...current };
        delete next[question.questionId];
        return next;
      });
    } else {
      const scheme = this.availableSchemes().find(s => s.id === selectedId);
      if (scheme) {
        this.simulatedSchemes.update(current => ({
          ...current,
          [question.questionId]: scheme
        }));
      }
    }
  }

  private getRankingPoints(rank: number, question: QuestionResultDto): number {
    const override = this.simulatedSchemes()[question.questionId];
    const activeScheme = override || question.scoringScheme;

    if (!activeScheme) {
      switch (rank) {
        case 1: return 12;
        case 2: return 10;
        case 3: return 8;
        case 4: return 7;
        case 5: return 6;
        case 6: return 5;
        case 7: return 4;
        case 8: return 3;
        case 9: return 2;
        case 10: return 1;
        default: return 0;
      }
    }

    if (activeScheme.isLinear) {
      return Math.max(0, question.results.length - rank + 1);
    }

    if (activeScheme.points && rank > 0 && rank <= activeScheme.points.length) {
      return activeScheme.points[rank - 1];
    }

    return 0;
  }

  getEffectiveVoteCount(option: OptionResultDto, question: QuestionResultDto): number {
    const maxRank = this.rankingFilters()[question.questionId];
    const isSimulated = !!this.simulatedSchemes()[question.questionId];

    // If no filter is applied and we're not simulating, use the backend's pre-calculated total
    if (!maxRank && !isSimulated) {
      return option.voteCount;
    }

    if (!option.rankCounts) {
      return option.voteCount;
    }

    let sum = 0;
    for (const [rankStr, count] of Object.entries(option.rankCounts)) {
      const rank = Number(rankStr);
      // If no maxRank is set, include all ranks
      if (!maxRank || rank <= maxRank) {
        sum += count * this.getRankingPoints(rank, question);
      }
    }
    return sum;
  }

  sortedResults(question: QuestionResultDto): (OptionResultDto & { effectiveVoteCount: number })[] {
    if (question.questionType === 'Ranking') {
      return question.results
        .map(o => ({ ...o, effectiveVoteCount: this.getEffectiveVoteCount(o, question) }))
        .sort((a, b) => b.effectiveVoteCount - a.effectiveVoteCount);
    }
    return question.results.map(o => ({ ...o, effectiveVoteCount: o.voteCount }));
  }

  // important: inchide conexiunea SignalR la parasirea paginii
  ngOnDestroy(): void {
    this.resultsService.disconnect();
  }

  questions(results: ElectionResultsDto): QuestionResultDto[] {
    return results.questions?.length
      ? results.questions
      : [{
        questionId: '', text: results.title, allowMultipleAnswers: false, questionType: 'Choice',
        totalVotes: results.totalVotes, results: results.results, textAnswers: []
      }];
  }

  percentFor(voteCount: number, total: number): number {
    return total > 0 ? Math.round((voteCount / total) * 100) : 0;
  }

  // A ranking bar is sized against the leader's score rather than the summed
  // points: "82% of the winner" compares two candidates, while a share of the
  // total says nothing, since ranking points accumulate independently instead
  // of dividing up a whole the way votes on a Choice question do.
  percentOfLeader(effectiveVoteCount: number, question: QuestionResultDto): number {
    const leader = Math.max(...question.results.map((r) => this.getEffectiveVoteCount(r, question)), 0);
    return leader > 0 ? Math.round((effectiveVoteCount / leader) * 100) : 0;
  }

  // true daca aceasta optiune e in frunte (folosit probabil pt highlight in UI)
  isLeading(effectiveVoteCount: number, question: QuestionResultDto): boolean {
    if (question.totalVotes === 0 || effectiveVoteCount === 0) return false;
    return effectiveVoteCount === Math.max(...question.results.map((r) => this.getEffectiveVoteCount(r, question)));
  }

  /**
   * Whether asking who voted is even on the table. An anonymous election never
   * gives this up, and among the rest it belongs to the people who own the
   * election - the server decides that for real, this only keeps the button off
   * screens where it could never work.
   */
  canAskWhoVoted(results: ElectionResultsDto): boolean {
    return !results.isAnonymous && (this.isFromMyElections() || this.auth.isAdmin());
  }

  /**
   * An election with no ElectionQuestion rows of its own still arrives carrying one
   * question, synthesised by the backend with an all-zero id. Asking the server about
   * that id gets a 404, so it has to be dropped from the request - without it, the
   * endpoint answers for the options hanging off the election itself, which is
   * precisely the shape those elections have.
   */
  private realQuestionId(questionId: string): string | undefined {
    const isPlaceholder = !questionId || questionId === '00000000-0000-0000-0000-000000000000';
    return isPlaceholder ? undefined : questionId;
  }

  isVotersPanelOpen(questionId: string): boolean {
    return this.votersPanelOpen()[questionId] === true;
  }

  /**
   * Opens or closes one question's voter panel, fetching on first open only -
   * identities stay on the server until somebody actually asks for them.
   */
  toggleVotersPanel(question: QuestionResultDto): void {
    const questionId = question.questionId;
    const opening = !this.isVotersPanelOpen(questionId);
    this.votersPanelOpen.update((current) => ({ ...current, [questionId]: opening }));

    if (!opening || this.votersByQuestion()[questionId] || this.votersLoading()[questionId]) {
      return;
    }

    this.votersLoading.update((current) => ({ ...current, [questionId]: true }));
    this.votersErrorKey.update((current) => ({ ...current, [questionId]: null }));

    this.resultsService.getVoters(this.electionId, this.realQuestionId(questionId)).subscribe({
      next: (groups) => {
        this.votersByQuestion.update((current) => ({ ...current, [questionId]: groups }));
        this.votersLoading.update((current) => ({ ...current, [questionId]: false }));
      },
      error: (err) => {
        const code = err?.error?.errorCode;
        this.votersErrorKey.update((current) => ({
          ...current,
          [questionId]: code ? `errors.${code}` : 'results.votersLoadFailed'
        }));
        this.votersLoading.update((current) => ({ ...current, [questionId]: false }));
      }
    });
  }

  /**
   * The groups to render: every answer while nothing is selected, otherwise just
   * the selected one. Filtering here rather than refetching keeps switching
   * between answers instant.
   */
  voterGroupsFor(question: QuestionResultDto): OptionVotersDto[] {
    const groups = this.votersByQuestion()[question.questionId] ?? [];
    const selected = this.selectedSlices()[question.questionId];
    return selected ? groups.filter((group) => group.optionId === selected) : groups;
  }

  visibleVoters(group: OptionVotersDto): OptionVoterDto[] {
    return this.isVoterGroupExpanded(group.optionId)
      ? group.voters
      : group.voters.slice(0, this.voterPreviewCount);
  }

  isVoterGroupExpanded(optionId: string): boolean {
    return this.expandedVoterGroups().has(optionId);
  }

  toggleVoterGroup(optionId: string): void {
    this.expandedVoterGroups.update((current) => {
      const next = new Set(current);
      next.has(optionId) ? next.delete(optionId) : next.add(optionId);
      return next;
    });
  }

  hoverSlice(questionId: string, optionId: string): void {
    this.hoveredSlice.set({ questionId, optionId });
  }

  clearHover(): void {
    this.hoveredSlice.set(null);
  }

  /**
   * The option this question is currently singled out on. Hovering wins over the
   * sticky selection while it lasts, so moving the pointer around still previews
   * other slices without losing what was picked - but only within the question
   * being hovered, so a pointer in one question leaves the others alone.
   */
  private activeOptionFor(questionId: string): string | null {
    const hovered = this.hoveredSlice();
    if (hovered !== null && hovered.questionId === questionId) {
      return hovered.optionId;
    }
    return this.selectedSlices()[questionId] ?? null;
  }

  isSliceActive(questionId: string, optionId: string): boolean {
    return this.activeOptionFor(questionId) === optionId;
  }

  // A question's other slices only dim once something in that question is
  // actually singled out.
  isSliceDimmed(questionId: string, optionId: string): boolean {
    const active = this.activeOptionFor(questionId);
    return active !== null && active !== optionId;
  }

  isSliceSelected(questionId: string, optionId: string): boolean {
    return this.selectedSlices()[questionId] === optionId;
  }

  toggleSliceSelection(questionId: string, optionId: string): void {
    this.selectedSlices.update((current) => {
      const next = { ...current };
      if (next[questionId] === optionId) {
        delete next[questionId];
      } else {
        next[questionId] = optionId;
      }
      return next;
    });
  }

  isOtherAnswersExpanded(questionId: string): boolean {
    return this.expandedOtherAnswers().has(questionId);
  }

  toggleOtherAnswers(questionId: string): void {
    this.expandedOtherAnswers.update((current) => {
      const next = new Set(current);
      if (next.has(questionId)) {
        next.delete(questionId);
      } else {
        next.add(questionId);
      }
      return next;
    });
  }

  // Identical Other answers collapse into a single entry carrying its
  // multiplicity, so ten voters writing the same thing read as one line with
  // x10 instead of ten identical lines. Most frequent first, since the count is
  // the whole reason to group; the first spelling seen wins the label.
  groupedTextAnswers(question: QuestionResultDto): TextAnswerGroup[] {
    const groups = new Map<string, TextAnswerGroup>();

    for (const answer of question.textAnswers) {
      const text = answer.trim();
      if (!text) continue;

      const key = text.toLocaleLowerCase();
      const existing = groups.get(key);
      if (existing) {
        existing.count++;
      } else {
        groups.set(key, { text, count: 1 });
      }
    }

    return [...groups.values()].sort((a, b) => b.count - a.count);
  }

  // Slices the groups the template already computed, rather than regrouping on
  // every change-detection pass.
  visibleAnswerGroups(groups: TextAnswerGroup[], questionId: string): TextAnswerGroup[] {
    return this.isOtherAnswersExpanded(questionId)
      ? groups
      : groups.slice(0, this.otherAnswersPreviewCount);
  }

  // Turns a multiple-answer question's options into independent rings - one
  // per option, each showing "% of respondents who picked this", since a
  // respondent can pick several and the shares don't sum to a whole circle.
  // Includes the synthetic "Other" entry (option.isOtherOption) the backend
  // appends to `results` when applicable, same as any other option - it just
  // gets its own ring like everything else here.
  optionMeters(question: QuestionResultDto): OptionMeter[] {
    const total = question.totalVotes;
    return question.results.map((option: OptionResultDto, index: number) => {
      const percent = this.percentFor(option.voteCount, total);
      const filled = (percent / 100) * this.meterCircumference;
      return {
        optionId: option.optionId,
        label: option.label,
        voteCount: option.voteCount,
        percent,
        colorVar: `var(--series-${(index % 8) + 1})`,
        dasharray: `${filled} ${this.meterCircumference - filled}`,
        isOtherOption: !!option.isOtherOption
      };
    });
  }

  // Turns a question's options into pie slices, as SVG path wedges with a
  // small angular gap between them. Path-based wedges (rather than stroking
  // a circle with stroke-dasharray) avoid a seam artefact where the circle's
  // own start/end point meets - with the dasharray trick that seam silently
  // swallows one side of the gap whenever two slices meet exactly there.
  //
  // Includes the synthetic "Other" entry (option.isOtherOption) the backend
  // appends to `results` when applicable, same as any other option - it just
  // gets its own wedge like everything else here, which is what keeps the
  // slices' vote counts summing back up to the question's total.
  pieSegments(question: QuestionResultDto): PieSegment[] {
    const total = this.questionTotal(question);
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
        isFullCircle,
        isOtherOption: !!option.isOtherOption
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