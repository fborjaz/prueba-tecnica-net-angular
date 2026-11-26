import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { DIALOG_DEFAULTS } from './dialog-defaults';
import { ConfirmData } from './confirm-dialog.component';
import { ConfirmDialogComponent } from './confirm-dialog.component';

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  ask(data: ConfirmData): Observable<boolean> {
    return this.dialog
      .open(ConfirmDialogComponent, { ...DIALOG_DEFAULTS, width: '420px', data })
      .afterClosed()
      .pipe(map(Boolean));
  }
}
