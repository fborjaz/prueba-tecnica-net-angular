import { HttpErrorResponse } from '@angular/common/http';
import { ApiResponse } from '../models/api.models';

/** Extrae el mensaje de error de la API. */
export function apiErrorMessage(error: unknown, fallback = 'No se pudo completar la operación.'): string {
  const body = (error as HttpErrorResponse)?.error as ApiResponse<unknown> | null;
  return body?.message ?? fallback;
}

/** Errores de validación por campo devueltos por la API. */
export function apiFieldErrors(error: unknown): Record<string, string[]> {
  const body = (error as HttpErrorResponse)?.error as ApiResponse<unknown> | null;
  return body?.errors ?? {};
}
