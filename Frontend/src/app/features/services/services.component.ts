import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { filter, switchMap } from 'rxjs';
import { InternetService } from '../../core/models/api.models';
import { CatalogService } from '../../core/services/catalog.service';
import { NotifyService } from '../../core/services/notify.service';
import { ConfirmService } from '../../shared/components/confirm.service';
import { DIALOG_DEFAULTS } from '../../shared/components/dialog-defaults';
import { ServiceFormDialogComponent } from './service-form-dialog.component';
import { ServicesApiService } from './services-api.service';

/** Catálogo de planes de internet (administrador). */
@Component({
  selector: 'app-services',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './services.component.html',
  styleUrl: './services.component.scss'
})
export class ServicesComponent implements OnInit {
  private readonly api = inject(ServicesApiService);
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
      .pipe(filter(Boolean), switchMap(() => this.api.remove(service.id)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }
}
