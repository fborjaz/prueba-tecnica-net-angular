import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';
import { SKIP_LOADING } from './http-context';

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
