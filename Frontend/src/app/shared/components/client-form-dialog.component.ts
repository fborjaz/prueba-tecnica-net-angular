import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage, apiFieldErrors } from '../../core/interceptors/api-errors';
import { Client } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { ClientsApiService } from '../../core/services/clients-api.service';
import { AppValidators, applyServerErrors } from '../../core/utils/validators';

export interface ClientFormData {
  client?: Client;
  /** Identificación precargada cuando el cajero la buscó y no existía. */
  identification?: string;
}

/** Registro y edición de clientes (mantenimiento de clientes y procesos de caja). */
@Component({
  selector: 'app-client-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './client-form-dialog.component.html'
})
export class ClientFormDialogComponent {
  readonly data = inject<ClientFormData>(MAT_DIALOG_DATA);
  private readonly api = inject(ClientsApiService);
  private readonly ref = inject(MatDialogRef<ClientFormDialogComponent>);
  private readonly notify = inject(NotifyService);

  readonly editing = !!this.data.client;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.data.client?.name ?? '', AppValidators.personName],
    lastName: [this.data.client?.lastName ?? '', AppValidators.personName],
    identification: [this.data.client?.identification ?? this.data.identification ?? '', AppValidators.identification],
    phoneNumber: [this.data.client?.phoneNumber ?? '', AppValidators.phone],
    email: [this.data.client?.email ?? '', AppValidators.email(120)],
    address: [this.data.client?.address ?? '', AppValidators.address],
    referenceAddress: [this.data.client?.referenceAddress ?? '', AppValidators.address]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body = Object.fromEntries(Object.entries(raw).map(([k, v]) => [k, v.trim()])) as typeof raw;

    this.saving.set(true);
    this.error.set(null);
    (this.editing ? this.api.update(this.data.client!.id, body) : this.api.create(body)).subscribe({
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
