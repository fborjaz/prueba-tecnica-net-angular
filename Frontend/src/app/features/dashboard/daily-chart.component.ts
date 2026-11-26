import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

export interface DailyPoint {
  day: string;
  turnsGenerated: number;
  turnsAttended: number;
}

const W = 720;
const H = 240;
const PAD = { top: 16, right: 86, bottom: 28, left: 36 };

/**
 * Serie diaria de turnos generados vs. atendidos.
 * Azul pizarra y ámbar: se distinguen también con daltonismo (protanopia y tritanopia).
 * El ámbar queda bajo 3:1 de contraste: se compensa con etiquetas directas y la vista de tabla.
 */
@Component({
  selector: 'app-daily-chart',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './daily-chart.component.html',
  styleUrl: './daily-chart.component.scss'
})
export class DailyChartComponent {
  readonly points = input.required<DailyPoint[]>();
  readonly showTable = signal(false);
  readonly hover = signal<number | null>(null);
  readonly w = W;
  readonly h = H;
  readonly pad = PAD;
  readonly Math = Math;

  private readonly max = computed(() => {
    const m = Math.max(1, ...this.points().flatMap(p => [p.turnsGenerated, p.turnsAttended]));
    const magnitude = 10 ** Math.floor(Math.log10(m));
    return Math.ceil(m / magnitude) * magnitude;
  });

  readonly step = computed(() => (W - PAD.left - PAD.right) / Math.max(1, this.points().length - 1));

  readonly coords = computed(() => {
    const max = this.max();
    const innerH = H - PAD.top - PAD.bottom;
    const n = this.points().length;
    return this.points().map((p, i) => ({
      i,
      day: p.day,
      x: PAD.left + (n === 1 ? (W - PAD.left - PAD.right) / 2 : i * this.step()),
      yg: PAD.top + innerH * (1 - p.turnsGenerated / max),
      ya: PAD.top + innerH * (1 - p.turnsAttended / max),
      g: p.turnsGenerated,
      a: p.turnsAttended
    }));
  });

  readonly ticks = computed(() => {
    const max = this.max();
    const innerH = H - PAD.top - PAD.bottom;
    return [0, 0.25, 0.5, 0.75, 1].map(f => ({ value: Math.round(max * f), y: PAD.top + innerH * (1 - f) }))
      .filter((t, i, arr) => i === 0 || t.value !== arr[i - 1].value);
  });

  readonly xLabels = computed(() => {
    const c = this.coords();
    const every = Math.max(1, Math.ceil(c.length / 8));
    return c.filter((_, i) => i % every === 0 || i === c.length - 1).map(p => ({
      i: p.i,
      x: p.x,
      label: new Date(p.day).toLocaleDateString('es-EC', { day: 'numeric', month: 'short', timeZone: 'UTC' })
    }));
  });

  readonly pathGenerated = computed(() => this.path('yg'));
  readonly pathAttended = computed(() => this.path('ya'));
  readonly last = computed(() => this.coords().at(-1) ?? null);
  readonly hovered = computed(() => {
    const i = this.hover();
    return i === null ? null : this.coords()[i] ?? null;
  });

  onKey(event: KeyboardEvent): void {
    const n = this.points().length;
    const current = this.hover() ?? n - 1;
    if (event.key === 'ArrowLeft') this.hover.set(Math.max(0, current - 1));
    else if (event.key === 'ArrowRight') this.hover.set(Math.min(n - 1, current + 1));
    else return;
    event.preventDefault();
  }

  private path(key: 'yg' | 'ya'): string {
    return this.coords().map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p[key].toFixed(1)}`).join(' ');
  }
}
