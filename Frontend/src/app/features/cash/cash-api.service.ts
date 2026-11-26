import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { ApiResponse, Cash, PagedResult, User } from '../../core/models/api.models';

@Injectable({ providedIn: 'root' })
export class CashApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/cash`;

  list = () => this.http.get<ApiResponse<Cash[]>>(this.api).pipe(map(r => r.data ?? []));
  create = (body: { description: string; active: boolean }) =>
    this.http.post<ApiResponse<Cash>>(this.api, body, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) });
  update = (id: number, body: { description: string; active: boolean }) =>
    this.http.put<ApiResponse<Cash>>(`${this.api}/${id}`, body, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) });
  remove = (id: number) => this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
  assign = (id: number, userId: number) => this.http.post<ApiResponse<Cash>>(`${this.api}/${id}/cashiers`, { userId });
  unassign = (id: number, userId: number) => this.http.delete<ApiResponse<Cash>>(`${this.api}/${id}/cashiers/${userId}`);

  /** Cajeros activos disponibles para asignar. */
  cashiers(): Observable<User[]> {
    return this.http
      .get<ApiResponse<PagedResult<User>>>(`${environment.apiUrl}/users`, { params: { rolId: 3, statusId: 'ACT', pageSize: 100 } })
      .pipe(map(r => r.data?.items ?? []));
  }
}
