import { TestBed } from '@angular/core/testing';
import { Route, Router, UrlSegment, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { RoleName } from '../models/api.models';
import { AuthService } from '../services/auth.service';
import { MenuService } from '../services/menu.service';
import { NotifyService } from '../services/notify.service';
import { authGuard, guestGuard, roleGuard } from './auth.guards';

describe('Guards de rutas', () => {
  let session: { authenticated: boolean; role: RoleName };
  let allowedRoutes: Observable<boolean>;
  const notify = { error: vi.fn() };

  beforeEach(() => {
    session = { authenticated: true, role: 'CAJERO' };
    allowedRoutes = of(true);
    notify.error.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => session.authenticated,
            hasRole: (...roles: RoleName[]) => roles.includes(session.role)
          }
        },
        { provide: MenuService, useValue: { allows: () => allowedRoutes } },
        { provide: NotifyService, useValue: notify }
      ]
    });
  });

  /** Ejecuta un guard y devuelve la URL de redirección o `true`. */
  async function run(guard: () => unknown): Promise<string | boolean> {
    let result = TestBed.runInInjectionContext(guard);
    if (isObservable(result)) result = await firstValueFrom(result);
    return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : (result as boolean);
  }

  const route = (roles: RoleName[]): Route => ({ path: 'dashboard', data: { roles } });
  const segments = [new UrlSegment('dashboard', {})];

  describe('authGuard', () => {
    it('deja pasar con sesión', async () => {
      expect(await run(() => authGuard({} as never, {} as never))).toBe(true);
    });

    it('sin sesión redirige al login', async () => {
      session.authenticated = false;
      expect(await run(() => authGuard({} as never, {} as never))).toBe('/auth/login');
    });
  });

  describe('guestGuard', () => {
    it('con sesión no deja volver al login', async () => {
      expect(await run(() => guestGuard({} as never, []))).toBe('/welcome');
    });

    it('sin sesión muestra el login', async () => {
      session.authenticated = false;
      expect(await run(() => guestGuard({} as never, []))).toBe(true);
    });
  });

  describe('roleGuard', () => {
    it('deja pasar si el rol está permitido y la ruta está en su menú', async () => {
      session.role = 'ADMINISTRADOR';
      expect(await run(() => roleGuard(route(['ADMINISTRADOR']), segments))).toBe(true);
    });

    it('bloquea si el rol no está permitido, sin consultar el menú', async () => {
      const allows = vi.spyOn(TestBed.inject(MenuService), 'allows');

      expect(await run(() => roleGuard(route(['ADMINISTRADOR']), segments))).toBe('/welcome');
      expect(allows).not.toHaveBeenCalled();
      expect(notify.error).toHaveBeenCalledWith('No tiene acceso a esa sección.');
    });

    it('bloquea si la ruta no está en el menú que la base asigna al rol', async () => {
      allowedRoutes = of(false);
      expect(await run(() => roleGuard(route(['CAJERO']), segments))).toBe('/welcome');
    });

    it('si falla la carga del menú, niega el acceso', async () => {
      allowedRoutes = throwError(() => new Error('sin conexión'));
      expect(await run(() => roleGuard(route(['CAJERO']), segments))).toBe('/welcome');
    });

    it('sin sesión redirige al login', async () => {
      session.authenticated = false;
      expect(await run(() => roleGuard(route(['CAJERO']), segments))).toBe('/auth/login');
    });
  });
});
