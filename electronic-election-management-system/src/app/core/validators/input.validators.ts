import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const INPUT_LIMITS = {
  email: 254,
  password: 128,
  shortText: 100,
  title: 200,
  question: 500,
  description: 2_000,
  address: 500,
  maxQuestions: 20,
  maxOptionsPerQuestion: 50
} as const;

export function trimmedRequired(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value.trim().length > 0
    ? null
    : { required: true };
}

export const dateRangeValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const startsAt = control.get('startsAt')?.value;
  const endsAt = control.get('endsAt')?.value;
  if (!startsAt || !endsAt) return null;

  const start = new Date(startsAt).getTime();
  const end = new Date(endsAt).getTime();
  return Number.isFinite(start) && Number.isFinite(end) && end > start
    ? null
    : { invalidDateRange: true };
};

export const uniqueOptionLabels: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const labels = Array.isArray(control.value)
    ? control.value
        .map((option: { label?: string } | null) => option?.label?.trim().toLocaleLowerCase() ?? '')
        .filter(Boolean)
    : [];
  return labels.length === new Set(labels).size ? null : { duplicateOptionLabels: true };
};

// Ensures voters can never open an election with nothing mandatory to answer.
export const atLeastOneRequiredQuestion: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const questions = Array.isArray(control.value) ? control.value : [];
  return questions.some((question: { isRequired?: boolean } | null) => question?.isRequired)
    ? null
    : { noRequiredQuestion: true };
};
