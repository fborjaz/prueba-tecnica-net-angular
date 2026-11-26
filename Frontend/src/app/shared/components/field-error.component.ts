import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { controlError } from '../../core/utils/validators';

/** Mensaje de error de un control reactivo (validación en el cliente o devuelta por la API). */
@Component({
  selector: 'app-field-error',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './field-error.component.html'
})
export class FieldErrorComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  readonly control = input.required<AbstractControl | null>();
  private readonly tick = signal(0);

  readonly message = computed(() => {
    this.tick();
    return controlError(this.control());
  });

  ngOnInit(): void {
    // Recalcula al cambiar valor o estado del control (la app es zoneless y usa signals).
    this.control()?.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.tick.update(n => n + 1));
  }
}
