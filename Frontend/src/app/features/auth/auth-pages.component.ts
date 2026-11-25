import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { apiErrorMessage } from '../../core/interceptors/http.interceptors';
import { NotifyService } from '../../core/services/app-services';
import { AuthService } from '../../core/services/auth.service';
import { AppValidators, matchFields } from '../../core/utils/validators';

// ------------------------------------------------------------------ login

@Component({
  selector: 'app-login',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Iniciar sesión</h1>
    <p class="lead">Ingrese con su usuario asignado.</p>

    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      @if (error()) { <div class="form-alert" role="alert">{{ error() }}</div> }

      <div class="field">
        <label for="username">Usuario</label>
        <input id="username" class="input" formControlName="username" autocomplete="username"
               [class.is-invalid]="invalid('username')" />
        <app-field-error [control]="form.controls.username" />
      </div>

      <div class="field">
        <label for="password">Contraseña</label>
        <div class="password">
          <input id="password" class="input" formControlName="password" autocomplete="current-password"
                 [type]="showPassword() ? 'text' : 'password'" [class.is-invalid]="invalid('password')" />
          <button type="button" class="icon-btn" (click)="showPassword.set(!showPassword())"
                  [attr.aria-label]="showPassword() ? 'Ocultar contraseña' : 'Mostrar contraseña'">
            <app-icon [name]="showPassword() ? 'eyeOff' : 'eye'" />
          </button>
        </div>
        <app-field-error [control]="form.controls.password" />
      </div>

      <div class="row">
        <a class="link" routerLink="/auth/forgot-password">¿Olvidó su contraseña?</a>
      </div>

      <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">
        @if (loading()) { <span class="spinner"></span> Verificando… } @else { Iniciar sesión }
      </button>
    </form>

    <div class="demo">
      Usuarios de demostración:
      <ul>
        <li><button type="button" (click)="fill('admin2026', 'Admin2026')">admin2026</button>, administrador</li>
        <li><button type="button" (click)="fill('gestor2026', 'Gestor2026')">gestor2026</button>, gestor</li>
        <li><button type="button" (click)="fill('cajero01', 'Cajero2026')">cajero01</button>, cajero</li>
      </ul>
    </div>
  `,
  styleUrl: './auth-pages.component.scss'
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

// ------------------------------------------------------------------ recuperar contraseña

@Component({
  selector: 'app-forgot-password',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Recuperar contraseña</h1>
    <p class="lead">Le enviaremos un enlace para crear una nueva contraseña.</p>

    @if (sent()) {
      <p class="done" role="status">{{ sent() }}</p>
      <p><a class="link" routerLink="/auth/login">Volver a iniciar sesión</a></p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="email">Correo registrado</label>
          <input id="email" type="email" class="input" formControlName="email" autocomplete="email"
                 [class.is-invalid]="form.controls.email.invalid && form.controls.email.touched" />
          <app-field-error [control]="form.controls.email" />
        </div>
        <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">
          @if (loading()) { <span class="spinner"></span> Enviando… } @else { Enviar enlace }
        </button>
        <a class="link" routerLink="/auth/login">Volver a iniciar sesión</a>
      </form>
    }
  `,
  styleUrl: './auth-pages.component.scss'
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

// ------------------------------------------------------------------ restablecer contraseña

@Component({
  selector: 'app-reset-password',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Nueva contraseña</h1>
    <p class="lead">Elija una contraseña que no haya usado antes.</p>

    @if (!token) {
      <div class="form-alert">El enlace no es válido. Solicite uno nuevo.</div>
      <p><a class="link" routerLink="/auth/forgot-password">Solicitar enlace</a></p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (error()) { <div class="form-alert" role="alert">{{ error() }}</div> }
        <div class="field">
          <label for="newPassword">Nueva contraseña</label>
          <input id="newPassword" type="password" class="input" formControlName="newPassword" autocomplete="new-password" />
          <ul class="rules">
            <li [class.ok]="rule('length')">Entre 8 y 30 caracteres</li>
            <li [class.ok]="rule('number')">Al menos un número</li>
            <li [class.ok]="rule('upper')">Al menos una letra mayúscula</li>
          </ul>
        </div>
        <div class="field">
          <label for="confirmPassword">Confirmar contraseña</label>
          <input id="confirmPassword" type="password" class="input" formControlName="confirmPassword" autocomplete="new-password" />
          @if (form.errors?.['mismatch'] && form.controls.confirmPassword.touched) {
            <span class="field-error">{{ form.errors?.['mismatch'] }}</span>
          }
        </div>
        <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">
          @if (loading()) { <span class="spinner"></span> Guardando… } @else { Guardar contraseña }
        </button>
      </form>
    }
  `,
  styleUrl: './auth-pages.component.scss'
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
