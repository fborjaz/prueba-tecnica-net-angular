import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { SKIP_ERROR_TOAST } from '../../core/interceptors/http-context';
import { ApiResponse, BulkUploadResult } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { HttpContext } from '@angular/common/http';

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
  templateUrl: './bulk-upload-dialog.component.html',
  styleUrl: './bulk-upload-dialog.component.scss'
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
