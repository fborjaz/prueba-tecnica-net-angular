import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage, apiFieldErrors } from '../../core/interceptors/http.interceptors';
import { Client } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
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
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="dialog__head">
        <h2>{{ editing ? 'Editar cliente' : 'Registrar cliente' }}</h2>
        <p>{{ editing ? data.client!.fullName : 'Un cliente se registra una sola vez por su identificación.' }}</p>
      </div>
      <div class="dialog__body">
        @if (error()) { <div class="form-alert" role="alert" style="margin-bottom:1rem">{{ error() }}</div> }
        <div class="form-grid">
          <div class="field">
            <label for="cl-name">Nombres</label>
            <input id="cl-name" class="input" formControlName="name" maxlength="50" />
            <app-field-error [control]="form.controls.name" />
          </div>
          <div class="field">
            <label for="cl-last">Apellidos</label>
            <input id="cl-last" class="input" formControlName="lastName" maxlength="50" />
            <app-field-error [control]="form.controls.lastName" />
          </div>
          <div class="field">
            <label for="cl-id">Identificación</label>
            <input id="cl-id" class="input" formControlName="identification" inputmode="numeric" maxlength="13" />
            <span class="hint">Cédula (10 dígitos) o RUC (13 dígitos).</span>
            <app-field-error [control]="form.controls.identification" />
          </div>
          <div class="field">
            <label for="cl-phone">Teléfono</label>
            <input id="cl-phone" class="input" formControlName="phoneNumber" inputmode="numeric" maxlength="13" placeholder="09…" />
            <app-field-error [control]="form.controls.phoneNumber" />
          </div>
          <div class="field span-2">
            <label for="cl-email">Correo</label>
            <input id="cl-email" type="email" class="input" formControlName="email" maxlength="120" />
            <app-field-error [control]="form.controls.email" />
          </div>
          <div class="field span-2">
            <label for="cl-address">Dirección</label>
            <input id="cl-address" class="input" formControlName="address" maxlength="100" />
            <span class="hint">{{ form.controls.address.value.length }}/100, mínimo 20 caracteres.</span>
            <app-field-error [control]="form.controls.address" />
          </div>
          <div class="field span-2">
            <label for="cl-ref">Referencia de la dirección</label>
            <input id="cl-ref" class="input" formControlName="referenceAddress" maxlength="100" placeholder="Frente a…, junto a…" />
            <span class="hint">{{ form.controls.referenceAddress.value.length }}/100, mínimo 20 caracteres.</span>
            <app-field-error [control]="form.controls.referenceAddress" />
          </div>
        </div>
      </div>
      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          @if (saving()) { <span class="spinner"></span> Guardando… } @else { {{ editing ? 'Guardar cambios' : 'Registrar cliente' }} }
        </button>
      </div>
    </form>
  `
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
