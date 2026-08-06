export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
  imageDataUrl?: string;
  // True for the synthetic entry representing a Choice question's "Other" answers - there is
  // no real option behind it, so the label is replaced with a translated string in the UI.
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
