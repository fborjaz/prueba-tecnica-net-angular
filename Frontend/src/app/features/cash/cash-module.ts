import { HttpClient, HttpContext } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, Injectable, NgModule, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { RouterModule, Routes } from '@angular/router';
import { Observable, filter, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { apiErrorMessage } from '../../core/interceptors/http.interceptors';
import { ApiResponse, Cash, PagedResult, ROLES, User } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
import { AuthService } from '../../core/services/auth.service';
import { ConfirmService, DIALOG_DEFAULTS } from '../../shared/components/dialogs';
import { SharedModule } from '../../shared/shared-module';

const MAX_CASHIERS = 2;

@Injectable({ providedIn: 'root' })
export class CashApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/cash`;

  list = () => this.http.get<ApiResponse<Cash[]>>(this.api).pipe(map(r => r.data ?? []));
  create = (body: { description: string; active: boolean }) =>
    this.http.post<ApiResponse<Cash>>(this.api, body, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) });
  update = (id: number, body: { description: string; active: boolean }) =>
    this.http.put<ApiResponse<Cash>>(`${this.api}/${id}`, body, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) });
  remove = (id: number) => this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
  assign = (id: number, userId: number) => this.http.post<ApiResponse<Cash>>(`${this.api}/${id}/cashiers`, { userId });
  unassign = (id: number, userId: number) => this.http.delete<ApiResponse<Cash>>(`${this.api}/${id}/cashiers/${userId}`);

  /** Cajeros activos disponibles para asignar. */
  cashiers(): Observable<User[]> {
    return this.http
      .get<ApiResponse<PagedResult<User>>>(`${environment.apiUrl}/users`, { params: { rolId: 3, statusId: 'ACT', pageSize: 100 } })
      .pipe(map(r => r.data?.items ?? []));
  }
}

// ------------------------------------------------------------------ formulario de caja

@Component({
  selector: 'app-cash-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="dialog__head"><h2>{{ data ? 'Editar caja' : 'Nueva caja' }}</h2></div>
      <div class="dialog__body" style="display:grid;gap:1rem">
        @if (error()) { <div class="form-alert" role="alert">{{ error() }}</div> }
        <div class="field">
          <label for="c-desc">Descripción</label>
          <input id="c-desc" class="input" formControlName="description" maxlength="50" placeholder="Caja 04 - Sucursal Sur" />
          <app-field-error [control]="form.controls.description" />
        </div>
        <label class="check"><input type="checkbox" formControlName="active" /> Caja habilitada para atender</label>
      </div>
      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          @if (saving()) { <span class="spinner"></span> Guardando… } @else { {{ data ? 'Guardar cambios' : 'Crear caja' }} }
        </button>
      </div>
    </form>
  `,
  styles: `.check { display: flex; gap: 0.5rem; align-items: center; font-size: var(--fs-sm); }`
})
export class CashFormDialogComponent {
  readonly data = inject<Cash | null>(MAT_DIALOG_DATA);
  private readonly api = inject(CashApiService);
  private readonly ref = inject(MatDialogRef<CashFormDialogComponent>);
  private readonly notify = inject(NotifyService);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    description: [this.data?.description ?? '', [Validators.required, Validators.maxLength(50)]],
    active: [this.data?.active ?? true]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const body = { ...this.form.getRawValue(), description: this.form.getRawValue().description.trim() };
    this.saving.set(true);
    (this.data ? this.api.update(this.data.id, body) : this.api.create(body)).subscribe({
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

// ------------------------------------------------------------------ listado

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

const routes: Routes = [{ path: '', component: CashListComponent, title: 'Cajas' }];

/** Cajas y asignación de cajeros (administrador y gestor). */
@NgModule({
  declarations: [CashListComponent, CashFormDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class CashModule {}
