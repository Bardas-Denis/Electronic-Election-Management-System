// SYNC: GeographyDtos.cs -> GeographicNodeDto, CreateGeographicNodeRequest, RenameGeographicNodeRequest

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
  /** 'country' | 'subdivision' | 'locality' — see LabelCategories.cs. */
  category?: string | null;
  /** Whether another level exists below, so a picker knows to offer one. */
  hasChildren: boolean;
}

/** Adds a node under an existing one. The category is derived server-side from the parent. */
export interface CreateGeographicNodeRequest {
  parentId: string;
  name: string;
}

export interface RenameGeographicNodeRequest {
  name: string;
}
