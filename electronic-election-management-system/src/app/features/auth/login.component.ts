import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  errorMessage = signal<string | null>(null);
  infoMessage = signal<string | null>(this.resolveInfoMessage());
  isLoading = signal(false);
  showPassword = signal(false);

  // Client-side validation only - real check happens server-side
  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  get emailCtrl() { return this.form.get('email')!; }
  get passwordCtrl() { return this.form.get('password')!; }

  private resolveInfoMessage(): string | null {
    switch (this.route.snapshot.queryParamMap.get('reason')) {
      case 'role-changed':
        return 'Rolul tau a fost schimbat. Te rugam sa te autentifici din nou pentru a continua.';
      case 'session-expired':
        return 'Sesiunea ta a expirat. Te rugam sa te autentifici din nou.';
      default:
        return null;
    }
  }

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue() as any).subscribe({
      next: () => {
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(
          err?.error?.message ?? 'Email sau parolă incorecte.'
        );
      }
    });
  }
}