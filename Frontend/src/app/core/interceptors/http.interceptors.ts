import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, finalize, throwError } from 'rxjs';
import { ApiResponse } from '../models/api.models';
import { AuthService } from '../services/auth.service';
import { LoadingService, NotifyService } from '../services/app-services';

import { SKIP_ERROR_TOAST, SKIP_LOADING } from './http-context';

export { SKIP_ERROR_TOAST, SKIP_LOADING };

/**
 * Adjunta a todas las peticiones el JWT y las cabeceras que necesita la API.
 */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private readonly auth = inject(AuthService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      'Accept-Language': 'es-EC',
      'X-Client-App': 'ventanilla-web',
      'X-Request-Id': crypto.randomUUID()
    };

    const token = this.auth.token;
    if (token) headers['Authorization'] = `Bearer ${token}`;

    return next.handle(req.clone({ setHeaders: headers }));
  }
}

/** Muestra el indicador de carga mientras haya peticiones en curso. */
@Injectable()
export class LoadingInterceptor implements HttpInterceptor {
  private readonly loading = inject(LoadingService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (req.context.get(SKIP_LOADING)) return next.handle(req);
    this.loading.start();
    return next.handle(req).pipe(finalize(() => this.loading.stop()));
  }
}

/** Manejo centralizado de errores: sesión expirada, sin conexión y mensajes de la API. */
@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        const isLogin = req.url.endsWith('/auth/login');
        const body = error.error as ApiResponse<unknown> | null;

        if (error.status === 401 && !isLogin) {
          this.auth.clear();
          this.notify.error('Su sesión terminó: expiró o se cerró desde otro equipo. Inicie sesión nuevamente.');
        } else if (error.status === 0) {
          this.notify.error('No hay conexión con el servidor. Verifique que la API esté disponible.');
        } else if (!req.context.get(SKIP_ERROR_TOAST)) {
          this.notify.error(body?.message ?? 'No se pudo completar la operación.');
        }

        return throwError(() => error);
      })
    );
  }
}

/** Extrae el mensaje de error de la API. */
export function apiErrorMessage(error: unknown, fallback = 'No se pudo completar la operación.'): string {
  const body = (error as HttpErrorResponse)?.error as ApiResponse<unknown> | null;
  return body?.message ?? fallback;
}

/** Errores de validación por campo devueltos por la API. */
export function apiFieldErrors(error: unknown): Record<string, string[]> {
  const body = (error as HttpErrorResponse)?.error as ApiResponse<unknown> | null;
  return body?.errors ?? {};
}
