import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_LOADING } from '../../core/interceptors/http-context';
import { ApiResponse, Cash, Turn } from '../../core/models/api.models';

@Injectable({ providedIn: 'root' })
export class TurnsApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/turns`;

  list(date: string, cashId: number | null, statusId: number | null, silent = false) {
    let params = new HttpParams().set('date', date);
    if (cashId) params = params.set('cashId', cashId);
    if (statusId) params = params.set('statusId', statusId);
    return this.http.get<ApiResponse<Turn[]>>(this.api, { params, context: quiet(silent) }).pipe(map(r => r.data ?? []));
  }

  create = (cashId: number, attentionTypeId: string) =>
    this.http.post<ApiResponse<Turn>>(this.api, { cashId, attentionTypeId });
  move = (id: number, cashId: number) => this.http.put<ApiResponse<Turn>>(`${this.api}/${id}`, { cashId });
  remove = (id: number) => this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
  cashes = (silent = false) =>
    this.http.get<ApiResponse<Cash[]>>(`${environment.apiUrl}/cash`, { context: quiet(silent) }).pipe(map(r => r.data ?? []));
}

/** Los refrescos automáticos no muestran la barra de carga global. */
const quiet = (silent: boolean) => new HttpContext().set(SKIP_LOADING, silent);
