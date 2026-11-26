import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Observable, debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { ApiResponse, PagedResult, ROLES, User } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { AuthService } from '../../core/services/auth.service';
import { BulkUploadDialogComponent } from '../../shared/components/bulk-upload-dialog.component';
import { ConfirmService } from '../../shared/components/confirm.service';
import { DIALOG_DEFAULTS } from '../../shared/components/dialog-defaults';
import { environment } from '../../../environments/environment';
import { UserFormDialogComponent } from './user-form-dialog.component';
import { UserFilters, UsersApiService } from './users-api.service';

@Component({
  selector: 'app-users-list',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-list.component.html'
})
export class UsersListComponent implements OnInit {
  private readonly api = inject(UsersApiService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly isAdmin = computed(() => this.auth.role() === ROLES.admin);
  readonly result = signal<PagedResult<User> | null>(null);
  readonly filters = signal<UserFilters>({ page: 1, pageSize: 10, rolId: null, statusId: null, search: '' });
  readonly search = new FormControl('', { nonNullable: true });

  ngOnInit(): void {
    this.load();
    this.search.valueChanges
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(search => this.setFilter({ search: search.trim() }));
  }

  load(): void {
    this.api.list(this.filters()).subscribe(r => this.result.set(r));
  }

  setFilter(change: Partial<UserFilters>): void {
    this.filters.update(f => ({ ...f, ...change, page: change.page ?? 1 }));
    this.load();
  }

  create(): void {
    this.openForm();
  }

  edit(user: User): void {
    this.openForm(user);
  }

  bulk(): void {
    this.dialog
      .open(BulkUploadDialogComponent, {
        ...DIALOG_DEFAULTS,
        width: '640px',
        data: {
          title: 'Carga masiva de usuarios',
          endpoint: `${environment.apiUrl}/users/bulk`,
          columns: ['usuario', 'nombres', 'apellidos', 'correo', 'identificacion', 'contrasena', 'rol'],
          sample: ['cajero2027', 'Rosa Elena', 'Quinde Vera', 'rosa.quinde@correo.com', '0956789012', 'Cajero2027', 'CAJERO'],
          templateName: 'plantilla-usuarios.csv'
        }
      })
      .afterClosed()
      .subscribe(result => result && this.load());
  }

  setStatus(user: User, statusId: 'ACT' | 'INA' | 'BLO'): void {
    const verbs = { ACT: 'Activar', INA: 'Inactivar', BLO: 'Bloquear' } as const;
    const effects = {
      ACT: 'Podrá iniciar sesión de nuevo.',
      INA: 'No podrá iniciar sesión y su sesión actual se cerrará.',
      BLO: 'No podrá iniciar sesión hasta que lo desbloquee o recupere su contraseña.'
    } as const;
    this.run(
      `${verbs[statusId]} a ${user.username}`,
      effects[statusId],
      verbs[statusId],
      () => this.api.changeStatus(user.id, statusId),
      statusId !== 'ACT'
    );
  }

  approve(user: User): void {
    this.run(`Aprobar a ${user.username}`, `Creado por ${user.createdBy}. Quedará activo y podrá iniciar sesión.`,
      'Aprobar', () => this.api.approve(user.id));
  }

  reject(user: User): void {
    this.run(`Rechazar a ${user.username}`, 'El usuario quedará inactivo y no podrá iniciar sesión.',
      'Rechazar', () => this.api.reject(user.id), true);
  }

  remove(user: User): void {
    this.run(`Eliminar a ${user.username}`, 'Dejará de aparecer en el sistema. Su historial se conserva.',
      'Eliminar', () => this.api.remove(user.id), true);
  }

  private openForm(user?: User): void {
    this.dialog
      .open(UserFormDialogComponent, { ...DIALOG_DEFAULTS, width: '640px', data: { user, isManager: !this.isAdmin() } })
      .afterClosed()
      .subscribe(saved => saved && this.load());
  }

  private run(title: string, message: string, confirmText: string, action: () => Observable<ApiResponse<unknown>>, danger = false): void {
    this.confirm
      .ask({ title, message, confirmText, danger })
      .pipe(filter(Boolean), switchMap(action))
      .subscribe(res => {
        this.notify.success(res.message);
        this.load();
      });
  }
}
