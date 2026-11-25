import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { controlError } from '../../core/utils/validators';

/** Encabezado de página: título en tipografía de señalética, descripción y acciones. */
@Component({
  selector: 'app-page-header',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1 class="sign">{{ title() }}</h1>
        @if (description()) { <p>{{ description() }}</p> }
      </div>
      <div class="page-header__actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .page-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 1rem 2rem;
      flex-wrap: wrap; margin-bottom: 1.5rem; }
    h1 { font-size: var(--fs-2xl); font-weight: 800; }
    p { margin: 0.375rem 0 0; color: var(--ink-2); max-width: 62ch; }
    .page-header__actions { display: flex; gap: 0.625rem; flex-wrap: wrap; }
  `
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
}

/** Mensaje de error de un control reactivo (validación en el cliente o devuelta por la API). */
@Component({
  selector: 'app-field-error',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (message(); as m) { <span class="field-error" role="alert">{{ m }}</span> }`
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

const STATUS_VARIANTS: Record<string, string> = {
  ACT: 'ok', VIG: 'ok', REN: 'ok', true: 'ok', '3': 'ok',
  PEN: 'pending', '1': 'pending',
  BLO: 'danger', CAN: 'danger', '4': 'danger',
  SUS: 'warn', '2': 'warn',
  INA: 'neutral', false: 'neutral'
};

/** Ficha de estado con color según el código (usuarios, contratos, atenciones). */
@Component({
  selector: 'app-status',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [class]="'chip chip--' + variant()">{{ label() }}</span>`
})
export class StatusChipComponent {
  readonly code = input.required<string | number | boolean>();
  readonly label = input.required<string>();
  readonly variant = computed(() => STATUS_VARIANTS[String(this.code())] ?? 'neutral');
}

/** Estado vacío con una indicación de qué hacer. */
@Component({
  selector: 'app-empty',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      <app-icon [name]="icon()" />
      <p class="empty__title">{{ title() }}</p>
      @if (hint()) { <p class="empty__hint">{{ hint() }}</p> }
      <ng-content />
    </div>
  `,
  styles: `
    .empty { display: grid; justify-items: center; gap: 0.375rem; padding: 2.5rem 1rem; text-align: center; color: var(--ink-2); }
    app-icon { width: 28px; height: 28px; color: var(--ink-3); margin-bottom: 0.25rem; }
    .empty__title { margin: 0; font-weight: 600; color: var(--ink); }
    .empty__hint { margin: 0 0 0.5rem; font-size: var(--fs-sm); max-width: 44ch; }
  `
})
export class EmptyStateComponent {
  readonly icon = input('search');
  readonly title = input.required<string>();
  readonly hint = input<string>();
}
