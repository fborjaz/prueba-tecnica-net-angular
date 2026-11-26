import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { filter, interval, switchMap } from 'rxjs';
import { Cash, CatalogItem, Turn } from '../../core/models/api.models';
import { CatalogService } from '../../core/services/catalog.service';
import { NotifyService } from '../../core/services/notify.service';
import { ConfirmService } from '../../shared/components/confirm.service';
import { TurnsApiService } from './turns-api.service';

/** Cada cuánto se actualizan el estado de las cajas y la lista de turnos. */
const REFRESH_MS = 15000;

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

@Component({
  selector: 'app-turns',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './turns.component.html',
  styleUrl: './turns.component.scss'
})
export class TurnsComponent implements OnInit {
  private readonly api = inject(TurnsApiService);
  private readonly catalogs = inject(CatalogService);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly cashes = signal<Cash[]>([]);
  readonly types = signal<CatalogItem[]>([]);
  readonly turns = signal<Turn[] | null>(null);
  readonly lastTurn = signal<Turn | null>(null);
  readonly saving = signal(false);

  readonly date = signal(today());
  readonly cashFilter = signal<number | null>(null);
  readonly statusFilter = signal<number | null>(null);

  /** Solo se asignan turnos a cajas activas que un cajero tenga abiertas: si no, nadie los atendería. */
  readonly assignable = computed(() => this.cashes().filter(c => c.active && c.cashiers.length > 0 && !!c.currentSession));
  readonly hasCashiers = computed(() => this.cashes().some(c => c.active && c.cashiers.length > 0));
  readonly isToday = computed(() => this.date() === today());
  readonly counts = computed(() => {
    const list = this.turns() ?? [];
    return {
      pending: list.filter(t => t.statusId === 1).length,
      attended: list.filter(t => t.statusId === 3).length,
      total: list.length
    };
  });

  readonly form = inject(FormBuilder).group({
    cashId: [null as number | null, Validators.required],
    attentionTypeId: [null as string | null, Validators.required]
  });

  ngOnInit(): void {
    this.loadCashes();
    this.catalogs.attentionTypes().subscribe(t => this.types.set(t));
    this.load();

    // Las cajas se abren y cierran desde otras sesiones: se refresca su estado y la lista.
    interval(REFRESH_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadCashes(true);
        if (this.isToday()) this.load(true);
      });
  }

  load(silent = false): void {
    this.api.list(this.date(), this.cashFilter(), this.statusFilter(), silent).subscribe(t => this.turns.set(t));
  }

  loadCashes(silent = false): void {
    this.api.cashes(silent).subscribe(c => {
      this.cashes.set(c);
      // Si la caja elegida se cerró mientras tanto, se limpia la selección.
      const selected = this.form.controls.cashId.value;
      if (selected && !this.assignable().some(x => x.id === selected)) this.form.controls.cashId.setValue(null);
    });
  }

  /** Cajas a las que se puede mover un turno pendiente (incluye la actual aunque ya esté cerrada). */
  moveOptions(turn: Turn): { id: number; description: string }[] {
    const open = this.assignable().map(c => ({ id: c.id, description: c.description }));
    return open.some(c => c.id === turn.cashId)
      ? open
      : [{ id: turn.cashId, description: `${turn.cashDescription} (cerrada)` }, ...open];
  }

  setFilter(kind: 'date' | 'cash' | 'status', value: string | number | null): void {
    if (kind === 'date') this.date.set((value as string) || today());
    if (kind === 'cash') this.cashFilter.set(value as number | null);
    if (kind === 'status') this.statusFilter.set(value as number | null);
    this.load();
  }

  assign(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { cashId, attentionTypeId } = this.form.getRawValue();
    this.saving.set(true);
    this.api.create(cashId!, attentionTypeId!).subscribe({
      next: res => {
        this.lastTurn.set(res.data!);
        this.notify.success(res.message);
        this.saving.set(false);
        if (this.isToday()) this.load();
      },
      error: () => {
        this.saving.set(false);
        this.loadCashes();
      }
    });
  }

  move(turn: Turn, cashId: number): void {
    if (cashId === turn.cashId) return;
    this.api.move(turn.id, cashId).subscribe({
      next: res => {
        this.notify.success(res.message);
        this.load();
      },
      error: () => {
        this.load();
        this.loadCashes();
      }
    });
  }

  remove(turn: Turn): void {
    this.confirm
      .ask({ title: `Eliminar el turno ${turn.code}`, message: 'El turno se anulará y saldrá de la cola de la caja.', confirmText: 'Eliminar turno', danger: true })
      .pipe(filter(Boolean), switchMap(() => this.api.remove(turn.id)))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }

  typeLabel(id: string | null | undefined): string {
    return this.types().find(t => t.id === id)?.description ?? '';
  }
}
