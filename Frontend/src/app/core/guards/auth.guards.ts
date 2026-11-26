import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Route, Router, UrlSegment } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { RoleName } from '../models/api.models';
import { MenuService } from '../services/menu.service';
import { NotifyService } from '../services/notify.service';
import { AuthService } from '../services/auth.service';

/** Exige sesión válida; si no, redirige al login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/auth/login']);
};

/** Las pantallas de autenticación solo son accesibles sin sesión. */
export const guestGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree(['/welcome']) : true;
};

/**
 * Protección por rol en dos niveles:
 * 1. El rol del usuario debe estar en `data.roles` de la ruta.
 * 2. La ruta debe existir en el menú que la base de datos asigna a ese rol.
 * Al usar canMatch, el módulo ni siquiera se descarga si el usuario no tiene acceso.
 */
export const roleGuard: CanMatchFn = (route: Route, segments: UrlSegment[]) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const menu = inject(MenuService);
  const notify = inject(NotifyService);

  const roles = (route.data?.['roles'] as RoleName[] | undefined) ?? [];
  const denied = () => {
    notify.error('No tiene acceso a esa sección.');
    return router.createUrlTree(['/welcome']);
  };

  if (!auth.isAuthenticated()) return router.createUrlTree(['/auth/login']);
  if (roles.length && !auth.hasRole(...roles)) return denied();

  const path = segments[0]?.path ?? route.path ?? '';
  return menu.allows(path).pipe(
    map(allowed => (allowed ? true : denied())),
    catchError(() => of(denied()))
  );
};
