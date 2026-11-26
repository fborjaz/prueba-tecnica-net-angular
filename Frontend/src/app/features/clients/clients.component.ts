import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Client, PagedResult } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { ClientsApiService } from '../../core/services/clients-api.service';
import { ClientFormDialogComponent } from '../../shared/components/client-form-dialog.component';
import { BulkUploadDialogComponent } from '../../shared/components/bulk-upload-dialog.component';
import { ConfirmService } from '../../shared/components/confirm.service';
import { DIALOG_DEFAULTS } from '../../shared/components/dialog-defaults';

/** Mantenimiento de clientes (solo cajero). */
@Component({
  selector: 'app-clients',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './clients.component.html'
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
