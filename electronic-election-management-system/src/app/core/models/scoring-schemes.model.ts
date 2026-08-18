export interface ScoringSchemeDto {
  id: string;
  name: string;
  points: number[];
  isLinear: boolean;
  isPredefined: boolean;
}

export interface CreateScoringSchemeDto {
  name: string;
  points: number[];
}
