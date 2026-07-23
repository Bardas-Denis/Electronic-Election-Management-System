import { Component, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-auth-password-field',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './auth-password-field.component.html',
  styleUrl: './auth-form-field.component.scss'
})
export class AuthPasswordFieldComponent {
  readonly control = input.required<FormControl<string>>();
  readonly inputId = input.required<string>();
  readonly labelKey = input.required<string>();
  readonly placeholderKey = input.required<string>();
  readonly requiredErrorKey = input.required<string>();
  readonly showPasswordKey = input.required<string>();
  readonly hidePasswordKey = input.required<string>();
  readonly autocomplete = input<'current-password' | 'new-password'>('current-password');
  readonly minLengthErrorKey = input<string>();
  readonly mismatch = input(false);
  readonly mismatchErrorKey = input<string>();

  protected readonly showPassword = signal(false);

  protected togglePassword(): void {
    this.showPassword.update(visible => !visible);
  }
}
