export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
}

// Full results snapshot for one election - used by both HTTP and SignalR
export interface ElectionResultsDto {
  electionId: string;
  title: string;
  totalVotes: number;
  results: OptionResultDto[];
}