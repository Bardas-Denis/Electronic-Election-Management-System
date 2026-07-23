export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
  imageDataUrl?: string;
}

export interface QuestionResultDto {
  questionId: string;
  text: string;
  totalVotes: number;
  results: OptionResultDto[];
}

// Full results snapshot for one election - used by both HTTP and SignalR
export interface ElectionResultsDto {
  electionId: string;
  title: string;
  totalVotes: number;
  results: OptionResultDto[];
  questions: QuestionResultDto[];
}
