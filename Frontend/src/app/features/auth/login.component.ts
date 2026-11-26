import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { NotifyService } from '../../core/services/notify.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './auth-form.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.maxLength(20)]],
    password: ['', [Validators.required, Validators.maxLength(30)]]
  });

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showPassword = signal(false);

  invalid(name: 'username' | 'password'): boolean {
    const c = this.form.controls[name];
    return c.invalid && c.touched;
  }

  fill(username: string, password: string): void {
    this.form.setValue({ username, password });
    this.error.set(null);
  }

  submit(): void {
    // Valida antes de consumir el servicio.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { username, password } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set(null);

    this.auth.login(username.trim(), password).subscribe({
      next: user => {
        this.notify.success(`Bienvenido, ${user.names}.`);
        this.router.navigate(['/welcome']);
      },
      error: err => {
        this.error.set(apiErrorMessage(err, 'No se pudo iniciar sesión.'));
        this.loading.set(false);
      }
    });
  }
}
