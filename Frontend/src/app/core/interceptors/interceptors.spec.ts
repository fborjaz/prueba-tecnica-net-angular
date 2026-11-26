import { HTTP_INTERCEPTORS, HttpClient, HttpContext, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../services/auth.service';
import { NotifyService } from '../services/notify.service';
import { AuthInterceptor } from './auth.interceptor';
import { ErrorInterceptor } from './error.interceptor';
import { SKIP_ERROR_TOAST } from './http-context';

describe('Interceptores HTTP', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const auth = { token: null as string | null, clear: vi.fn() };
  const notify = { error: vi.fn() };

  beforeEach(() => {
    auth.token = null;
    auth.clear.mockClear();
    notify.error.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: AuthService, useValue: auth },
        { provide: NotifyService, useValue: notify }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  describe('AuthInterceptor', () => {
    it('adjunta el JWT y las cabeceras de la aplicación', () => {
      auth.token = 'token-jwe';
      http.get('/api/users').subscribe();

      const req = backend.expectOne('/api/users');
      expect(req.request.headers.get('Authorization')).toBe('Bearer token-jwe');
      expect(req.request.headers.get('Accept-Language')).toBe('es-EC');
      expect(req.request.headers.get('X-Client-App')).toBe('ventanilla-web');
      expect(req.request.headers.get('X-Request-Id')).toBeTruthy();
      req.flush({});
    });

    it('sin sesión no envía Authorization', () => {
      http.get('/api/auth/login').subscribe();

      const req = backend.expectOne('/api/auth/login');
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({});
    });
  });

  describe('ErrorInterceptor', () => {
    const fail = (url: string, status: number, body: object | null = null, context?: HttpContext) => {
      http.get(url, { context }).subscribe({ error: () => undefined });
      backend.expectOne(url).flush(body, { status, statusText: 'Error' });
    };

    it('un 401 cierra la sesión y avisa', () => {
      fail('/api/users', 401);

      expect(auth.clear).toHaveBeenCalled();
      expect(notify.error).toHaveBeenCalledWith(expect.stringContaining('Su sesión terminó'));
    });

    it('un 401 del login no cierra la sesión (el formulario muestra el error)', () => {
      fail('/api/auth/login', 401);

      expect(auth.clear).not.toHaveBeenCalled();
    });

    it('muestra el mensaje que devuelve la API', () => {
      fail('/api/cash/1/open', 409, { success: false, message: 'La caja está siendo utilizada por cajero01.' });

      expect(notify.error).toHaveBeenCalledWith('La caja está siendo utilizada por cajero01.');
    });

    it('respeta SKIP_ERROR_TOAST cuando el formulario maneja el error', () => {
      fail('/api/users', 400, { message: 'Datos inválidos' }, new HttpContext().set(SKIP_ERROR_TOAST, true));

      expect(notify.error).not.toHaveBeenCalled();
    });
  });
});
