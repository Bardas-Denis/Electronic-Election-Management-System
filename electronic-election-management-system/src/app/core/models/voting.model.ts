export type ElectionType = 'Politic' | 'Comercial';

export interface OptionDto {
  id: string;
  label: string;
  description?: string;
}

export interface ElectionDto {
  id: string;
  title: string;
  description?: string;
  // The actual question shown to voters, displayed above the options
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  startsAt: string;
  endsAt: string;
  options: OptionDto[];
  // Filled in by the backend based on the current user, so the UI knows whether the user can still vote
  hasUserVoted?: boolean;
  // True if endsAt has already passed; the election no longer accepts new votes, but previously recorded votes and results remain accessible
  isExpired?: boolean;
  // True if at least one vote has been recorded; once true, the election can no longer be edited
  hasVotes?: boolean;
}

export interface CreateOptionDto {
  label: string;
  description?: string;
}

// Payload for both create AND update (same shape, different HTTP verb)
export interface CreateElectionRequest {
  title: string;
  description?: string;
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  startsAt: string;
  endsAt: string;
  options: CreateOptionDto[];
}

// Which fields matter depends on the election's type - see VoterDeclarationModalComponent.
export interface VoterDeclarationDto {
  cnp?: string;
  fullName?: string;
  domiciliuJudet?: string;
  domiciliuAdresa?: string;
  domiciliuLocalitate?: string;
  citizenship?: string;

  gender?: string;
  workEmail?: string;
  department?: string;
  jobTitle?: string;
  company?: string;
  employeeId?: string;
}

export interface CastVoteRequest {
  electionId: string;
  optionId: string;
  // Required by the backend when the election is not anonymous; omitted entirely for anonymous ones.
  voterDeclaration?: VoterDeclarationDto;
}