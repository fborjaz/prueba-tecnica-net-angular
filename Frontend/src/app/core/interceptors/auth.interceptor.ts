import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';

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
