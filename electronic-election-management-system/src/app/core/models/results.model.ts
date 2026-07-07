export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
}

export interface ElectionResultsDto {
  electionId: string;
  title: string;
  totalVotes: number;
  results: OptionResultDto[];
}
