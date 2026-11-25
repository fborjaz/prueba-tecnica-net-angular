import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { ApiResponse, LoginResponse, RoleName, UserProfile } from '../models/api.models';
import { MenuService } from './app-services';

interface StoredSession {
  token: string;
  expiresAt: string;
  user: UserProfile;
}

const STORAGE_KEY = 'ventanilla.session';

/**
 * Sesión del usuario. El JWT viene cifrado (JWE): el frontend no puede leer sus claims,
 * por eso el perfil se guarda junto al token a partir de la respuesta del login.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly menu = inject(MenuService);
  private readonly api = `${environment.apiUrl}/auth`;

  private readonly session = signal<StoredSession | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly role = computed<RoleName | null>(() => this.session()?.user.role ?? null);
  readonly isAuthenticated = computed(() => this.isValid(this.session()));

  get token(): string | null {
    return this.isValid(this.session()) ? this.session()!.token : null;
  }

  login(username: string, password: string): Observable<UserProfile> {
    return this.http.post<ApiResponse<LoginResponse>>(`${this.api}/login`, { username, password }, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true)
    }).pipe(
      map(res => res.data!),
      tap(data => this.store({ token: data.token, expiresAt: data.expiresAt, user: data.user })),
      map(data => data.user)
    );
  }

  logout(): void {
    if (!this.token) {
      this.clear();
      return;
    }
    this.http.post(`${this.api}/logout`, {}).pipe(finalize(() => this.clear())).subscribe({ error: () => void 0 });
  }

  /** Limpia la sesión local y vuelve al login (también se usa cuando la API responde 401). */
  clear(redirect = true): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    this.menu.reset();
    if (redirect) this.router.navigate(['/auth/login']);
  }

  refreshProfile(): Observable<UserProfile | null> {
    if (!this.token) return of(null);
    return this.http.get<ApiResponse<UserProfile>>(`${this.api}/me`).pipe(
      map(res => res.data!),
      tap(user => {
        const current = this.session();
        if (current) this.store({ ...current, user });
      })
    );
  }

  forgotPassword(email: string): Observable<string> {
    return this.http.post<ApiResponse<null>>(`${this.api}/forgot-password`, { email }).pipe(map(r => r.message));
  }

  resetPassword(token: string, newPassword: string, confirmPassword: string): Observable<string> {
    return this.http
      .post<ApiResponse<null>>(`${this.api}/reset-password`, { token, newPassword, confirmPassword }, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, true)
      })
      .pipe(map(r => r.message));
  }

  hasRole(...roles: RoleName[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  private store(session: StoredSession): void {
    // sessionStorage: la sesión no sobrevive al cierre del navegador.
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): StoredSession | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      const session = raw ? (JSON.parse(raw) as StoredSession) : null;
      return this.isValid(session) ? session : null;
    } catch {
      return null;
    }
  }

  private isValid(session: StoredSession | null): boolean {
    return !!session && new Date(session.expiresAt).getTime() > Date.now();
  }
}
