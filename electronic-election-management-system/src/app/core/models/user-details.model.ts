// SYNC: VotingDtos.cs -> PersonalDetailsDto
export interface PersonalDetailsDto {
  // --- Politic ---
  cnp?: string | null;
  fullName?: string | null;
  residenceCounty?: string | null;
  residenceAddress?: string | null;
  residenceCity?: string | null;
  citizenship?: string | null;

  // --- Comercial ---
  gender?: string | null;
  workEmail?: string | null;
  employeeId?: string | null;
  department?: string | null;
  jobTitle?: string | null;
  company?: string | null;
}
