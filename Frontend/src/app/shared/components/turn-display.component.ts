import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  untracked,
  viewChild,
  viewChildren
} from '@angular/core';
import { gsap } from 'gsap';

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const DIGITS = '0123456789';
const SLOTS = 6;

/**
 * Pantalla de turno estilo split-flap (como los tableros LED de una ventanilla).
 * Cuando cambia el código, cada casilla "voltea" por caracteres aleatorios hasta
 * detenerse en el definitivo, de izquierda a derecha, y el tablero se ilumina.
 * Animación con un timeline de GSAP; respeta prefers-reduced-motion.
 */
@Component({
  selector: 'app-turn-display',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="board" #board>
      <span class="board__label">{{ label() }}</span>
      <div class="flaps" role="status" aria-live="polite" [attr.aria-label]="code() ? 'Turno ' + code() : 'Sin turno en pantalla'">
        @for (slot of slots; track slot) {
          <span class="flap" aria-hidden="true"><span class="flap__char" #char></span></span>
        }
      </div>
      <span class="board__meta">{{ meta() }}</span>
    </div>
  `,
  styles: `
    /* El tamaño de las casillas depende del ancho del tablero (container query),
       no de la ventana: así nunca se desborda de la columna donde esté. */
    :host { display: block; min-width: 0; container-type: inline-size; }
    .board { position: relative; display: grid; gap: 0.875rem; padding: 1.5rem clamp(1rem, 5cqi, 1.75rem) 1.375rem;
      background: radial-gradient(120% 140% at 50% 0%, #243142 0%, var(--ink) 60%);
      border-radius: var(--r-panel); color: #c8d1dc; --glow: 0; overflow: hidden; }
    .board::after { content: ''; position: absolute; inset: 0; pointer-events: none;
      background: radial-gradient(60% 80% at 50% 55%, rgba(242,169,0,calc(var(--glow) * 0.22)), transparent 70%); }
    .board__label { font-size: var(--fs-sm); color: #9fb0c2; }
    .board__meta { font-size: var(--fs-sm); color: #9fb0c2; min-height: 1.4em; }
    .flaps { display: grid; grid-template-columns: repeat(6, minmax(0, 64px)); column-gap: 0.375rem; perspective: 600px; }
    .flap { position: relative; display: grid; place-items: center; width: 100%; aspect-ratio: 0.74;
      background: linear-gradient(#111a24 0 49.4%, #0c131b 50.6% 100%); border-radius: 5px;
      box-shadow: inset 0 0 0 1px rgba(255,255,255,0.05), 0 2px 0 rgba(0,0,0,0.35); }
    .flap::after { content: ''; position: absolute; left: 0; right: 0; top: 50%; height: 1px; background: rgba(0,0,0,0.65); }
    .flap:nth-child(2) { margin-right: 0.625rem; }
    .flap__char { font-family: var(--font-sign); font-weight: 800; font-size: clamp(1.5rem, 9cqi, 3.25rem);
      line-height: 1; color: var(--amber); transform-origin: 50% 50%; backface-visibility: hidden;
      text-shadow: 0 0 calc(6px + var(--glow) * 14px) rgba(242,169,0,calc(0.35 + var(--glow) * 0.5)); }
  `
})
export class TurnDisplayComponent implements AfterViewInit, OnDestroy {
  readonly code = input<string | null>(null);
  readonly label = input('Turno en atención');
  readonly meta = input<string>('');

  readonly slots = Array.from({ length: SLOTS }, (_, i) => i);
  private readonly board = viewChild.required<ElementRef<HTMLElement>>('board');
  private readonly chars = viewChildren<ElementRef<HTMLElement>>('char');

  private timeline?: gsap.core.Timeline;
  private ready = false;
  private readonly reducedMotion = typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    effect(() => {
      const code = this.code();
      if (this.ready) untracked(() => this.flipTo(code));
    });
  }

  ngAfterViewInit(): void {
    this.ready = true;
    this.render(this.code());
  }

  ngOnDestroy(): void {
    this.timeline?.kill();
  }

  private flipTo(code: string | null): void {
    this.timeline?.kill();
    if (this.reducedMotion) {
      this.render(code);
      return;
    }

    const target = this.normalize(code);
    const tl = gsap.timeline({ defaults: { overwrite: 'auto' } });

    this.chars().forEach((ref, i) => {
      const el = ref.nativeElement;
      const finalChar = target[i];
      const pool = i < 2 ? LETTERS : DIGITS;
      const flips = finalChar === '–' ? 2 : 5 + i * 2; // cada casilla se detiene un poco después que la anterior
      const cell = gsap.timeline();

      for (let f = 0; f < flips; f++) {
        const next = f === flips - 1 ? finalChar : pool[Math.floor(Math.random() * pool.length)];
        cell
          .to(el, { rotationX: -88, duration: 0.05, ease: 'power1.in', onComplete: () => { el.textContent = next; } })
          .fromTo(el, { rotationX: 88 }, { rotationX: 0, duration: 0.06, ease: 'power1.out', immediateRender: false });
      }

      tl.add(cell, i * 0.06);
    });

    // Al detenerse la última casilla, el tablero "se enciende" y vuelve a su brillo normal.
    if (code) {
      tl.fromTo(this.board().nativeElement, { '--glow': 0 }, { '--glow': 1, duration: 0.18, ease: 'power2.out' })
        .to(this.board().nativeElement, { '--glow': 0, duration: 1.1, ease: 'power2.inOut' });
    }

    this.timeline = tl;
  }

  private render(code: string | null): void {
    const target = this.normalize(code);
    this.chars().forEach((ref, i) => {
      ref.nativeElement.textContent = target[i];
      gsap.set(ref.nativeElement, { rotationX: 0 });
    });
  }

  private normalize(code: string | null): string[] {
    const value = (code ?? '').toUpperCase().padEnd(SLOTS, '–').slice(0, SLOTS);
    return [...value];
  }
}
