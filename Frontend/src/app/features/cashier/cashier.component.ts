import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, Validators } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { filter, switchMap } from 'rxjs';
import { ATTENTION_STATUS, Cash, CashSession, Client, Contract, Payment, Turn } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
import { AuthService } from '../../core/services/auth.service';
import { ClientsApiService } from '../../core/services/clients-api.service';
import { AppValidators } from '../../core/utils/validators';
import { ClientFormDialogComponent } from '../../shared/components/client-form-dialog.component';
import { ConfirmService, DIALOG_DEFAULTS } from '../../shared/components/dialogs';
import { CashierApiService } from './cashier-api.service';
import { ContractAction, ContractDialogComponent } from './contract-dialogs.component';

/** Proceso sugerido según el tipo de atención del turno. */
const SUGGESTED: Record<string, ContractAction | 'cancel' | null> = {
  CT: 'new',
  PS: 'payment',
  CS: 'service',
  CF: 'method',
  CC: 'cancel',
  AC: null
};

const QUEUE_REFRESH_MS = 15000;

@Component({
  selector: 'app-cashier',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cashier.component.html',
  styleUrl: './cashier.component.scss'
})
export class CashierComponent implements OnInit, OnDestroy {
  private readonly api = inject(CashierApiService);
  private readonly clientsApi = inject(ClientsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);

  readonly username = computed(() => this.auth.user()?.username);
  readonly ready = signal(false);
  readonly cashes = signal<Cash[]>([]);
  readonly session = signal<CashSession | null>(null);
  readonly queue = signal<Turn[]>([]);
  readonly client = signal<Client | null>(null);
  readonly clientNotFound = signal<string | null>(null);
  readonly contracts = signal<Contract[]>([]);
  readonly payments = signal<Payment[]>([]);
  readonly busy = signal(false);

