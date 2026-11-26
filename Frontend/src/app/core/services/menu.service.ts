import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, MenuItem } from '../models/api.models';

/** Menú de navegación del rol, leído desde la base de datos (GET /api/menu). */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly http = inject(HttpClient);
  private cache$: Observable<MenuItem[]> | null = null;
  readonly items = signal<MenuItem[]>([]);

  load(): Observable<MenuItem[]> {
    this.cache$ ??= this.http.get<ApiResponse<MenuItem[]>>(`${environment.apiUrl}/menu`).pipe(
      map(r => r.data ?? []),
      tap(items => this.items.set(items)),
      shareReplay(1)
    );
    return this.cache$;
  }

  /** ¿La ruta está habilitada para el rol según el menú de la base de datos? */
  allows(path: string): Observable<boolean> {
    const route = '/' + path.replace(/^\//, '');
    return this.load().pipe(map(items => this.flatten(items).some(i => i.route === route)));
  }

  reset(): void {
    this.cache$ = null;
    this.items.set([]);
  }

  private flatten(items: MenuItem[]): MenuItem[] {
    return items.flatMap(i => [i, ...this.flatten(i.children ?? [])]);
  }
}
