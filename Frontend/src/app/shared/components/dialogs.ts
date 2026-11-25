import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogConfig, MatDialogRef } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { apiErrorMessage, SKIP_ERROR_TOAST } from '../../core/interceptors/http.interceptors';
import { ApiResponse, BulkUploadResult } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/app-services';
import { HttpContext } from '@angular/common/http';

export const DIALOG_DEFAULTS: MatDialogConfig = {
  panelClass: 'app-dialog',
  autoFocus: 'first-tabbable',
  maxWidth: '96vw'
};

// ------------------------------------------------------------------ confirmación

export interface ConfirmData {
  title: string;
  message: string;
  confirmText: string;
  danger?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog" role="alertdialog">
      <div class="dialog__head"><h2>{{ data.title }}</h2></div>
      <div class="dialog__body"><p class="muted" style="margin:0">{{ data.message }}</p></div>
      <div class="dialog__foot">
        <button class="btn btn--ghost" type="button" [mat-dialog-close]="false">Cancelar</button>
        <button class="btn" type="button" [class.btn--dark]="!data.danger" [class.btn--danger]="data.danger"
                [mat-dialog-close]="true" cdkFocusInitial>{{ data.confirmText }}</button>
      </div>
    </div>
  `
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
}

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

// ------------------------------------------------------------------ carga masiva

export interface BulkUploadData {
  title: string;
  /** Ruta de la API, p. ej. "/api/users/bulk". */
  endpoint: string;
  /** Columnas esperadas (se usan para la plantilla CSV). */
  columns: string[];
  /** Fila de ejemplo para la plantilla. */
  sample: string[];
  templateName: string;
}

@Component({
  selector: 'app-bulk-upload-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog">
      <div class="dialog__head">
        <h2>{{ data.title }}</h2>
        <p>Archivo .xlsx o .csv con una fila de cabecera. Las filas válidas se registran y las demás se informan abajo.</p>
      </div>

      <div class="dialog__body">
        @if (!result()) {
          <label class="drop" [class.drop--over]="dragging()" (dragover)="$event.preventDefault(); dragging.set(true)"
                 (dragleave)="dragging.set(false)" (drop)="onDrop($event)">
            <input type="file" accept=".xlsx,.csv" class="sr-only" (change)="onPick($event)" />
            <app-icon name="upload" />
            @if (file(); as f) {
              <strong>{{ f.name }}</strong>
              <span class="subtle">{{ (f.size / 1024) | number: '1.0-1' }} KB, haga clic para cambiarlo</span>
            } @else {
              <strong>Arrastre el archivo aquí o haga clic para elegirlo</strong>
              <span class="subtle">Máximo 1000 registros, 5 MB</span>
            }
          </label>

          <p class="columns">
            Columnas: @for (c of data.columns; track c) { <code>{{ c }}</code> }
          </p>
          <button class="btn btn--ghost btn--sm" type="button" (click)="downloadTemplate()">
            <app-icon name="download" /> Descargar plantilla CSV
          </button>
          @if (error()) { <p class="form-alert" style="margin-top:1rem">{{ error() }}</p> }
        } @else {
          @let r = result()!;
          <div class="summary">
            <div><span class="sign">{{ r.total }}</span><small>filas leídas</small></div>
            <div class="ok"><span class="sign">{{ r.created }}</span><small>registradas</small></div>
            <div [class.bad]="r.failed > 0"><span class="sign">{{ r.failed }}</span><small>con errores</small></div>
          </div>
          @if (r.errors.length) {
            <div class="table-wrap errors">
              <table class="table">
                <thead><tr><th>Fila</th><th>Registro</th><th>Motivo</th></tr></thead>
                <tbody>
                  @for (e of r.errors; track e.row) {
                    <tr><td>{{ e.row }}</td><td class="primary">{{ e.identifier || '—' }}</td>
                      <td>@for (m of e.messages; track $index) { <div>{{ m }}</div> }</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      </div>

      <div class="dialog__foot">
        @if (!result()) {
          <button class="btn btn--ghost" type="button" mat-dialog-close>Cancelar</button>
          <button class="btn btn--primary" type="button" [disabled]="!file() || uploading()" (click)="upload()">
            @if (uploading()) { <span class="spinner"></span> Procesando… } @else { <app-icon name="upload" /> Cargar archivo }
          </button>
        } @else {
          <button class="btn btn--dark" type="button" [mat-dialog-close]="result()">Listo</button>
        }
      </div>
    </div>
  `,
  styles: `
    .drop { display: grid; justify-items: center; gap: 0.375rem; padding: 2rem 1rem; text-align: center; cursor: pointer;
      border: 1.5px dashed var(--line); border-radius: var(--r-panel); background: #fafbfc; transition: border-color .15s, background .15s; }
    .drop:hover, .drop--over { border-color: var(--amber-deep); background: var(--amber-wash); }
    .drop app-icon { width: 26px; height: 26px; color: var(--ink-2); }
    .drop:focus-within { box-shadow: var(--focus); }
    .columns { font-size: var(--fs-sm); color: var(--ink-2); margin: 1rem 0 0.5rem; line-height: 1.9; }
    code { background: #eef1f4; padding: 0.1rem 0.4rem; border-radius: 3px; margin-right: 0.3rem; font-size: var(--fs-xs); }
    .summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1px; background: var(--line); border: 1px solid var(--line);
      border-radius: var(--r-panel); overflow: hidden; margin-bottom: 1rem; }
    .summary > div { background: var(--surface); padding: 0.875rem 1rem; display: grid; }
    .summary .sign { font-size: var(--fs-2xl); }
    .summary small { color: var(--ink-2); }
    .summary .ok .sign { color: var(--fiber); }
    .summary .bad .sign { color: var(--alert); }
    .errors { max-height: 260px; border: 1px solid var(--line); border-radius: var(--r-panel); }
  `
})
export class BulkUploadDialogComponent {
  readonly data = inject<BulkUploadData>(MAT_DIALOG_DATA);
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<BulkUploadDialogComponent>);

  readonly file = signal<File | null>(null);
  readonly dragging = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<BulkUploadResult | null>(null);

  onPick(event: Event): void {
    this.setFile((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    this.setFile(event.dataTransfer?.files?.[0] ?? null);
  }

  upload(): void {
    const file = this.file();
    if (!file) return;

    const body = new FormData();
    body.append('file', file);
    this.uploading.set(true);
    this.ref.disableClose = true;

    this.http
      .post<ApiResponse<BulkUploadResult>>(this.data.endpoint, body, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({
        next: res => {
          this.result.set(res.data!);
          this.uploading.set(false);
          this.ref.disableClose = false;
          res.data!.failed ? this.notify.info(res.message) : this.notify.success(res.message);
        },
        error: err => {
          this.error.set(apiErrorMessage(err));
          this.uploading.set(false);
          this.ref.disableClose = false;
        }
      });
  }

  downloadTemplate(): void {
    const csv = [this.data.columns.join(','), this.data.sample.map(v => `"${v}"`).join(',')].join('\r\n');
    const url = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }));
    const a = Object.assign(document.createElement('a'), { href: url, download: this.data.templateName });
    a.click();
    URL.revokeObjectURL(url);
  }

  private setFile(file: File | null): void {
    this.error.set(null);
    if (file && !/\.(xlsx|csv)$/i.test(file.name)) {
      this.error.set('Formato no soportado. Use un archivo .xlsx o .csv.');
      return;
    }
    if (file && file.size > 5 * 1024 * 1024) {
      this.error.set('El archivo supera los 5 MB.');
      return;
    }
    this.file.set(file);
  }
}
