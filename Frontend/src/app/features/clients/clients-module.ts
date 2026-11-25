import { ChangeDetectionStrategy, Component, DestroyRef, NgModule, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { RouterModule, Routes } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Client, PagedResult } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
import { ClientsApiService } from '../../core/services/clients-api.service';
import { ClientFormDialogComponent } from '../../shared/components/client-form-dialog.component';
import { BulkUploadDialogComponent, ConfirmService, DIALOG_DEFAULTS } from '../../shared/components/dialogs';
import { SharedModule } from '../../shared/shared-module';

/** Mantenimiento de clientes (solo cajero). */
@Component({
  selector: 'app-clients',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Clientes" description="Cada cliente se registra una sola vez y se identifica por su cédula o RUC.">
      <button class="btn btn--outline" type="button" (click)="bulk()"><app-icon name="upload" /> Carga masiva</button>
      <button class="btn btn--primary" type="button" (click)="openForm()"><app-icon name="plus" /> Registrar cliente</button>
    </app-page-header>

    <section class="panel">
      <div class="toolbar">
        <label class="sr-only" for="cl-search">Buscar</label>
        <input id="cl-search" class="input grow" type="search" [formControl]="search" placeholder="Buscar por identificación, nombre o correo" />
        <label class="sr-only" for="cl-active">Estado</label>
        <select id="cl-active" class="select" [ngModel]="active()" (ngModelChange)="active.set($event); load(1)">
          <option [ngValue]="null">Todos</option>
          <option [ngValue]="true">Activos</option>
          <option [ngValue]="false">Inactivos</option>
        </select>
      </div>

      @if (result(); as r) {
        @if (r.items.length) {
          <div class="table-wrap">
            <table class="table">
              <thead><tr><th>Cliente</th><th>Identificación</th><th>Contacto</th><th>Dirección</th><th class="num">Contratos vigentes</th><th>Estado</th><th class="actions"><span class="sr-only">Acciones</span></th></tr></thead>
              <tbody>
                @for (c of r.items; track c.id) {
                  <tr>
                    <td class="primary">{{ c.fullName }}</td>
                    <td>{{ c.identification }}</td>
                    <td>{{ c.phoneNumber }}<div class="subtle">{{ c.email }}</div></td>
                    <td>{{ c.address }}<div class="subtle">{{ c.referenceAddress }}</div></td>
                    <td class="num">{{ c.activeContracts }}</td>
                    <td><app-status [code]="c.active" [label]="c.active ? 'Activo' : 'Inactivo'" /></td>
                    <td class="actions">
                      <button class="icon-btn" type="button" (click)="toggle(c)" [matTooltip]="c.active ? 'Inactivar' : 'Activar'"
                              [attr.aria-label]="c.active ? 'Inactivar' : 'Activar'"><app-icon [name]="c.active ? 'power' : 'check'" /></button>
                      <button class="icon-btn" type="button" (click)="openForm(c)" matTooltip="Editar" aria-label="Editar"><app-icon name="edit" /></button>
                      <button class="icon-btn" type="button" (click)="remove(c)" matTooltip="Eliminar" aria-label="Eliminar"><app-icon name="trash" /></button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="pager">
            <span>{{ r.total }} cliente(s), página {{ r.page }} de {{ r.totalPages || 1 }}</span>
            <div>
              <button class="icon-btn" type="button" [disabled]="r.page <= 1" (click)="load(r.page - 1)" aria-label="Página anterior"><app-icon name="left" /></button>
              <button class="icon-btn" type="button" [disabled]="r.page >= r.totalPages" (click)="load(r.page + 1)" aria-label="Página siguiente"><app-icon name="right" /></button>
            </div>
          </div>
        } @else {
          <app-empty icon="clients" title="No se encontraron clientes" hint="Registre uno nuevo o cargue varios desde un archivo." />
        }
      }
    </section>
  `
})
export class ClientsComponent implements OnInit {
  private readonly api = inject(ClientsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  private readonly destroyRef = inject(DestroyRef);

  readonly result = signal<PagedResult<Client> | null>(null);
  readonly active = signal<boolean | null>(null);
  readonly search = new FormControl('', { nonNullable: true });

  ngOnInit(): void {
    this.load(1);
    this.search.valueChanges
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));
  }

  load(page = this.result()?.page ?? 1): void {
    this.api.list(this.search.value.trim(), this.active(), page).subscribe(r => this.result.set(r));
  }

  openForm(client?: Client): void {
    this.dialog
      .open(ClientFormDialogComponent, { ...DIALOG_DEFAULTS, width: '640px', data: { client } })
      .afterClosed()
      .subscribe(saved => saved && this.load());
  }

  toggle(client: Client): void {
    this.api.setActive(client.id, !client.active).subscribe(res => {
      this.notify.success(res.message);
      this.load();
    });
  }

  remove(client: Client): void {
    this.confirm
      .ask({ title: `Eliminar a ${client.fullName}`, message: 'Solo es posible si no tiene contratos vigentes. Su historial se conserva.', confirmText: 'Eliminar', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.api.remove(client.id)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }

  bulk(): void {
    this.dialog
      .open(BulkUploadDialogComponent, {
        ...DIALOG_DEFAULTS,
        width: '640px',
        data: {
          title: 'Carga masiva de clientes',
          endpoint: `${environment.apiUrl}/clients/bulk`,
          columns: ['nombres', 'apellidos', 'identificacion', 'correo', 'telefono', 'direccion', 'referencia'],
          sample: ['Carla Andrea', 'Mena Ruiz', '0961234567', 'carla.mena@correo.com', '0981112233',
            'Av. Las Américas 456 y Calle 12', 'Diagonal al centro comercial, casa blanca'],
          templateName: 'plantilla-clientes.csv'
        }
      })
      .afterClosed()
      .subscribe(result => result && this.load(1));
  }
}

const routes: Routes = [{ path: '', component: ClientsComponent, title: 'Clientes' }];

@NgModule({
  declarations: [ClientsComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ClientsModule {}
