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

// One voter behind an option. Only ever sent for a non-anonymous election, to an
// admin or the election's creator. `fullName` is null for a voter who has neither
// declared a name for the vote nor filled one into their profile - the email is
// always there, and names are not unique, so both are shown.
export interface OptionVoterDto {
  userId: string;
  email: string;
  fullName: string | null;
}

// One typed answer together with who wrote it. Text and author arrive paired
// because the results payload sends answers as bare strings - two people writing
// "Nothing" are indistinguishable there, so nothing could match a name to the
// right card by position.
export interface TextAnswerAuthorDto {
  answerText: string;
  userId: string;
  email: string;
  fullName: string | null;
}

export interface OptionVotersDto {
  optionId: string;
  label: string;
  voters: OptionVoterDto[];
}

// Full results snapshot for one election - used by both HTTP and SignalR
export interface ElectionResultsDto {
  electionId: string;
  title: string;
  isAnonymous: boolean;
  totalVotes: number;
  results: OptionResultDto[];
  questions: QuestionResultDto[];
}
