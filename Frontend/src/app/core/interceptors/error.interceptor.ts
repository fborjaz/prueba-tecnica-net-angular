import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiResponse } from '../models/api.models';
import { AuthService } from '../services/auth.service';
import { NotifyService } from '../services/notify.service';
import { SKIP_ERROR_TOAST } from './http-context';

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
