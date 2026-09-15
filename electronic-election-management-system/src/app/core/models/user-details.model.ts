// SYNC: VotingDtos.cs -> PersonalDetailsDto
export interface PersonalDetailsDto {
  // --- Politic ---
  cnp?: string | null;
  fullName?: string | null;
  /** ISO 3166-1 alpha-2 country code, picked from the geographic tree. */
  residenceCountry?: string | null;
  residenceCounty?: string | null;
  /** ISO 3166-2 subdivision code; the name above stays for display and for vote declarations. */
  residenceCountyCode?: string | null;
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
