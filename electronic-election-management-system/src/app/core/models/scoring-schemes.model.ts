export interface ScoringSchemeDto {
  id: string;
  name: string;
  points: number[];
  isLinear: boolean;
  isPredefined: boolean;
  // Set when the points are produced by a backend scoring plugin. The browser cannot run
  // one, so such a scheme is never offered in the results simulator.
  pluginKey?: string | null;
}

export interface CreateScoringSchemeDto {
  name: string;
  points: number[];
}
