import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Mensajes de éxito y error (snackbar). */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snack = inject(MatSnackBar);

  success(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 3500, panelClass: ['toast', 'toast--success'] });
  }

  error(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 6000, panelClass: ['toast', 'toast--error'] });
  }

  info(message: string): void {
    this.snack.open(message, 'Cerrar', { duration: 4000, panelClass: ['toast'] });
  }
}
