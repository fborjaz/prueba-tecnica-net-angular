import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { NgModule, Optional, SkipSelf } from '@angular/core';
import { AuthInterceptor, ErrorInterceptor, LoadingInterceptor } from './interceptors/http.interceptors';

/**
 * Módulo núcleo: HttpClient e interceptores (se importa una sola vez en AppModule).
 * Orden: AuthInterceptor (JWT y cabeceras) → LoadingInterceptor → ErrorInterceptor.
 */
@NgModule({
  providers: [
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: LoadingInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true }
  ]
})
export class CoreModule {
  constructor(@Optional() @SkipSelf() parent?: CoreModule) {
    if (parent) throw new Error('CoreModule ya fue cargado. Impórtelo solo en AppModule.');
  }
}
