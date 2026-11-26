import { Injectable, signal } from '@angular/core';

/** Contador de peticiones en curso: alimenta la barra de progreso global. */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly pending = signal(0);
  readonly active = () => this.pending() > 0;

  start(): void {
    this.pending.update(n => n + 1);
  }

  stop(): void {
    this.pending.update(n => Math.max(0, n - 1));
  }
}
