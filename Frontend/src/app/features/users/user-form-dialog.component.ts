import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage, apiFieldErrors } from '../../core/interceptors/http.interceptors';
import { User } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
import { AppValidators, applyServerErrors } from '../../core/utils/validators';
import { UsersApiService } from './users-api.service';

export interface UserFormData {
  user?: User;
  /** El gestor crea usuarios que quedan pendientes de aprobación. */
  isManager: boolean;
}

@Component({
  selector: 'app-user-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="dialog__head">
        <h2>{{ editing ? 'Editar usuario' : 'Nuevo usuario' }}</h2>
        @if (!editing && data.isManager) {
          <p>El usuario quedará pendiente hasta que un administrador lo apruebe.</p>
        } @else if (editing) {
          <p>{{ data.user!.username }}. El nombre de usuario no se puede cambiar.</p>
        }
      </div>

      <div class="dialog__body">
        @if (error()) { <div class="form-alert" role="alert" style="margin-bottom:1rem">{{ error() }}</div> }

        <div class="form-grid">
          @if (!editing) {
            <div class="field">
              <label for="u-username">Nombre de usuario</label>
              <input id="u-username" class="input" formControlName="username" autocomplete="off" maxlength="20" />
              <span class="hint">8 a 20 caracteres, letras y al menos un número.</span>
              <app-field-error [control]="form.controls.username" />
            </div>
          }
          <div class="field">
            <label for="u-rol">Rol</label>
            <select id="u-rol" class="select" formControlName="rolId">
              <option [ngValue]="3">Cajero</option>
              <option [ngValue]="2">Gestor</option>
            </select>
          </div>
          <div class="field">
            <label for="u-names">Nombres</label>
            <input id="u-names" class="input" formControlName="names" maxlength="50" />
            <app-field-error [control]="form.controls.names" />
          </div>
          <div class="field">
            <label for="u-lastnames">Apellidos</label>
            <input id="u-lastnames" class="input" formControlName="lastNames" maxlength="50" />
            <app-field-error [control]="form.controls.lastNames" />
          </div>
          <div class="field">
            <label for="u-identification">Identificación</label>
            <input id="u-identification" class="input" formControlName="identification" inputmode="numeric" maxlength="13" />
            <app-field-error [control]="form.controls.identification" />
          </div>
          <div class="field">
            <label for="u-email">Correo</label>
            <input id="u-email" type="email" class="input" formControlName="email" maxlength="100" />
            <app-field-error [control]="form.controls.email" />
          </div>
          <div class="field span-2">
            <label for="u-password">{{ editing ? 'Nueva contraseña (opcional)' : 'Contraseña' }}</label>
            <input id="u-password" type="password" class="input" formControlName="password" autocomplete="new-password" maxlength="30" />
            <span class="hint">8 a 30 caracteres, al menos un número y una mayúscula.{{ editing ? ' Déjela vacía para mantener la actual.' : '' }}</span>
            <app-field-error [control]="form.controls.password" />
          </div>
        </div>
      </div>

      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          @if (saving()) { <span class="spinner"></span> Guardando… } @else { {{ editing ? 'Guardar cambios' : 'Crear usuario' }} }
        </button>
      </div>
    </form>
  `
})
export class UserFormDialogComponent {
  readonly data = inject<UserFormData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(UsersApiService);
  private readonly ref = inject(MatDialogRef<UserFormDialogComponent>);
  private readonly notify = inject(NotifyService);

  readonly editing = !!this.data.user;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    username: [this.data.user?.username ?? '', this.editing ? [] : AppValidators.username],
    rolId: [this.data.user?.rolId ?? 3, Validators.required],
    names: [this.data.user?.names ?? '', AppValidators.personName],
    lastNames: [this.data.user?.lastNames ?? '', AppValidators.personName],
    identification: [this.data.user?.identification ?? '', AppValidators.identification],
    email: [this.data.user?.email ?? '', AppValidators.email(100)],
    password: ['', this.editing ? AppValidators.password.slice(1) : AppValidators.password]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body = {
      username: v.username.trim(),
      names: v.names.trim(),
      lastNames: v.lastNames.trim(),
      identification: v.identification.trim(),
      email: v.email.trim(),
      rolId: v.rolId,
      password: v.password || null
    };

    this.saving.set(true);
    this.error.set(null);
    const request = this.editing ? this.api.update(this.data.user!.id, body) : this.api.create(body);

    request.subscribe({
      next: res => {
        this.notify.success(res.message);
        this.ref.close(res.data);
      },
      error: err => {
        applyServerErrors(this.form, apiFieldErrors(err));
        this.error.set(apiErrorMessage(err));
        this.saving.set(false);
      }
    });
  }
}
