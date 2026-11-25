import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST, SKIP_LOADING } from '../../core/interceptors/http-context';
import { ApiResponse, Cash, CashSession, Contract, Payment, Turn } from '../../core/models/api.models';

/** Endpoints que usa el cajero: caja, cola de turnos, contratos y pagos. */
@Injectable({ providedIn: 'root' })
export class CashierApiService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  // Caja
  myCashes = () => this.http.get<ApiResponse<Cash[]>>(`${this.api}/cash/mine`).pipe(map(r => r.data ?? []));
  session = () => this.http.get<ApiResponse<CashSession | null>>(`${this.api}/cash/session`).pipe(map(r => r.data ?? null));
  open = (cashId: number) => this.http.post<ApiResponse<CashSession>>(`${this.api}/cash/${cashId}/open`, {});
  close = () => this.http.post<ApiResponse<null>>(`${this.api}/cash/session/close`, {});

  // Turnos
  queue = (silent = false) =>
    this.http
      .get<ApiResponse<Turn[]>>(`${this.api}/turns/queue`, {
        context: new HttpContext().set(SKIP_LOADING, silent).set(SKIP_ERROR_TOAST, silent)
      })
      .pipe(map(r => r.data ?? []));
  start = (turnId: number) => this.http.post<ApiResponse<Turn>>(`${this.api}/turns/${turnId}/start`, {});
  complete = (turnId: number, clientId: number, notes: string | null) =>
    this.http.post<ApiResponse<Turn>>(`${this.api}/turns/${turnId}/complete`, { clientId, notes });
  cancel = (turnId: number, notes: string | null) =>
    this.http.post<ApiResponse<Turn>>(`${this.api}/turns/${turnId}/cancel`, { notes });

  // Contratos y pagos
  contracts = (clientId: number) =>
    this.http.get<ApiResponse<Contract[]>>(`${this.api}/contracts`, { params: new HttpParams().set('clientId', clientId) })
      .pipe(map(r => r.data ?? []));
  payments = (clientId: number) =>
    this.http.get<ApiResponse<Payment[]>>(`${this.api}/payments`, { params: new HttpParams().set('clientId', clientId) })
      .pipe(map(r => r.data ?? []));

  private readonly quiet = () => new HttpContext().set(SKIP_ERROR_TOAST, true);
  createContract = (body: { clientId: number; serviceId: number; methodPaymentId: number; months: number }) =>
    this.http.post<ApiResponse<Contract>>(`${this.api}/contracts`, body, { context: this.quiet() });
  changeService = (id: number, newServiceId: number) =>
    this.http.put<ApiResponse<Contract>>(`${this.api}/contracts/${id}/service`, { newServiceId }, { context: this.quiet() });
  changeMethod = (id: number, methodPaymentId: number) =>
    this.http.put<ApiResponse<Contract>>(`${this.api}/contracts/${id}/payment-method`, { methodPaymentId }, { context: this.quiet() });
  cancelContract = (id: number) => this.http.put<ApiResponse<Contract>>(`${this.api}/contracts/${id}/cancel`, {});
  pay = (body: { contractId: number; amount: number | null; methodPaymentId: number | null }) =>
    this.http.post<ApiResponse<Payment>>(`${this.api}/payments`, body, { context: this.quiet() });
}
