export type ElectionType = 'Politic' | 'Comercial';

export interface OptionDto {
  id: string;
  label: string;
  description?: string;
  imageDataUrl?: string;
}

export interface ElectionQuestionDto {
  id: string;
  text: string;
  displayOrder: number;
  options: OptionDto[];
}

export interface ElectionDto {
  id: string;
  title: string;
  description?: string;
  // The actual question shown to voters, displayed above the options
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  // Closed elections are only visible to their creator and invited accounts/emails.
  isClosed: boolean;
  startsAt: string;
  endsAt: string;
  options: OptionDto[];
  questions: ElectionQuestionDto[];
  // Filled in by the backend based on the current user, so the UI knows whether the user can still vote
  hasUserVoted?: boolean;
  // Optional current-user vote details (if backend returns them)
  userVoteOptionId?: string;
  userVoteOptionLabel?: string;
  // True if endsAt has already passed; the election no longer accepts new votes, but previously recorded votes and results remain accessible
  isExpired?: boolean;
  // True if at least one vote has been recorded; once true, the election can no longer be edited
  hasVotes?: boolean;
}

export interface CreateOptionDto {
  label: string;
  description?: string;
  imageDataUrl?: string;
}

export interface CreateElectionQuestionDto {
  text: string;
  options: CreateOptionDto[];
}

// Payload for both create AND update (same shape, different HTTP verb)
export interface CreateElectionRequest {
  title: string;
  description?: string;
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  isClosed: boolean;
  invitedUserIds?: string[];
  invitedEmails?: string[];
  startsAt: string;
  endsAt: string;
  options: CreateOptionDto[];
  questions: CreateElectionQuestionDto[];
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
  optionIds: string[];
  // Required by the backend when the election is not anonymous; omitted entirely for anonymous ones.
  voterDeclaration?: VoterDeclarationDto;
}

export interface UserVoteDto {
  electionId: string;
  optionId: string;
  optionLabel?: string;
  votedAt?: string;
  // False once the voter has already used their one allowed edit
  canEdit?: boolean;
  answers?: { questionId: string; optionId: string; optionLabel?: string }[];
}

export interface InviteToElectionRequest {
  userIds: string[];
  emails: string[];
}

export interface ElectionInvitationDto {
  id: string;
  userId?: string;
  email: string;
  method: 'Manual' | 'Email';
  createdAt: string;
}

export interface InvitationCandidateDto {
  id: string;
  email: string;
}
