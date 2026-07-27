import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { UserDetailsService } from '../../core/services/user-details.service';
import { PersonalDetailsDto } from '../../core/models/user-details.model';
import { parseCnp } from '../../core/utils/cnp.util';
import { INPUT_LIMITS } from '../../core/validators/input.validators';

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
    residenceCounty: '',
    residenceAddress: '',
    residenceCity: '',
    citizenship: '',
    gender: '',
    workEmail: '',
    employeeId: '',
    department: '',
    jobTitle: '',
    company: ''
  });

  readonly validationErrorKey = computed<string | null>(() => {
    const value = this.form();
    const cnp = value.cnp?.trim() ?? '';
    const workEmail = value.workEmail?.trim() ?? '';

    if (cnp && !parseCnp(cnp)) return 'profile.validation.invalidCnp';
    if (workEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(workEmail)) {
      return 'profile.validation.invalidEmail';
    }

    const shortFields: (keyof PersonalDetailsDto)[] = [
      'fullName', 'residenceCounty', 'residenceCity', 'citizenship',
      'employeeId', 'department', 'jobTitle', 'company'
    ];
    if (shortFields.some(field => this.getField(field).length > INPUT_LIMITS.shortText) ||
        this.getField('residenceAddress').length > INPUT_LIMITS.address ||
        workEmail.length > INPUT_LIMITS.email) {
      return 'profile.validation.tooLong';
    }

    return null;
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
    this.errorKey.set(null);
  }

  getField(field: keyof PersonalDetailsDto): string {
    return (this.form()[field] as string) ?? '';
  }

  save(): void {
    if (this.isSaving()) return;
    const validationError = this.validationErrorKey();
    if (validationError) {
      this.errorKey.set(validationError);
      return;
    }
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
    const role = this.authService.currentUser()?.role ?? '';
    return role.replace(/([a-z])([A-Z])/g, '$1 $2');
  }
}
