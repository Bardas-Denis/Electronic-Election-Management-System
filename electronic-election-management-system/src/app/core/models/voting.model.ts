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
  // Intrebarea efectiva prezentata votantilor, afisata deasupra optiunilor
  question?: string;
  type: ElectionType;
  isAnonymous: boolean;
  startsAt: string;
  endsAt: string;
  options: OptionDto[];
  // completat de backend pe baza userului curent, ca sa stie UI-ul daca mai poate vota
  hasUserVoted?: boolean;
  // true daca endsAt a trecut deja; alegerea nu mai accepta voturi noi, dar
  // voturile inregistrate anterior si rezultatele raman accesibile
  isExpired?: boolean;
  // true daca a fost inregistrat cel putin un vot; odata true, alegerea nu mai poate fi editata
  hasVotes?: boolean;
}

export interface OptionCreateDto {
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
  options: OptionCreateDto[];
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