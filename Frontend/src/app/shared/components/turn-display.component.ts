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
  templateUrl: './turn-display.component.html',
  styleUrl: './turn-display.component.scss'
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
