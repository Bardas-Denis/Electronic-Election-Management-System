import { describe, expect, it } from 'vitest';
import { removeDiacritics } from './text.util';

describe('removeDiacritics', () => {
  it('folds Romanian diacritics to their base letters', () => {
    expect(removeDiacritics('Timișoara')).toBe('Timisoara');
    expect(removeDiacritics('Brăila')).toBe('Braila');
    expect(removeDiacritics('Târgu Mureș')).toBe('Targu Mures');
  });

  // The comma-below forms (U+0219/U+021B) and the cedilla forms (U+015F/U+0163) both appear in
  // Romanian text depending on the keyboard layout, and users mix them freely.
  it('folds the cedilla spellings the same way as the comma-below ones', () => {
    expect(removeDiacritics('Timişoara')).toBe(removeDiacritics('Timișoara'));
  });

  it('leaves text that has no diacritics untouched', () => {
    expect(removeDiacritics('Cluj-Napoca')).toBe('Cluj-Napoca');
  });

  it('keeps spacing and punctuation, folding only the letters', () => {
    expect(removeDiacritics('Sânnicolau Mare')).toBe('Sannicolau Mare');
  });
});
