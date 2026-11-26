import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, inject } from '@angular/core';
import { gsap } from 'gsap';

/** Trazos de fibra óptica que convergen hacia el formulario. */
const STRANDS = [
  'M-20 120 C 160 110, 260 260, 420 300 S 640 330, 760 340',
  'M-20 210 C 140 200, 250 300, 420 320 S 640 340, 760 345',
  'M-20 300 C 150 300, 260 330, 420 335 S 640 348, 760 350',
  'M-20 390 C 150 400, 260 360, 420 350 S 640 352, 760 355',
  'M-20 480 C 160 490, 270 400, 420 365 S 640 358, 760 360',
  'M-20 570 C 170 580, 280 440, 430 380 S 640 362, 760 365',
  'M-20 30 C 170 20, 270 220, 430 285 S 640 326, 760 335'
];

/**
 * Pantalla de autenticación en dos columnas. A la izquierda, el haz de fibra óptica:
 * una única secuencia al cargar (los hilos se trazan y luego viajan pulsos de luz),
 * hecha con un timeline de GSAP dentro de gsap.matchMedia para respetar reduced-motion.
 */
@Component({
  selector: 'app-auth-shell',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-shell.component.html',
  styleUrl: './auth-shell.component.scss'
})
export class AuthShellComponent implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly strands = STRANDS;
  private mm?: gsap.MatchMedia;

  ngAfterViewInit(): void {
    const root = this.host.nativeElement as HTMLElement;
    this.mm = gsap.matchMedia();

    this.mm.add(
      { motion: '(prefers-reduced-motion: no-preference)', reduce: '(prefers-reduced-motion: reduce)' },
      ctx => {
        const strands = gsap.utils.toArray<SVGPathElement>('.strand', root);
        const pulses = gsap.utils.toArray<SVGPathElement>('.pulse', root);

        strands.forEach(p => {
          const length = p.getTotalLength();
          gsap.set(p, { strokeDasharray: length, strokeDashoffset: ctx.conditions?.['reduce'] ? 0 : length });
        });
        pulses.forEach(p => {
          const length = p.getTotalLength();
          gsap.set(p, { strokeDasharray: `60 ${length}`, strokeDashoffset: 60, autoAlpha: 0 });
        });

        if (ctx.conditions?.['reduce']) {
          gsap.set('.hero__copy > *', { autoAlpha: 1 });
          return;
        }

        // El formulario se muestra de inmediato (no se bloquea al usuario esperando la animación).
        const tl = gsap.timeline({ defaults: { ease: 'power2.out' } });
        tl.from('.form-card', { autoAlpha: 0, x: 16, duration: 0.35 }, 0)
          .to(strands, { strokeDashoffset: 0, duration: 1.6, stagger: 0.09, ease: 'power3.inOut' }, 0)
          .from('.hero__copy > *', { autoAlpha: 0, y: 18, duration: 0.7, stagger: 0.12 }, 0.7);

        // Pulsos de luz que recorren la fibra, escalonados y lentos.
        pulses.forEach((p, i) => {
          const length = p.getTotalLength();
          tl.to(p, {
            keyframes: [
              { autoAlpha: 1, duration: 0.01 },
              { strokeDashoffset: -length, duration: 2.6 + (i % 3) * 0.4, ease: 'sine.inOut' }
            ],
            repeat: -1,
            repeatDelay: 1.4 + i * 0.35
          }, 1.2 + i * 0.28);
        });
      },
      root
    );
  }

  ngOnDestroy(): void {
    this.mm?.revert();
  }
}
