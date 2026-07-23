import { Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-auth-email-field',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './auth-email-field.component.html',
  styleUrl: './auth-form-field.component.scss'
})
export class AuthEmailFieldComponent {
  readonly control = input.required<FormControl<string>>();
  readonly inputId = input.required<string>();
  readonly labelKey = input.required<string>();
  readonly placeholderKey = input.required<string>();
  readonly requiredErrorKey = input.required<string>();
  readonly invalidErrorKey = input.required<string>();
}
