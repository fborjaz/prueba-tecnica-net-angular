import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, CatalogItem, InternetService, MethodPayment } from '../models/api.models';

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
