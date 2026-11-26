import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { Cash } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { CashApiService } from './cash-api.service';

@Component({
  selector: 'app-cash-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cash-form-dialog.component.html',
  styleUrl: './cash-form-dialog.component.scss'
})
export class CashFormDialogComponent {
  readonly data = inject<Cash | null>(MAT_DIALOG_DATA);
  private readonly api = inject(CashApiService);
  private readonly ref = inject(MatDialogRef<CashFormDialogComponent>);
  private readonly notify = inject(NotifyService);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    description: [this.data?.description ?? '', [Validators.required, Validators.maxLength(50)]],
    active: [this.data?.active ?? true]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const body = { ...this.form.getRawValue(), description: this.form.getRawValue().description.trim() };
    this.saving.set(true);
    (this.data ? this.api.update(this.data.id, body) : this.api.create(body)).subscribe({
      next: res => {
        this.notify.success(res.message);
        this.ref.close(true);
      },
      error: err => {
        this.error.set(apiErrorMessage(err));
        this.saving.set(false);
      }
    });
  }
}
