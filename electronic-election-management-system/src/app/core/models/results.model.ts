import { QuestionType } from './voting.model';
import { ScoringSchemeDto } from './scoring-schemes.model';

export interface OptionResultDto {
  optionId: string;
  label: string;
  voteCount: number;
  imageId?: string;
  // True for the synthetic entry representing a Choice question's "Other" answers - there is
  // no real option behind it, so the label is replaced with a translated string in the UI.
  isOtherOption?: boolean;
  rankCounts?: Record<number, number>;
}

export interface QuestionResultDto {
  questionId: string;
  text: string;
  allowMultipleAnswers: boolean;
  questionType: QuestionType;
  totalVotes: number;
  requiredRankCount?: number;
  scoringScheme?: ScoringSchemeDto;
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
