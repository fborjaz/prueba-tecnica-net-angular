import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { ApiResponse, PagedResult, SaveUserRequest, User } from '../../core/models/api.models';

export interface UserFilters {
  search?: string;
  rolId?: number | null;
  statusId?: string | null;
  page: number;
  pageSize: number;
}

/** Cliente HTTP del CRUD de usuarios (/api/users). */
@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/users`;
  private readonly formContext = () => new HttpContext().set(SKIP_ERROR_TOAST, true);

  list(filters: UserFilters): Observable<PagedResult<User>> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.search) params = params.set('search', filters.search);
    if (filters.rolId) params = params.set('rolId', filters.rolId);
    if (filters.statusId) params = params.set('statusId', filters.statusId);
    return this.http.get<ApiResponse<PagedResult<User>>>(this.api, { params }).pipe(map(r => r.data!));
  }

  create(body: SaveUserRequest): Observable<ApiResponse<User>> {
    return this.http.post<ApiResponse<User>>(this.api, body, { context: this.formContext() });
  }

  update(id: number, body: SaveUserRequest): Observable<ApiResponse<User>> {
    return this.http.put<ApiResponse<User>>(`${this.api}/${id}`, body, { context: this.formContext() });
  }

  changeStatus(id: number, statusId: string): Observable<ApiResponse<User>> {
    return this.http.patch<ApiResponse<User>>(`${this.api}/${id}/status`, { statusId });
  }

  approve(id: number): Observable<ApiResponse<User>> {
    return this.http.post<ApiResponse<User>>(`${this.api}/${id}/approve`, {});
  }

  reject(id: number): Observable<ApiResponse<User>> {
    return this.http.post<ApiResponse<User>>(`${this.api}/${id}/reject`, {});
  }

  remove(id: number): Observable<ApiResponse<null>> {
    return this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
  }
}
