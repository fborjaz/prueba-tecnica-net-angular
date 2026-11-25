import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../core/interceptors/http.interceptors';
import { ApiResponse, Client, Contract, InternetService, MethodPayment } from '../../core/models/api.models';
import { CatalogService, NotifyService } from '../../core/services/app-services';
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
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="dialog__head">
        <h2>{{ title }}</h2>
        <p>{{ data.client.fullName }}{{ data.contract ? ', contrato #' + data.contract.id + ' (' + data.contract.serviceName + ')' : '' }}</p>
      </div>

      <div class="dialog__body" style="display:grid;gap:1rem">
        @if (error()) { <div class="form-alert" role="alert">{{ error() }}</div> }

        @if (data.action === 'new' || data.action === 'service') {
          <fieldset class="plans">
            <legend class="sr-only">Plan de internet</legend>
            @for (s of services(); track s.id) {
              <label class="plan" [class.plan--current]="s.id === data.contract?.serviceId">
                <input type="radio" formControlName="serviceId" [value]="s.id" />
                <span class="plan__speed sign">{{ s.speedMbps }}<small>Mbps</small></span>
                <span class="plan__info">
                  <strong>{{ s.name }}</strong>
                  <span class="subtle">{{ s.devices.join(', ') }}</span>
                </span>
                <span class="plan__price">{{ s.price | currency: 'USD' : 'symbol' : '1.2-2' }}<small>/mes</small></span>
                @if (s.id === data.contract?.serviceId) { <span class="chip">Actual</span> }
                @else if (data.contract && s.speedMbps > data.contract.speedMbps) { <span class="chip chip--ok">Mayor velocidad</span> }
                @else if (data.contract) { <span class="chip chip--warn">Menor velocidad</span> }
              </label>
            }
          </fieldset>
          @if (form.controls.serviceId.invalid && form.controls.serviceId.touched) {
            <span class="field-error">Elija un plan.</span>
          }
          @if (data.action === 'service') {
            <p class="note">El contrato actual quedará como sustituido y se creará uno nuevo en renovación que mantiene la fecha de fin ({{ data.contract!.endDate | date: 'd MMM y' }}).</p>
          }
        }

        @if (data.action !== 'service') {
          <div class="form-grid">
            @if (data.action === 'payment') {
              <div class="field">
                <label for="cd-amount">Monto (USD)</label>
                <input id="cd-amount" class="input" type="number" min="0.01" step="0.01" formControlName="amount" />
                <span class="hint">Precio del plan: {{ data.contract!.price | currency: 'USD' }}</span>
                <app-field-error [control]="form.controls.amount" />
              </div>
            }
            <div class="field">
              <label for="cd-method">Forma de pago</label>
              <select id="cd-method" class="select" formControlName="methodPaymentId">
                <option [ngValue]="null" disabled>Elegir…</option>
                @for (m of methods(); track m.id) {
                  <option [ngValue]="m.id" [disabled]="data.action === 'method' && m.id === data.contract?.methodPaymentId">
                    {{ m.description }}{{ data.action === 'method' && m.id === data.contract?.methodPaymentId ? ' (actual)' : '' }}
                  </option>
                }
              </select>
              <app-field-error [control]="form.controls.methodPaymentId" />
            </div>
            @if (data.action === 'new') {
              <div class="field">
                <label for="cd-months">Plazo</label>
                <select id="cd-months" class="select" formControlName="months">
                  <option [ngValue]="6">6 meses</option>
                  <option [ngValue]="12">12 meses</option>
                  <option [ngValue]="24">24 meses</option>
                </select>
              </div>
            }
          </div>
        }
      </div>

      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          @if (saving()) { <span class="spinner"></span> Procesando… } @else { {{ submit }} }
        </button>
      </div>
    </form>
  `,
  styles: `
    .plans { border: 0; margin: 0; padding: 0; display: grid; gap: 0.5rem; }
    .plan { display: grid; grid-template-columns: 5.5rem 1fr auto auto; align-items: center; gap: 1rem;
      padding: 0.75rem 1rem; border: 1px solid var(--line); border-radius: var(--r-control); cursor: pointer;
      transition: border-color .15s, background .15s; }
    .plan:hover { border-color: #b9c2cd; }
    .plan:has(input:checked) { border-color: var(--amber-deep); background: var(--amber-wash); }
    .plan:has(input:focus-visible) { box-shadow: var(--focus); }
    .plan--current { opacity: 0.6; cursor: default; pointer-events: none; }
    .plan input { position: absolute; opacity: 0; pointer-events: none; }
    .plan__speed { font-size: 2rem; }
    .plan__speed small { font: 600 var(--fs-xs) var(--font-ui); margin-left: 0.25rem; color: var(--ink-2); }
    .plan__info { display: grid; line-height: 1.3; min-width: 0; }
    .plan__price { font-weight: 700; white-space: nowrap; }
    .plan__price small { font-weight: 400; color: var(--ink-3); }
    .note { margin: 0; font-size: var(--fs-sm); color: var(--ink-2); padding: 0.75rem; background: #f5f7f9; border-radius: var(--r-control); }
    @media (max-width: 560px) { .plan { grid-template-columns: 4.5rem 1fr; } }
  `
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
