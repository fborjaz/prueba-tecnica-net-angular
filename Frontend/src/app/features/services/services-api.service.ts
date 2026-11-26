import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { ApiResponse, InternetService } from '../../core/models/api.models';

/** Planes de internet (administrador). */
@Injectable({ providedIn: 'root' })
export class ServicesApiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/services`;

  /** El formulario muestra los errores por campo, así que no se lanza el toast global. */
  save(id: number | null, body: unknown) {
    const options = { context: new HttpContext().set(SKIP_ERROR_TOAST, true) };
    return id
      ? this.http.put<ApiResponse<InternetService>>(`${this.api}/${id}`, body, options)
      : this.http.post<ApiResponse<InternetService>>(this.api, body, options);
  }

  remove = (id: number) => this.http.delete<ApiResponse<null>>(`${this.api}/${id}`);
}
