// SYNC: GeographyDtos.cs -> GeographicNodeDto, CreateGeographicNodeRequest, UpdateGeographicNodeRequest

/**
 * One node of the geographic label tree. The tree is browsed a level at a time — countries
 * first, then a node's children — so nothing ever loads the whole world at once.
 */
export interface GeographicNode {
  id: string;
  /** Display name. Translated and editable, so never use it as an identity. */
  name: string;
  /** ISO code: 'RO' for a country, 'RO-CJ' for a subdivision. Stable across renames. */
  code?: string | null;
  /** 'country' | 'subdivision' | 'locality' — see LabelCategories.cs. Structural, derived. */
  category?: string | null;
  /**
   * What the place is in words: 'County', 'Oraș', 'Comună'. Purely descriptive — it never
   * affects how results are grouped, which follows the tree itself.
   */
  kind?: string | null;
  /** Whether another level exists below, so a picker knows to offer one. */
  hasChildren: boolean;
}

/**
 * The kinds an administrator can pick, stored as these keys rather than as the words on screen.
 * A key survives a language switch and cannot be misspelt; free text would turn "Oraș", "Oras"
 * and "oraş" into three different kinds of the same thing.
 *
 * The seed maps the source's hundred-odd wordings onto these keys, so nothing reaches the
 * screen in English. What it cannot place is left unset and falls back to the structural level.
 */
export const GEOGRAPHIC_KINDS = [
  'autonomousCommunity',
  'borough',
  'canton',
  'city',
  'commune',
  'county',
  'department',
  'district',
  'emirate',
  'governorate',
  'island',
  'municipality',
  'parish',
  'prefecture',
  'province',
  'region',
  'republic',
  'sector',
  'state',
  'territory',
  'village'
] as const;;

export type GeographicKind = (typeof GEOGRAPHIC_KINDS)[number];

/** True when a stored kind is one of ours, and so has a translation to show. */
export function isKnownKind(kind: string | null | undefined): boolean {
  return !!kind && (GEOGRAPHIC_KINDS as readonly string[]).includes(kind);
}

/** Adds a node under an existing one. The category is derived server-side from the parent. */
export interface CreateGeographicNodeRequest {
  parentId: string;
  name: string;
  kind?: string | null;
}

/** Edits the two things safe to change by hand. The code and the parent stay put. */
export interface UpdateGeographicNodeRequest {
  name: string;
  kind?: string | null;
}
