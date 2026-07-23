// SYNC: VotingDtos.cs -> PersonalDetailsDto
export interface PersonalDetailsDto {
  // --- Politic ---
  cnp?: string | null;
  fullName?: string | null;
  domiciliuJudet?: string | null;
  domiciliuAdresa?: string | null;
  domiciliuLocalitate?: string | null;
  citizenship?: string | null;

  // --- Comercial ---
  gender?: string | null;
  workEmail?: string | null;
  employeeId?: string | null;
  department?: string | null;
  jobTitle?: string | null;
  company?: string | null;
}
