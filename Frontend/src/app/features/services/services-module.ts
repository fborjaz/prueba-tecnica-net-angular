import { HttpClient, HttpContext } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, NgModule, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormControl, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { RouterModule, Routes } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { apiErrorMessage } from '../../core/interceptors/http.interceptors';
import { ApiResponse, InternetService } from '../../core/models/api.models';
import { CatalogService, NotifyService } from '../../core/services/app-services';
import { ConfirmService, DIALOG_DEFAULTS } from '../../shared/components/dialogs';
import { SharedModule } from '../../shared/shared-module';

const API = `${environment.apiUrl}/services`;

@Component({
  selector: 'app-service-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="dialog__head"><h2>{{ data ? 'Editar plan' : 'Nuevo plan' }}</h2></div>
      <div class="dialog__body">
        @if (error()) { <div class="form-alert" role="alert" style="margin-bottom:1rem">{{ error() }}</div> }
        <div class="form-grid">
          <div class="field span-2">
            <label for="s-name">Nombre del servicio</label>
            <input id="s-name" class="input" formControlName="name" maxlength="100" />
            <app-field-error [control]="form.controls.name" />
          </div>
          <div class="field span-2">
            <label for="s-desc">Descripción</label>
            <input id="s-desc" class="input" formControlName="description" maxlength="150" />
            <app-field-error [control]="form.controls.description" />
          </div>
          <div class="field">
            <label for="s-speed">Velocidad (Mbps)</label>
            <input id="s-speed" class="input" type="number" min="1" formControlName="speedMbps" />
            <app-field-error [control]="form.controls.speedMbps" />
          </div>
          <div class="field">
            <label for="s-price">Precio mensual (USD)</label>
            <input id="s-price" class="input" type="number" min="0.01" step="0.01" formControlName="price" />
            <app-field-error [control]="form.controls.price" />
          </div>
          <fieldset class="field span-2 devices">
            <legend>Equipos incluidos</legend>
            @for (d of devices.controls; track $index) {
              <div class="device">
                <input class="input" [formControl]="d" maxlength="50" [attr.aria-label]="'Equipo ' + ($index + 1)" />
                <button class="icon-btn" type="button" (click)="devices.removeAt($index)" [disabled]="devices.length === 1" aria-label="Quitar equipo"><app-icon name="x" /></button>
              </div>
            }
            <button class="btn btn--ghost btn--sm" type="button" (click)="addDevice()"><app-icon name="plus" /> Agregar equipo</button>
          </fieldset>
          <label class="check span-2"><input type="checkbox" formControlName="active" /> Disponible para contratar</label>
        </div>
      </div>
      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          @if (saving()) { <span class="spinner"></span> Guardando… } @else { {{ data ? 'Guardar cambios' : 'Crear plan' }} }
        </button>
      </div>
    </form>
  `,
  styles: `
    .devices { border: 0; padding: 0; margin: 0; }
    legend { font-size: var(--fs-sm); font-weight: 600; margin-bottom: 0.375rem; }
    .device { display: flex; gap: 0.375rem; margin-bottom: 0.375rem; }
    .check { display: flex; gap: 0.5rem; align-items: center; font-size: var(--fs-sm); }
  `
})
export class ServiceFormDialogComponent {
  readonly data = inject<InternetService | null>(MAT_DIALOG_DATA);
  private readonly http = inject(HttpClient);
  private readonly ref = inject(MatDialogRef<ServiceFormDialogComponent>);
  private readonly notify = inject(NotifyService);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly devices = new FormArray<FormControl<string>>(
    (this.data?.devices.length ? this.data.devices : ['']).map(d => this.deviceControl(d)));

  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.data?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data?.description ?? '', [Validators.required, Validators.maxLength(150)]],
    speedMbps: [this.data?.speedMbps ?? 100, [Validators.required, Validators.min(1), Validators.max(10000)]],
    price: [this.data?.price ?? 20, [Validators.required, Validators.min(0.01)]],
    active: [this.data?.active ?? true],
    devices: this.devices
  });

  addDevice(): void {
    this.devices.push(this.deviceControl(''));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const body = { ...this.form.getRawValue(), devices: this.devices.value.map(d => d.trim()) };
    const ctx = { context: new HttpContext().set(SKIP_ERROR_TOAST, true) };
    this.saving.set(true);
    (this.data
      ? this.http.put<ApiResponse<InternetService>>(`${API}/${this.data.id}`, body, ctx)
      : this.http.post<ApiResponse<InternetService>>(API, body, ctx)
    ).subscribe({
      next: res => {
        this.notify.success(res.message);
        this.ref.close(true);
      },
      error: err => {
        this.error.set(apiErrorMessage(err));
        this.saving.set(false);
      }
    });
  }

  private deviceControl(value: string): FormControl<string> {
    return new FormControl(value, { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] });
  }
}

/** Catálogo de planes de internet (administrador). */
@Component({
  selector: 'app-services',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Servicios" description="Planes de internet que los cajeros pueden contratar.">
      <button class="btn btn--primary" type="button" (click)="openForm()"><app-icon name="plus" /> Nuevo plan</button>
    </app-page-header>
    <section class="panel">
      @if (services(); as list) {
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th class="num">Velocidad</th><th>Plan</th><th>Equipos</th><th class="num">Precio</th><th>Estado</th><th class="actions"><span class="sr-only">Acciones</span></th></tr></thead>
            <tbody>
              @for (s of list; track s.id) {
                <tr>
                  <td class="num"><span class="sign speed">{{ s.speedMbps }}</span> Mbps</td>
                  <td><span class="primary">{{ s.name }}</span><div class="subtle">{{ s.description }}</div></td>
                  <td>{{ s.devices.join(', ') }}</td>
                  <td class="num">{{ s.price | currency: 'USD' }}</td>
                  <td><app-status [code]="s.active" [label]="s.active ? 'Disponible' : 'No disponible'" /></td>
                  <td class="actions">
                    <button class="icon-btn" type="button" (click)="openForm(s)" matTooltip="Editar" aria-label="Editar"><app-icon name="edit" /></button>
                    <button class="icon-btn" type="button" (click)="remove(s)" matTooltip="Eliminar" aria-label="Eliminar"><app-icon name="trash" /></button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: `.speed { font-size: 1.5rem; }`
})
export class ServicesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly catalogs = inject(CatalogService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  readonly services = signal<InternetService[] | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.catalogs.services(false).subscribe(s => this.services.set(s));
  }

  openForm(service?: InternetService): void {
    this.dialog
      .open(ServiceFormDialogComponent, { ...DIALOG_DEFAULTS, width: '600px', data: service ?? null })
      .afterClosed()
      .subscribe(saved => saved && this.load());
  }

  remove(service: InternetService): void {
    this.confirm
      .ask({ title: `Eliminar ${service.name}`, message: 'Solo es posible si ningún contrato vigente lo usa.', confirmText: 'Eliminar', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.http.delete<ApiResponse<null>>(`${API}/${service.id}`)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }
}

const routes: Routes = [{ path: '', component: ServicesComponent, title: 'Servicios' }];

@NgModule({
  declarations: [ServicesComponent, ServiceFormDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ServicesModule {}
