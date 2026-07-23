import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

export const EMAIL_VALIDATORS: ValidatorFn[] = [
  Validators.required,
  Validators.email
];

export const PASSWORD_VALIDATORS: ValidatorFn[] = [
  Validators.required,
  Validators.minLength(6)
];

export function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmation = control.get('confirmPassword')?.value;

  return password && confirmation && password !== confirmation
    ? { passwordsMismatch: true }
    : null;
}

export function authErrorMessageKey(
  error: HttpErrorResponse,
  statusOverrides: Readonly<Record<number, string>> = {}
): string {
  const errorCode = error.error?.errorCode;
  if (typeof errorCode === 'string' && errorCode.length > 0) {
    return `errors.${errorCode}`;
  }

  return statusOverrides[error.status] ?? {
    0: 'network.connectionError',
    400: 'network.badRequest',
    500: 'network.serverError'
  }[error.status] ?? 'errors.unknown';
}
