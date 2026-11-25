import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, CatalogItem, InternetService, MenuItem, MethodPayment } from '../models/api.models';

/** Contador de peticiones en curso: alimenta la barra de progreso global. */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly pending = signal(0);
  readonly active = () => this.pending() > 0;

  start(): void {
    this.pending.update(n => n + 1);
  }

  stop(): void {
    this.pending.update(n => Math.max(0, n - 1));
  }
}

/** Mensajes de éxito y error (snackbar). */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snack = inject(MatSnackBar);

  success(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 3500, panelClass: ['toast', 'toast--success'] });
  }

  error(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 6000, panelClass: ['toast', 'toast--error'] });
  }

  info(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 4000, panelClass: ['toast'] });
  }
}

/** Menú de navegación del rol, leído desde la base de datos (GET /api/menu). */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly http = inject(HttpClient);
  private cache$: Observable<MenuItem[]> | null = null;
  readonly items = signal<MenuItem[]>([]);

  load(): Observable<MenuItem[]> {
    this.cache$ ??= this.http.get<ApiResponse<MenuItem[]>>(`${environment.apiUrl}/menu`).pipe(
      map(r => r.data ?? []),
      tap(items => this.items.set(items)),
      shareReplay(1)
    );
    return this.cache$;
  }

  /** ¿La ruta está habilitada para el rol según el menú de la base de datos? */
  allows(path: string): Observable<boolean> {
    const route = '/' + path.replace(/^\//, '');
    return this.load().pipe(map(items => this.flatten(items).some(i => i.route === route)));
  }

  reset(): void {
    this.cache$ = null;
    this.items.set([]);
  }

  private flatten(items: MenuItem[]): MenuItem[] {
    return items.flatMap(i => [i, ...this.flatten(i.children ?? [])]);
  }
}

/** Catálogos compartidos entre módulos (con caché en memoria). */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;
  private readonly cache = new Map<string, Observable<unknown>>();

  roles = () => this.cached<CatalogItem[]>('catalogs/roles');
  userStatuses = () => this.cached<CatalogItem[]>('catalogs/user-statuses');
  attentionTypes = () => this.cached<CatalogItem[]>('catalogs/attention-types');

  methodPayments(): Observable<MethodPayment[]> {
    return this.http
      .get<ApiResponse<MethodPayment[]>>(`${this.api}/catalogs/method-payments`, { params: { onlyActive: true } })
      .pipe(map(r => r.data ?? []));
  }

  services(onlyActive = false): Observable<InternetService[]> {
    return this.http
      .get<ApiResponse<InternetService[]>>(`${this.api}/services`, { params: { onlyActive } })
      .pipe(map(r => r.data ?? []));
  }

  private cached<T>(path: string): Observable<T> {
    if (!this.cache.has(path)) {
      this.cache.set(path, this.http.get<ApiResponse<T>>(`${this.api}/${path}`).pipe(map(r => r.data as T), shareReplay(1)));
    }
    return (this.cache.get(path) as Observable<T>) ?? of([] as T);
  }
}
