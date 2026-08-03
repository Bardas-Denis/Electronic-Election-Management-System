import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import {
  atLeastOneRequiredQuestion,
  dateRangeValidator,
  trimmedRequired,
  uniqueOptionLabels
} from './input.validators';

describe('input validators', () => {
  it('rejects empty and whitespace-only required text', () => {
    expect(trimmedRequired(new FormControl(''))).toEqual({ required: true });
    expect(trimmedRequired(new FormControl('   '))).toEqual({ required: true });
    expect(trimmedRequired(new FormControl(' Election '))).toBeNull();
  });

  it('requires the election end to be after its start', () => {
    const form = new FormGroup({
      startsAt: new FormControl('2026-07-27T10:00'),
      endsAt: new FormControl('2026-07-27T09:00')
    });

    expect(dateRangeValidator(form)).toEqual({ invalidDateRange: true });
    form.controls.endsAt.setValue('2026-07-27T11:00');
    expect(dateRangeValidator(form)).toBeNull();
  });

  it('rejects duplicate option labels regardless of case or surrounding spaces', () => {
    expect(uniqueOptionLabels(new FormControl([
      { label: 'Candidate A' },
      { label: ' candidate a ' }
    ]))).toEqual({ duplicateOptionLabels: true });

    expect(uniqueOptionLabels(new FormControl([
      { label: 'Candidate A' },
      { label: 'Candidate B' }
    ]))).toBeNull();
  });

  it('requires at least one question to be marked as required', () => {
    expect(atLeastOneRequiredQuestion(new FormControl([
      { text: 'Q1', isRequired: false },
      { text: 'Q2', isRequired: false }
    ]))).toEqual({ noRequiredQuestion: true });

    expect(atLeastOneRequiredQuestion(new FormControl([
      { text: 'Q1', isRequired: false },
      { text: 'Q2', isRequired: true }
    ]))).toBeNull();
  });
});
