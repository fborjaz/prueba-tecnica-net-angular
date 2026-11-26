import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { ApiResponse, Client, Contract, InternetService, MethodPayment } from '../../core/models/api.models';
import { CatalogService } from '../../core/services/catalog.service';
import { NotifyService } from '../../core/services/notify.service';
import { CashierApiService } from './cashier-api.service';

export type ContractAction = 'new' | 'service' | 'method' | 'payment';

export interface ContractDialogData {
  action: ContractAction;
  client: Client;
  contract?: Contract;
}

const TITLES: Record<ContractAction, string> = {
  new: 'Contratar servicio',
  service: 'Cambiar servicio',
  method: 'Cambiar forma de pago',
  payment: 'Registrar pago'
};

const SUBMIT: Record<ContractAction, string> = {
  new: 'Contratar',
  service: 'Cambiar servicio',
  method: 'Cambiar forma de pago',
  payment: 'Registrar pago'
};

/** Un solo diálogo para los procesos de caja sobre contratos. */
@Component({
  selector: 'app-contract-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './contract-dialog.component.html',
  styleUrl: './contract-dialog.component.scss'
})
export class ContractDialogComponent implements OnInit {
  readonly data = inject<ContractDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(CashierApiService);
  private readonly catalogs = inject(CatalogService);
  private readonly ref = inject(MatDialogRef<ContractDialogComponent>);
  private readonly notify = inject(NotifyService);

  readonly title = TITLES[this.data.action];
  readonly submit = SUBMIT[this.data.action];
  readonly services = signal<InternetService[]>([]);
  readonly methods = signal<MethodPayment[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  private readonly needsService = this.data.action === 'new' || this.data.action === 'service';
  private readonly needsMethod = this.data.action !== 'service';

  readonly form = inject(FormBuilder).group({
    serviceId: [null as number | null, this.needsService ? Validators.required : []],
    methodPaymentId: [
      (this.data.action === 'payment' ? this.data.contract?.methodPaymentId : null) ?? null as number | null,
      this.needsMethod ? Validators.required : []
    ],
    months: [12],
    amount: [this.data.contract?.price ?? null as number | null,
      this.data.action === 'payment' ? [Validators.required, Validators.min(0.01)] : []]
  });

  readonly hasServices = computed(() => this.services().length > 0);

  ngOnInit(): void {
    if (this.needsService) this.catalogs.services(true).subscribe(s => this.services.set(s));
    if (this.needsMethod) this.catalogs.methodPayments().subscribe(m => this.methods.set(m));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const { action, client, contract } = this.data;
    let request: Observable<ApiResponse<unknown>>;

    switch (action) {
      case 'new':
        request = this.api.createContract({ clientId: client.id, serviceId: v.serviceId!, methodPaymentId: v.methodPaymentId!, months: v.months ?? 12 });
        break;
      case 'service':
        request = this.api.changeService(contract!.id, v.serviceId!);
        break;
      case 'method':
        request = this.api.changeMethod(contract!.id, v.methodPaymentId!);
        break;
      default:
        request = this.api.pay({ contractId: contract!.id, amount: Number(v.amount), methodPaymentId: v.methodPaymentId });
    }

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
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
}
