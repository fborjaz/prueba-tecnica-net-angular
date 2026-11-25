import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { ApiResponse, Client, PagedResult, SaveClientRequest } from '../models/api.models';

/** Cliente HTTP de /api/clients (lo usan Clientes y Procesos de caja). */
@Injectable({ providedIn: 'root' })
export class ClientsApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/clients`;
  private readonly quiet = () => new HttpContext().set(SKIP_ERROR_TOAST, true);

  list(search: string, active: boolean | null, page: number, pageSize = 10): Observable<PagedResult<Client>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (active !== null) params = params.set('active', active);
    return this.http.get<ApiResponse<PagedResult<Client>>>(this.api, { params }).pipe(map(r => r.data!));
  }

  /** Búsqueda por identificación. El 404 se maneja en la pantalla (cliente no registrado). */
  byIdentification(identification: string): Observable<Client> {
    return this.http
      .get<ApiResponse<Client>>(`${this.api}/identification/${encodeURIComponent(identification)}`, { context: this.quiet() })
      .pipe(map(r => r.data!));
  }

  create = (body: SaveClientRequest) => this.http.post<ApiResponse<Client>>(this.api, body, { context: this.quiet() });
  update = (id: number, body: SaveClientRequest) => this.http.put<ApiResponse<Client>>(`${this.api}/${id}`, body, { context: this.quiet() });
  setActive = (id: number, active: boolean) => this.http.patch<ApiResponse<Client>>(`${this.api}/${id}/status`, { active });
  remove = (id: number) => this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
}