  readonly identification = new FormControl('', { nonNullable: true, validators: AppValidators.identification });
  readonly notes = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(250)] });

  readonly current = computed(() => this.queue().find(t => t.statusId === ATTENTION_STATUS.inProgress) ?? null);
  readonly waiting = computed(() => this.queue().filter(t => t.statusId === ATTENTION_STATUS.pending));
  readonly suggested = computed(() => SUGGESTED[this.current()?.attentionTypeId ?? ''] ?? null);
  readonly activeContracts = computed(() => this.contracts().filter(c => c.statusId === 'VIG' || c.statusId === 'REN'));
  readonly pastContracts = computed(() => this.contracts().filter(c => c.statusId === 'SUS' || c.statusId === 'CAN'));

  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.api.session().subscribe({
      next: session => {
        this.session.set(session);
        this.ready.set(true);
        session ? this.startQueue() : this.loadCashes();
      },
      error: () => this.ready.set(true)
    });
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  // ---------------------------------------------------------------- caja

  loadCashes(): void {
    this.api.myCashes().subscribe(c => this.cashes.set(c));
  }

  open(cash: Cash): void {
    this.api.open(cash.id).subscribe({
      next: res => {
        this.notify.success(res.message);
        this.session.set(res.data!);
        this.startQueue();
      },
      error: () => this.loadCashes()
    });
  }

  close(): void {
    this.confirm
      .ask({ title: 'Cerrar caja', message: 'El otro cajero asignado podrá abrirla después.', confirmText: 'Cerrar caja' })
      .pipe(filter(Boolean), switchMap(() => this.api.close()))
      .subscribe(res => {
        this.notify.success(res.message);
        clearInterval(this.timer);
        this.session.set(null);
        this.queue.set([]);
        this.resetAttention();
        this.loadCashes();
      });
  }

  // ---------------------------------------------------------------- turnos

  refreshQueue(silent = false): void {
    this.api.queue(silent).subscribe({
      next: q => {
        const hadCurrent = this.current()?.id;
        this.queue.set(q);
        // Si se recarga la página con un turno en atención, se recupera su cliente.
        const current = this.current();
        if (current && current.id !== hadCurrent && current.clientIdentification && !this.client()) {
          this.identification.setValue(current.clientIdentification);
          this.searchClient();
        }
      },
      error: () => this.verifySession()
    });
  }

  /** Si la caja se cerró desde otra sesión (otra pestaña o un cierre de sesión), la pantalla vuelve a "elegir caja". */
  private verifySession(): void {
    this.api.session().subscribe(session => {
      if (session) return;
      clearInterval(this.timer);
      this.session.set(null);
      this.queue.set([]);
      this.resetAttention();
      this.loadCashes();
      this.notify.info('Su caja se cerró desde otra sesión. Ábrala de nuevo para seguir atendiendo.');
    });
  }

  callNext(): void {
    const next = this.waiting()[0];
    if (!next) return;
    this.busy.set(true);
    this.api.start(next.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.resetAttention();
        this.refreshQueue();
      },
      error: () => {
        this.busy.set(false);
        this.refreshQueue();
      }
    });
  }

  finish(): void {
    const turn = this.current();
    const client = this.client();
    if (!turn) return;
    if (!client) {
      this.notify.error('Identifique al cliente antes de finalizar la atención.');
      return;
    }
    if (this.notes.invalid) return;

    this.busy.set(true);
    this.api.complete(turn.id, client.id, this.notes.value.trim() || null).subscribe({
      next: res => {
        this.notify.success(`${res.message} Turno ${turn.code}.`);
        this.busy.set(false);
        this.resetAttention();
        this.refreshQueue();
      },
      error: () => this.busy.set(false)
    });
  }

  noShow(): void {
    const turn = this.current();
    if (!turn) return;
    this.confirm
      .ask({ title: `Anular el turno ${turn.code}`, message: 'Úselo cuando el cliente no se presentó.', confirmText: 'Anular turno', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.api.cancel(turn.id, this.notes.value.trim() || null)))
      .subscribe(res => {
        this.notify.info(res.message);
        this.resetAttention();
        this.refreshQueue();
      });
  }

  // ---------------------------------------------------------------- cliente

  searchClient(): void {
    if (this.identification.invalid) {
      this.identification.markAsTouched();
      return;
    }
    const value = this.identification.value.trim();
    this.clientNotFound.set(null);
    this.clientsApi.byIdentification(value).subscribe({
      next: client => this.setClient(client),
      error: (err: HttpErrorResponse) => {
        this.client.set(null);
        if (err.status === 404) this.clientNotFound.set(value);
        else this.notify.error('No se pudo buscar el cliente.');
      }
    });
  }

  registerClient(): void {
    this.dialog
      .open(ClientFormDialogComponent, { ...DIALOG_DEFAULTS, width: '640px', data: { identification: this.clientNotFound() ?? '' } })
      .afterClosed()
      .subscribe((client?: Client) => {
        if (!client) return;
        this.clientNotFound.set(null);
        this.identification.setValue(client.identification);
        this.setClient(client);
      });
  }

  editClient(): void {
    const client = this.client();
    if (!client) return;
    this.dialog
      .open(ClientFormDialogComponent, { ...DIALOG_DEFAULTS, width: '640px', data: { client } })
      .afterClosed()
      .subscribe((updated?: Client) => updated && this.client.set(updated));
  }

  // ---------------------------------------------------------------- contratos

  contractAction(action: ContractAction, contract?: Contract): void {
    const client = this.client();
    if (!client) return;
    this.dialog
      .open(ContractDialogComponent, { ...DIALOG_DEFAULTS, width: action === 'new' || action === 'service' ? '620px' : '480px', data: { action, client, contract } })
      .afterClosed()
      .subscribe(done => done && this.loadClientData(client.id));
  }

  cancelContract(contract: Contract): void {
    this.confirm
      .ask({
        title: `Cancelar el contrato #${contract.id}`,
        message: `${contract.serviceName}. El contrato terminará hoy y el servicio dejará de prestarse.`,
        confirmText: 'Cancelar contrato',
        danger: true
      })
      .pipe(filter(Boolean), switchMap(() => this.api.cancelContract(contract.id)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.loadClientData(contract.clientId);
      });
  }

  // ---------------------------------------------------------------- helpers

  private startQueue(): void {
    this.refreshQueue();
    clearInterval(this.timer);
    this.timer = setInterval(() => this.refreshQueue(true), QUEUE_REFRESH_MS);
  }

  private setClient(client: Client): void {
    this.client.set(client);
    this.loadClientData(client.id);
  }

  private loadClientData(clientId: number): void {
    this.api.contracts(clientId).subscribe(c => this.contracts.set(c));
    this.api.payments(clientId).subscribe(p => this.payments.set(p.slice(0, 5)));
  }

  private resetAttention(): void {
    this.client.set(null);
    this.clientNotFound.set(null);
    this.contracts.set([]);
    this.payments.set([]);
    this.identification.reset('');
    this.notes.reset('');
  }
}
