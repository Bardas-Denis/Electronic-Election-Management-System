/**
 * Folds accented letters onto their base letters: "Timișoara" becomes "Timisoara".
 *
 * Used on the residence city, which is typed freehand rather than picked from the geographic
 * tree. Two people naming the same city must not end up under two different labels because one
 * of them typed diacritics, so the value is folded before it is stored or compared.
 *
 * Romanian is spelled with either the comma-below letters (ș U+0219) or the older cedilla ones
 * (ş U+015F) depending on the keyboard, and both decompose to a base letter plus a combining
 * mark, so both fold to the same result.
 *
 * NOTE: letters with no canonical decomposition — Polish ł, Danish ø — pass through unchanged.
 * They are outside Romanian input and the server-side key is the authority regardless.
 */
export function removeDiacritics(value: string): string {
  return value.normalize('NFD').replace(/\p{M}/gu, '');
}
