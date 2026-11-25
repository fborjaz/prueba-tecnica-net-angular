import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { gsap } from 'gsap';
import { filter } from 'rxjs';
import { LoadingService, MenuService } from '../core/services/app-services';
import { AuthService } from '../core/services/auth.service';
import { ConfirmService } from '../shared/components/dialogs';

const ROLE_LABELS: Record<string, string> = {
  ADMINISTRADOR: 'Administrador',
  GESTOR: 'Gestor',
  CAJERO: 'Cajero'
};

/**
 * Estructura principal: barra lateral con el menú que devuelve la base de datos según el rol,
 * barra de progreso global y área de contenido (router-outlet de los módulos).
 */
@Component({
  selector: 'app-shell',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly menuService = inject(MenuService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  readonly loading = inject(LoadingService);

  readonly user = this.auth.user;
  readonly menu = this.menuService.items;
  readonly menuError = signal(false);
  readonly drawerOpen = signal(false);
  readonly roleLabel = computed(() => ROLE_LABELS[this.user()?.role ?? ''] ?? '');
  readonly initials = computed(() => {
    const u = this.user();
    return u ? `${u.names[0] ?? ''}${u.lastNames[0] ?? ''}`.toUpperCase() : '';
  });

  private readonly nav = viewChild<ElementRef<HTMLElement>>('nav');
  private readonly indicator = viewChild<ElementRef<HTMLElement>>('indicator');
  private ctx?: gsap.Context;

  ngOnInit(): void {
    this.menuService.load().subscribe({
      next: () => requestAnimationFrame(() => this.moveIndicator(true)),
      error: () => this.menuError.set(true)
    });

    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.drawerOpen.set(false);
        requestAnimationFrame(() => this.moveIndicator(false));
      });
  }

  ngAfterViewInit(): void {
    this.ctx = gsap.context(() => void 0, this.nav()?.nativeElement);
  }

  ngOnDestroy(): void {
    this.ctx?.revert();
  }

  logout(): void {
    this.confirm
      .ask({ title: 'Cerrar sesión', message: 'Si tiene una caja abierta, también se cerrará.', confirmText: 'Cerrar sesión' })
      .subscribe(ok => {
        if (!ok) return;
        this.menuService.reset();
        this.auth.logout();
      });
  }

  /**
   * Desliza la barra ámbar hasta la opción activa del menú: la única animación del layout,
   * y responde a la navegación del usuario.
   */
  private moveIndicator(instant: boolean): void {
    const nav = this.nav()?.nativeElement;
    const indicator = this.indicator()?.nativeElement;
    if (!nav || !indicator) return;

    const active = nav.querySelector<HTMLElement>('a.is-active');
    if (!active) {
      gsap.to(indicator, { autoAlpha: 0, duration: 0.2 });
      return;
    }

    const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.ctx?.add(() => {
      gsap.to(indicator, {
        y: active.offsetTop,
        height: active.offsetHeight,
        autoAlpha: 1,
        duration: instant || reduced ? 0 : 0.45,
        ease: 'power3.out'
      });
    });
  }
}
