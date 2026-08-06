export type ElectionType = 'Politic' | 'Comercial';
export type QuestionType = 'Choice' | 'FreeText';

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
  isRequired: boolean;
  allowMultipleAnswers: boolean;
  questionType: QuestionType;
  // Only meaningful for a 'Choice' question: voters may answer with free text
  // ("Other: ___") instead of / alongside picking a fixed option.
  allowOtherOption: boolean;
  // For a 'Choice' question, the selectable options. For a 'FreeText' question,
  // optional suggestion chips - voters may still type anything.
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
  // SYNC: ElectionDto — false while the owner has not yet published the election.
  // Invisible elections are hidden from voters' listing and cannot accept votes.
  // Owners always see their own elections regardless of this flag (via /elections/mine).
  // Optional so that stale API responses (missing field) are treated as visible rather
  // than showing a false-positive "Hidden" badge.
  isVisible?: boolean;
}

export interface CreateOptionDto {
  label: string;
  description?: string;
  imageDataUrl?: string;
}

export interface CreateElectionQuestionDto {
  text: string;
  isRequired: boolean;
  allowMultipleAnswers: boolean;
  questionType: QuestionType;
  allowOtherOption: boolean;
  options: CreateOptionDto[];
}

// SYNC: AudienceConditionDto — one label condition inside an AND-group
export interface AudienceConditionDto {
  labelId: string;
  /** True means "does NOT have this label" (NOT condition). */
  isExcluded: boolean;
}

// SYNC: AudienceGroupDto — one AND-group in the audience OR-of-AND rule
export interface AudienceGroupDto {
  conditions: AudienceConditionDto[];
}

// Payload for both create AND update (same shape, different HTTP verb)
export interface CreateElectionRequest {
  title: string;
  description?: string;
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  isClosed: boolean;
  // SYNC: CreateElectionRequest — when false, the election is hidden from voters until the
  // owner explicitly calls PATCH /elections/{id}/start. Defaults to true so elections created
  // without touching this field remain immediately visible (preserving existing behaviour).
  isVisible: boolean;
  invitedUserIds?: string[];
  invitedEmails?: string[];
  /** Audience rule: an OR of AND-groups with per-condition NOT support. */
  invitedAudienceGroups?: AudienceGroupDto[];
  startsAt: string;
  endsAt: string;
  options: CreateOptionDto[];
  questions: CreateElectionQuestionDto[];
}

// Which fields matter depends on the election's type - see VoterDeclarationModalComponent.
export interface VoterDeclarationDto {
  cnp?: string;
  fullName?: string;
  residenceCounty?: string;
  residenceAddress?: string;
  residenceCity?: string;
  citizenship?: string;

  gender?: string;
  workEmail?: string;
  department?: string;
  jobTitle?: string;
  company?: string;
  employeeId?: string;
}

export interface TextAnswerDto {
  questionId: string;
  text: string;
}

export interface CastVoteRequest {
  electionId: string;
  optionId: string;
  optionIds: string[];
  textAnswers: TextAnswerDto[];
  // Required by the backend when the election is not anonymous; omitted entirely for anonymous ones.
  voterDeclaration?: VoterDeclarationDto;
}

export interface UserVoteAnswerDto {
  questionId: string;
  optionId?: string;
  optionLabel?: string;
  // Set instead of optionId/optionLabel when this answer is for a FreeText question.
  text?: string;
}

export interface UserVoteDto {
  electionId: string;
  optionId?: string;
  optionLabel?: string;
  votedAt?: string;
  // False once the voter has already used their one allowed edit
  canEdit?: boolean;
  answers?: UserVoteAnswerDto[];
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

export interface InvitationLabelDto {
  id: string;
  name: string;
  category?: string | null;
  userCount: number;
  /** IDs of users assigned to this label (excluding the requesting user). */
  userIds: string[];
}
