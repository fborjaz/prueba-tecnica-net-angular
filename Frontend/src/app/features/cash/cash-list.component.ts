import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { filter, switchMap } from 'rxjs';
import { Cash, ROLES, User } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { AuthService } from '../../core/services/auth.service';
import { ConfirmService } from '../../shared/components/confirm.service';
import { DIALOG_DEFAULTS } from '../../shared/components/dialog-defaults';
import { CashApiService } from './cash-api.service';
import { CashFormDialogComponent } from './cash-form-dialog.component';

const MAX_CASHIERS = 2;

@Component({
  selector: 'app-cash-list',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cash-list.component.html',
  styleUrl: './cash-list.component.scss'
})
export class CashListComponent implements OnInit {
  private readonly api = inject(CashApiService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);

  readonly max = MAX_CASHIERS;
  readonly isAdmin = computed(() => this.auth.role() === ROLES.admin);
  readonly cashes = signal<Cash[] | null>(null);
  readonly cashiers = signal<User[]>([]);
  /** Cajero seleccionado para asignar, por caja. */
  readonly selection = signal<Record<number, number | null>>({});

  ngOnInit(): void {
    this.load();
    this.api.cashiers().subscribe(c => this.cashiers.set(c));
  }

  load(): void {
    this.api.list().subscribe(c => this.cashes.set(c));
  }

  available(cash: Cash): User[] {
    const assigned = new Set(cash.cashiers.map(c => c.userId));
    return this.cashiers().filter(u => !assigned.has(u.id));
  }

  select(cashId: number, userId: number | null): void {
    this.selection.update(s => ({ ...s, [cashId]: userId }));
  }

  assign(cash: Cash): void {
    const userId = this.selection()[cash.id];
    if (!userId) return;
    this.api.assign(cash.id, userId).subscribe(res => {
      this.notify.success(res.message);
      this.select(cash.id, null);
      this.load();
    });
  }

  unassign(cash: Cash, userId: number, username: string): void {
    this.confirm
      .ask({ title: `Quitar a ${username}`, message: `Dejará de poder abrir ${cash.description}.`, confirmText: 'Quitar asignación', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.api.unassign(cash.id, userId)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }

  openForm(cash?: Cash): void {
    this.dialog
      .open(CashFormDialogComponent, { ...DIALOG_DEFAULTS, width: '460px', data: cash ?? null })
      .afterClosed()
      .subscribe(saved => saved && this.load());
  }

  remove(cash: Cash): void {
    this.confirm
      .ask({ title: `Eliminar ${cash.description}`, message: 'Se quitarán también sus asignaciones de cajeros.', confirmText: 'Eliminar', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.api.remove(cash.id)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }
}
