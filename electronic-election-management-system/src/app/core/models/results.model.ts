export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
  imageDataUrl?: string;
  // True for the synthetic "Other" slice the backend adds when the question allows an
  // "Other" free-text answer and at least one respondent used it. There's no real option
  // behind this row - substitute the localized 'elections.otherOptionLabel' translation
  // instead of `label` (which is just an English fallback) wherever this is set.
  isOtherOption?: boolean;
}

export interface QuestionResultDto {
  questionId: string;
  text: string;
  allowMultipleAnswers: boolean;
  questionType: 'Choice' | 'FreeText';
  totalVotes: number;
  results: OptionResultDto[];
  // Populated only for a 'FreeText' question - the raw submitted answers, with no
  // attribution to who submitted them.
  textAnswers: string[];
}

// Full results snapshot for one election - used by both HTTP and SignalR
export interface ElectionResultsDto {
  electionId: string;
  title: string;
  totalVotes: number;
  results: OptionResultDto[];
  questions: QuestionResultDto[];
}