import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { UserDetailsService } from '../../core/services/user-details.service';
import { LabelService } from '../../core/services/label.service';
import { GeographyService } from '../../core/services/geography.service';
import { GeographicNode } from '../../core/models/geography.model';
import { PersonalDetailsDto } from '../../core/models/user-details.model';
import { UserLabel } from '../../core/models/label.model';
import { parseCnp } from '../../core/utils/cnp.util';
import { removeDiacritics } from '../../core/utils/text.util';
import { INPUT_LIMITS } from '../../core/validators/input.validators';

/**
 * Applies a country choice to the profile form. A subdivision belongs to exactly one country, so
 * changing the country clears whichever county was picked under the previous one.
 */
export function applyCountrySelection(
  form: PersonalDetailsDto, code: string): PersonalDetailsDto {
  return { ...form, residenceCountry: code || null, residenceCounty: null, residenceCountyCode: null };
}

/**
 * Applies a county choice, storing it twice: the ISO code because it survives a rename of the
 * node, and the display name because that is what a vote declaration carries.
 *
 * @param counties The currently loaded level. A code that is not in it clears the selection
 *                 rather than storing a county the tree cannot resolve.
 */
export function applyCountySelection(
  form: PersonalDetailsDto, code: string, counties: GeographicNode[]): PersonalDetailsDto {
  const node = counties.find(c => c.code === code);
  return { ...form, residenceCountyCode: node?.code ?? null, residenceCounty: node?.name ?? null };
}

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
  private labelService = inject(LabelService);
  private geographyService = inject(GeographyService);
  private router = inject(Router);

  isLoading = signal(true);
  isLoadingLabels = signal(true);
  isSaving = signal(false);
  saveSuccess = signal(false);
  errorKey = signal<string | null>(null);

  myLabels = signal<UserLabel[]>([]);

  countries = signal<GeographicNode[]>([]);
  counties = signal<GeographicNode[]>([]);
  isLoadingCountries = signal(true);
  isLoadingCounties = signal(false);

  /** True once the tree turns out to be empty, so the form can say so instead of showing a dead select. */
  readonly geographyUnavailable = computed(
    () => !this.isLoadingCountries() && this.countries().length === 0);

  form = signal<PersonalDetailsDto>({
    cnp: '',
    fullName: '',
    residenceCountry: '',
    residenceCounty: '',
    residenceCountyCode: '',
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
    this.geographyService.getCountries().subscribe({
      next: (countries) => {
        this.countries.set(countries);
        this.isLoadingCountries.set(false);
        this.loadCountiesForSavedCountry();
      },
      // An empty tree is a valid state - the seeder has not run yet. geographyUnavailable()
      // turns the selects into a message rather than leaving them silently empty.
      error: () => this.isLoadingCountries.set(false)
    });

    this.userDetailsService.getMyDetails().subscribe({
      next: (dto) => {
        if (dto) {
          this.form.set({ ...dto });
          this.loadCountiesForSavedCountry();
        }
        this.isLoading.set(false);
      },
      error: () => {
        // 204 No Content still counts as "no saved details"
        this.isLoading.set(false);
      }
    });

    this.labelService.getMyLabels().subscribe({
      next: (labels) => {
        this.myLabels.set(labels);
        this.isLoadingLabels.set(false);
      },
      error: () => {
        this.isLoadingLabels.set(false);
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

  updateCountry(code: string): void {
    this.form.update(f => applyCountrySelection(f, code));
    this.counties.set([]);
    this.saveSuccess.set(false);
    this.errorKey.set(null);
    this.loadCountiesForSavedCountry();
  }

  updateCounty(code: string): void {
    this.form.update(f => applyCountySelection(f, code, this.counties()));
    this.saveSuccess.set(false);
    this.errorKey.set(null);
  }

  /** Localities are typed rather than picked, so the spelling is folded as the user types. */
  updateCity(value: string): void {
    this.updateField('residenceCity', removeDiacritics(value));
  }

  private loadCountiesForSavedCountry(): void {
    const code = this.form().residenceCountry;
    const country = code ? this.countries().find(c => c.code === code) : undefined;
    if (!country || !country.hasChildren) return;

    this.isLoadingCounties.set(true);
    this.geographyService.getChildren(country.id).subscribe({
      next: (children) => {
        this.counties.set(children);
        this.isLoadingCounties.set(false);
      },
      error: () => this.isLoadingCounties.set(false)
    });
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
