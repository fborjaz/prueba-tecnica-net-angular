import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { AppValidators } from '../../core/utils/validators';

@Component({
  selector: 'app-forgot-password',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password.component.html',
  styleUrl: './auth-form.scss'
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({ email: ['', AppValidators.email(100)] });
  readonly loading = signal(false);
  readonly sent = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.auth.forgotPassword(this.form.getRawValue().email.trim()).subscribe({
      next: message => this.sent.set(message),
      error: () => this.loading.set(false)
    });
  }
}
