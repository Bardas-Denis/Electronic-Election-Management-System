import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import {
  authErrorMessageKey,
  EMAIL_VALIDATORS,
  passwordsMatch,
  PASSWORD_VALIDATORS
} from './auth-form.utils';

describe('auth form utilities', () => {
  it('validates shared email and password rules', () => {
    const email = new FormControl('', EMAIL_VALIDATORS);
    const password = new FormControl('', PASSWORD_VALIDATORS);

    expect(email.hasError('required')).toBe(true);
    expect(password.hasError('required')).toBe(true);

    email.setValue('not-an-email');
    password.setValue('short');

    expect(email.hasError('email')).toBe(true);
    expect(password.hasError('minlength')).toBe(true);
  });

  it('detects mismatched password confirmation', () => {
    const form = new FormGroup(
      {
        password: new FormControl('correct-password'),
        confirmPassword: new FormControl('different-password')
      },
      passwordsMatch
    );

    expect(form.hasError('passwordsMismatch')).toBe(true);
  });

  it('maps typed API errors before HTTP status fallbacks', () => {
    const typedError = new HttpErrorResponse({
      status: 400,
      error: { errorCode: 'invalidCredentials' }
    });
    const conflict = new HttpErrorResponse({ status: 409 });

    expect(authErrorMessageKey(typedError)).toBe('errors.invalidCredentials');
    expect(authErrorMessageKey(conflict, {
      409: 'errors.emailAlreadyExists'
    })).toBe('errors.emailAlreadyExists');
  });
});
