import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

// Group-level validator: checks password === confirmPassword
function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirm = control.get('confirmPassword')?.value;
  if (password && confirm && password !== confirm) {
    return { passwordsMismatch: true };
  }
  return null;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  errorMessage = signal<string | null>(null);
  isLoading = signal(false);
  showPassword = signal(false);
  showConfirm = signal(false);

  form = this.fb.group(
    {
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: passwordsMatch }
  );

  get emailCtrl() { return this.form.get('email')!; }
  get passwordCtrl() { return this.form.get('password')!; }
  get confirmCtrl() { return this.form.get('confirmPassword')!; }

  togglePassword(): void { this.showPassword.update(v => !v); }
  toggleConfirm(): void  { this.showConfirm.update(v => !v); }

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

    if (controlName === 'confirmPassword') {
      if (control.errors['required']) return 'Confirmare parolă este obligatorie.';
    }

    return null;
  }

  getFormErrorMessage(): string | null {
    if (!this.form.errors) return null;
    if (this.form.errors['passwordsMismatch']) return 'Parolele nu se potrivesc.';
    return null;
  }

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.form.getRawValue() as any;

    this.authService.register({ email, password }).subscribe({
      next: () => {
        this.router.navigate(['/elections']);
      },
      error: (err) => {
        this.isLoading.set(false);
        
        let message = 'Nu am putut crea contul. Încearcă alt email.';
        
        if (err?.error?.message) {
          message = err.error.message;
        } else if (err?.error?.errors) {
          // Handle validation errors from API
          const errors = err.error.errors;
          if (errors.email) message = `Email: ${errors.email}`;
          else if (errors.password) message = `Parolă: ${errors.password}`;
          else message = Object.values(errors)[0] as string;
        } else if (err?.status === 409) {
          message = 'Acest email este deja înregistrat.';
        } else if (err?.status === 400) {
          message = 'Date invalide. Verifică formularul.';
        } else if (err?.status === 500) {
          message = 'Eroare pe server. Încearcă mai târziu.';
        } else if (err?.status === 0) {
          message = 'Eroare de conectare. Verifică conexiunea la internet.';
        }
        
        this.errorMessage.set(message);
      }
    });
  }
}