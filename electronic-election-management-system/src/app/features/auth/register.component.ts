import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { AuthEmailFieldComponent } from './auth-email-field.component';
import {
  authErrorMessageKey,
  EMAIL_VALIDATORS,
  passwordsMatch,
  PASSWORD_VALIDATORS
} from './auth-form.utils';
import { AuthPasswordFieldComponent } from './auth-password-field.component';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    AuthEmailFieldComponent,
    AuthPasswordFieldComponent
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly errorMessageKey = signal<string | null>(null);
  protected readonly isLoading = signal(false);

  protected readonly form = this.fb.nonNullable.group(
    {
      email: ['', EMAIL_VALIDATORS],
      password: ['', PASSWORD_VALIDATORS],
      confirmPassword: ['', Validators.required]
    },
    { validators: passwordsMatch }
  );

  protected onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessageKey.set(null);

    const { email, password } = this.form.getRawValue();
    this.authService.register({ email, password }).subscribe({
      next: () => this.router.navigate(['/elections']),
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessageKey.set(authErrorMessageKey(error, {
          409: 'errors.emailAlreadyExists'
        }));
      }
    });
  }
}
