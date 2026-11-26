import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormControl, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { apiErrorMessage } from '../../core/interceptors/api-errors';
import { InternetService } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';
import { ServicesApiService } from './services-api.service';

@Component({
  selector: 'app-service-form-dialog',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './service-form-dialog.component.html',
  styleUrl: './service-form-dialog.component.scss'
})
export class ServiceFormDialogComponent {
  readonly data = inject<InternetService | null>(MAT_DIALOG_DATA);
  private readonly api = inject(ServicesApiService);
  private readonly ref = inject(MatDialogRef<ServiceFormDialogComponent>);
  private readonly notify = inject(NotifyService);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly devices = new FormArray<FormControl<string>>(
    (this.data?.devices.length ? this.data.devices : ['']).map(d => this.deviceControl(d)));

  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.data?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data?.description ?? '', [Validators.required, Validators.maxLength(150)]],
    speedMbps: [this.data?.speedMbps ?? 100, [Validators.required, Validators.min(1), Validators.max(10000)]],
    price: [this.data?.price ?? 20, [Validators.required, Validators.min(0.01)]],
    active: [this.data?.active ?? true],
    devices: this.devices
  });

  addDevice(): void {
    this.devices.push(this.deviceControl(''));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const body = { ...this.form.getRawValue(), devices: this.devices.value.map(d => d.trim()) };
    this.saving.set(true);
    this.api.save(this.data?.id ?? null, body).subscribe({
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

  private deviceControl(value: string): FormControl<string> {
    return new FormControl(value, { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] });
  }
}
