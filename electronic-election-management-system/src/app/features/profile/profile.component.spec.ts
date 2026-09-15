import { describe, expect, it } from 'vitest';
import { GeographicNode } from '../../core/models/geography.model';
import { PersonalDetailsDto } from '../../core/models/user-details.model';
import { applyCountrySelection, applyCountySelection } from './profile.component';

const counties: GeographicNode[] = [
  { id: 'cluj-id', name: 'Cluj', code: 'RO-CJ', category: 'subdivision', hasChildren: false },
  { id: 'timis-id', name: 'Timis', code: 'RO-TM', category: 'subdivision', hasChildren: false }
];

const saved: PersonalDetailsDto = {
  residenceCountry: 'RO',
  residenceCounty: 'Cluj',
  residenceCountyCode: 'RO-CJ',
  residenceCity: 'Cluj-Napoca'
};

describe('applyCountrySelection', () => {
  // A county belongs to exactly one country, so keeping it after the country changes would leave
  // the profile claiming a Romanian county under Germany.
  it('clears both county fields when the country changes', () => {
    const result = applyCountrySelection(saved, 'DE');

    expect(result.residenceCountry).toBe('DE');
    expect(result.residenceCounty).toBeNull();
    expect(result.residenceCountyCode).toBeNull();
  });

  it('stores an empty selection as null rather than an empty string', () => {
    expect(applyCountrySelection(saved, '').residenceCountry).toBeNull();
  });

  it('leaves unrelated fields alone', () => {
    expect(applyCountrySelection(saved, 'DE').residenceCity).toBe('Cluj-Napoca');
  });
});

describe('applyCountySelection', () => {
  // Both are stored: the code survives a rename of the node, the name is what a vote declaration
  // carries and what the profile shows back to the user.
  it('stores the code and the display name of the chosen county', () => {
    const result = applyCountySelection(saved, 'RO-TM', counties);

    expect(result.residenceCountyCode).toBe('RO-TM');
    expect(result.residenceCounty).toBe('Timis');
  });

  it('clears both fields when the selection is cleared', () => {
    const result = applyCountySelection(saved, '', counties);

    expect(result.residenceCountyCode).toBeNull();
    expect(result.residenceCounty).toBeNull();
  });

  it('clears both fields when the code matches no loaded county', () => {
    const result = applyCountySelection(saved, 'RO-XX', counties);

    expect(result.residenceCountyCode).toBeNull();
    expect(result.residenceCounty).toBeNull();
  });
});
