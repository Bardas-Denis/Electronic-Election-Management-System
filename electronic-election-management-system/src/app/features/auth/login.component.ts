import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';


@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  /** Translation key for inline server errors — resolved in template via | translate. */
  errorMessageKey = signal<string | null>(null);
  /** Translation key for info banners (role-changed, session-expired). */
  infoMessageKey = signal<string | null>(this.resolveInfoMessageKey());
  isLoading = signal(false);
  showPassword = signal(false);

  // Client-side validation only - real check happens server-side
  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  get emailCtrl() { return this.form.get('email')!; }
  get passwordCtrl() { return this.form.get('password')!; }

  private resolveInfoMessageKey(): string | null {
    switch (this.route.snapshot.queryParamMap.get('reason')) {
      case 'role-changed':   return 'auth.roleChanged';
      case 'session-expired': return 'auth.sessionExpired';
      default:               return null;
    }
  }

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  getErrorMessage(controlName: string): string | null {
    const control = this.form.get(controlName);
    if (!control || !control.touched || !control.errors) return null;

    if (controlName === 'email') {
      if (control.errors['required']) return 'Email-ul este obligatoriu.';
      if (control.errors['email']) return 'Email-ul nu este valid.';
    }

    if (controlName === 'password') {
      if (control.errors['required']) return 'Parola este obligatorie.';
      if (control.errors['minlength']) return `Parola trebuie să aibă cel puțin ${control.errors['minlength'].requiredLength} caractere.`;
    }

    return null;
  }

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessageKey.set(null);

    this.authService.login(this.form.getRawValue() as any).subscribe({
      next: () => {
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isLoading.set(false);

        const code: string | undefined = err?.error?.errorCode;
        if (code) {
          this.errorMessageKey.set(`errors.${code}`);
        } else if (err?.status === 401) {
          this.errorMessageKey.set('errors.invalidCredentials');
        } else if (err?.status === 403) {
          this.errorMessageKey.set('network.forbidden');
        } else if (err?.status === 400) {
          this.errorMessageKey.set('network.badRequest');
        } else if (err?.status === 500) {
          this.errorMessageKey.set('network.serverError');
        } else if (err?.status === 0) {
          this.errorMessageKey.set('network.connectionError');
        } else {
          this.errorMessageKey.set('errors.unknown');
        }
      }
    });
  }
}