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
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue() as any).subscribe({
      next: () => {
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isLoading.set(false);
        
        let message = 'Email sau parolă incorecte.';
        
        if (err?.error?.message) {
          message = err.error.message;
        } else if (err?.status === 401) {
          message = 'Email sau parolă incorecte.';
        } else if (err?.status === 403) {
          message = 'Contul tău nu are permisiunea de a accesa această resursă.';
        } else if (err?.status === 400) {
          message = 'Date invalide. Verfică email-ul și parola.';
        } else if (err?.status === 500) {
          message = 'Eroare server. Încearcă din nou mai târziu.';
        } else if (err?.status === 0) {
          message = 'Eroare de rețea. Verifică conexiunea la internet.';
        }
        
        this.errorMessage.set(message);
      }
    });
  }
}