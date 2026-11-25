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
  template: `
    <div class="chart">
      <div class="chart__head">
        <ul class="legend" aria-label="Series">
          <li><span class="swatch swatch--generated"></span>Turnos generados</li>
          <li><span class="swatch swatch--attended"></span>Turnos atendidos</li>
        </ul>
        <button class="btn btn--ghost btn--sm" type="button" (click)="showTable.set(!showTable())" [attr.aria-pressed]="showTable()">
          {{ showTable() ? 'Ver gráfico' : 'Ver tabla' }}
        </button>
      </div>

      @if (!showTable()) {
        <div class="plot" (mouseleave)="hover.set(null)">
          <svg [attr.viewBox]="'0 0 ' + w + ' ' + h" role="img"
               [attr.aria-label]="'Turnos por día del ' + (points()[0]?.day | date: 'd MMM') + ' al ' + (points()[points().length - 1]?.day | date: 'd MMM')"
               tabindex="0" (keydown)="onKey($event)" (focus)="hover.set(hover() ?? points().length - 1)" (blur)="hover.set(null)">
            @for (t of ticks(); track t.value) {
              <line class="grid" [attr.x1]="pad.left" [attr.x2]="w - pad.right" [attr.y1]="t.y" [attr.y2]="t.y" />
              <text class="axis" [attr.x]="pad.left - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.value }}</text>
            }
            @for (l of xLabels(); track l.i) {
              <text class="axis" [attr.x]="l.x" [attr.y]="h - 8" text-anchor="middle">{{ l.label }}</text>
            }

            <path class="line line--generated" [attr.d]="pathGenerated()" />
            <path class="line line--attended" [attr.d]="pathAttended()" />

            @if (last(); as p) {
              <text class="direct" [attr.x]="p.x + 10" [attr.y]="p.yg + 4">{{ p.g }} generados</text>
              <text class="direct" [attr.x]="p.x + 10" [attr.y]="p.ya + (Math.abs(p.ya - p.yg) < 14 ? 16 : 4)">{{ p.a }} atendidos</text>
            }

            @if (hovered(); as p) {
              <line class="cross" [attr.x1]="p.x" [attr.x2]="p.x" [attr.y1]="pad.top" [attr.y2]="h - pad.bottom" />
              <circle class="dot dot--generated" [attr.cx]="p.x" [attr.cy]="p.yg" r="4.5" />
              <circle class="dot dot--attended" [attr.cx]="p.x" [attr.cy]="p.ya" r="4.5" />
            }

            <!-- Zonas de captura más anchas que la marca -->
            @for (p of coords(); track p.i) {
              <rect class="hit" [attr.x]="p.x - step() / 2" [attr.y]="pad.top" [attr.width]="step()" [attr.height]="h - pad.top - pad.bottom"
                    (mouseenter)="hover.set(p.i)" />
            }
          </svg>

          @if (hovered(); as p) {
            <div class="tooltip" [style.left.%]="(p.x / w) * 100" role="status">
              <strong>{{ p.day | date: 'EEEE d MMM' }}</strong>
              <span><i class="swatch swatch--generated"></i>Generados <b>{{ p.g }}</b></span>
              <span><i class="swatch swatch--attended"></i>Atendidos <b>{{ p.a }}</b></span>
            </div>
          }
        </div>
      } @else {
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>Día</th><th class="num">Generados</th><th class="num">Atendidos</th></tr></thead>
            <tbody>
              @for (p of points(); track p.day) {
                <tr><td>{{ p.day | date: 'EEE d MMM' }}</td><td class="num">{{ p.turnsGenerated }}</td><td class="num">{{ p.turnsAttended }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: `
    :host { --generated: #3a6db5; --attended: #d99000; display: block; }
    .chart__head { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: 0.5rem; }
    .legend { display: flex; gap: 1.25rem; margin: 0; padding: 0; list-style: none; font-size: var(--fs-sm); color: var(--ink-2); }
    .legend li { display: flex; align-items: center; gap: 0.5rem; }
    .swatch { display: inline-block; width: 12px; height: 3px; border-radius: 2px; }
    .swatch--generated { background: var(--generated); }
    .swatch--attended { background: var(--attended); }
    .plot { position: relative; }
    svg { display: block; width: 100%; height: auto; overflow: visible; }
    svg:focus-visible { outline: none; box-shadow: var(--focus); border-radius: var(--r-control); }
    .grid { stroke: var(--line-soft); stroke-width: 1; }
    .axis { fill: var(--ink-3); font-size: 11px; font-family: var(--font-ui); }
    .line { fill: none; stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .line--generated { stroke: var(--generated); }
    .line--attended { stroke: var(--attended); }
    .direct { fill: var(--ink-2); font-size: 11px; font-family: var(--font-ui); font-weight: 600; }
    .cross { stroke: var(--ink-3); stroke-width: 1; stroke-dasharray: 3 3; }
    .dot { stroke: var(--surface); stroke-width: 2; }
    .dot--generated { fill: var(--generated); }
    .dot--attended { fill: var(--attended); }
    .hit { fill: transparent; }
    .tooltip { position: absolute; top: 0; transform: translateX(-50%); display: grid; gap: 0.125rem; min-width: 150px;
      padding: 0.5rem 0.75rem; background: var(--ink); color: #fff; border-radius: var(--r-control); font-size: var(--fs-xs);
      pointer-events: none; box-shadow: 0 8px 24px rgba(27,36,48,0.25); }
    .tooltip strong { text-transform: capitalize; margin-bottom: 0.125rem; }
    .tooltip span { display: flex; align-items: center; gap: 0.375rem; color: #c8d1dc; }
    .tooltip b { margin-left: auto; color: #fff; }
  `
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
