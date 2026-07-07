export type ElectionType = 'Politic' | 'Comercial';

export interface OptionDto {
  id: string;
  label: string;
}

export interface ElectionDto {
  id: string;
  title: string;
  description?: string;
  type: ElectionType;
  isAnonymous: boolean;
  startsAt: string;
  endsAt: string;
  options: OptionDto[];
  // completat de backend pe baza userului curent, ca sa stie UI-ul daca mai poate vota
  hasUserVoted?: boolean;
}

export interface CreateElectionRequest {
  title: string;
  description?: string;
  type: ElectionType;
  isAnonymous: boolean;
  startsAt: string;
  endsAt: string;
  optionLabels: string[];
}

export interface CastVoteRequest {
  electionId: string;
  optionId: string;
}
