import { Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-auth-email-field',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <div class="field" [class.field--error]="control().invalid && control().touched">
      <label [for]="inputId()">{{ labelKey() | translate }}</label>
      <input
        [id]="inputId()"
        type="email"
        [formControl]="control()"
        [placeholder]="placeholderKey() | translate"
        autocomplete="email"
        required
        [attr.aria-describedby]="inputId() + '-error'"
      />
      @if (control().touched && control().hasError('required')) {
        <span [id]="inputId() + '-error'" class="field-error" role="alert">
          {{ requiredErrorKey() | translate }}
        </span>
      } @else if (control().touched && control().hasError('email')) {
        <span [id]="inputId() + '-error'" class="field-error" role="alert">
          {{ invalidErrorKey() | translate }}
        </span>
      }
    </div>
  `,
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
