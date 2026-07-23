import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { UserDetailsService } from '../../core/services/user-details.service';
import { PersonalDetailsDto } from '../../core/models/user-details.model';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent implements OnInit {
  readonly authService = inject(AuthService);
  private userDetailsService = inject(UserDetailsService);
  private router = inject(Router);

  isLoading = signal(true);
  isSaving = signal(false);
  saveSuccess = signal(false);
  errorKey = signal<string | null>(null);

  form = signal<PersonalDetailsDto>({
    cnp: '',
    fullName: '',
    domiciliuJudet: '',
    domiciliuAdresa: '',
    domiciliuLocalitate: '',
    citizenship: '',
    gender: '',
    workEmail: '',
    employeeId: '',
    department: '',
    jobTitle: '',
    company: ''
  });

  ngOnInit(): void {
    this.userDetailsService.getMyDetails().subscribe({
      next: (dto) => {
        if (dto) {
          this.form.set({ ...dto });
        }
        this.isLoading.set(false);
      },
      error: () => {
        // 204 No Content still counts as "no saved details"
        this.isLoading.set(false);
      }
    });
  }

  updateField(field: keyof PersonalDetailsDto, value: string): void {
    this.form.update(f => ({ ...f, [field]: value || null }));
    this.saveSuccess.set(false);
  }

  getField(field: keyof PersonalDetailsDto): string {
    return (this.form()[field] as string) ?? '';
  }

  save(): void {
    if (this.isSaving()) return;
    this.isSaving.set(true);
    this.errorKey.set(null);
    this.saveSuccess.set(false);

    this.userDetailsService.saveMyDetails(this.form()).subscribe({
      next: (saved) => {
        this.form.set({ ...saved });
        this.isSaving.set(false);
        this.saveSuccess.set(true);
        setTimeout(() => this.saveSuccess.set(false), 3000);
      },
      error: (err) => {
        this.isSaving.set(false);
        const code: string | undefined = err?.error?.errorCode;
        this.errorKey.set(code ? `errors.${code}` : 'profile.saveFailed');
      }
    });
  }

  get avatarLetter(): string {
    return (this.authService.currentUser()?.email ?? '?').charAt(0).toUpperCase();
  }

  get userEmail(): string {
    return this.authService.currentUser()?.email ?? '';
  }

  get userRole(): string {
    return this.authService.currentUser()?.role ?? '';
  }
}
