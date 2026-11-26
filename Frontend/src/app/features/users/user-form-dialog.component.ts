import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage, apiFieldErrors } from '../../core/interceptors/api-errors';
import { User } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
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
  templateUrl: './user-form-dialog.component.html'
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
