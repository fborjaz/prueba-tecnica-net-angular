import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { NotifyService } from '../../core/services/notify.service';
import { AuthService } from '../../core/services/auth.service';
import { AppValidators, matchFields } from '../../core/utils/validators';

@Component({
  selector: 'app-reset-password',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password.component.html',
  styleUrl: './auth-form.scss'
})
export class ResetPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

  token = '';
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private readonly value = signal('');

  readonly form = this.fb.nonNullable.group(
    { newPassword: ['', AppValidators.password], confirmPassword: ['', Validators.required] },
    { validators: matchFields('newPassword', 'confirmPassword') }
  );

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.form.controls.newPassword.valueChanges.subscribe(v => this.value.set(v));
  }

  rule(name: 'length' | 'number' | 'upper'): boolean {
    const v = this.value();
    return name === 'length' ? v.length >= 8 && v.length <= 30 : name === 'number' ? /\d/.test(v) : /[A-Z]/.test(v);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { newPassword, confirmPassword } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.auth.resetPassword(this.token, newPassword, confirmPassword).subscribe({
      next: message => {
        this.notify.success(message);
        this.router.navigate(['/auth/login']);
      },
      error: err => {
        this.error.set(apiErrorMessage(err));
        this.loading.set(false);
      }
    });
  }
}
