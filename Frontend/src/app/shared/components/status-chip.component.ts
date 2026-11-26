import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

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
  templateUrl: './status-chip.component.html'
})
export class StatusChipComponent {
  readonly code = input.required<string | number | boolean>();
  readonly label = input.required<string>();
  readonly variant = computed(() => STATUS_VARIANTS[String(this.code())] ?? 'neutral');
}
