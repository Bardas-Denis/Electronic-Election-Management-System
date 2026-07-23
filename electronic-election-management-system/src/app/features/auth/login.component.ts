import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { AuthEmailFieldComponent } from './auth-email-field.component';
import {
  authErrorMessageKey,
  EMAIL_VALIDATORS,
  PASSWORD_VALIDATORS
} from './auth-form.utils';
import { AuthPasswordFieldComponent } from './auth-password-field.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    AuthEmailFieldComponent,
    AuthPasswordFieldComponent
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly errorMessageKey = signal<string | null>(null);
  protected readonly infoMessageKey = signal<string | null>(this.resolveInfoMessageKey());
  protected readonly isLoading = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', EMAIL_VALIDATORS],
    password: ['', PASSWORD_VALIDATORS]
  });

  private resolveInfoMessageKey(): string | null {
    switch (this.route.snapshot.queryParamMap.get('reason')) {
      case 'role-changed':
        return 'auth.roleChanged';
      case 'session-expired':
        return 'auth.sessionExpired';
      default:
        return null;
    }
  }

  protected onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessageKey.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => this.router.navigate(['/elections']),
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessageKey.set(authErrorMessageKey(error, {
          401: 'errors.invalidCredentials',
          403: 'network.forbidden'
        }));
      }
    });
  }
}
