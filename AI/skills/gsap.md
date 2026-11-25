# Skills: gsap-core y gsap-frameworks

**gsap-core** cubre la API central de GSAP: `gsap.to/from/fromTo/set`, eases, stagger,
alias de transformaciones (`x`, `y`, `rotationX`), `autoAlpha` y `gsap.matchMedia()` para
breakpoints y movimiento reducido.

**gsap-frameworks** define cómo usar GSAP dentro de un framework con ciclo de vida: crear las
animaciones cuando el DOM ya existe, acotar los selectores al componente con
`gsap.context(callback, scope)` y revertir todo al destruirlo.

## Traducción a Angular

| Regla del skill | Implementación en Angular |
|-----------------|---------------------------|
| Crear al montar | `ngAfterViewInit()` |
| Limpiar al desmontar | `ngOnDestroy()` → `ctx.revert()` / `mm.revert()` / `timeline.kill()` |
| Selectores acotados | `gsap.context(..., hostElement)` y `gsap.utils.toArray(selector, root)` |
| Movimiento reducido | `gsap.matchMedia()` con `(prefers-reduced-motion: reduce)` |
| Timelines en vez de `delay` encadenados | `gsap.timeline()` con posiciones absolutas (`0`, `0.7`, `i * 0.06`) |

## 1. Tablero split-flap del turno

`Frontend/src/app/shared/components/turn-display.component.ts`

Cuando cambia el código del turno, un timeline maestro agrega un sub-timeline por casilla,
desfasado `i * 0.06` s. Cada casilla gira (`rotationX` -88° → 88° → 0°) cambiando el carácter
en cada medio giro, más veces cuanto más a la derecha, para que se detengan de izquierda a
derecha. Al final, el tablero se ilumina animando una variable CSS (`--glow`).

```ts
const cell = gsap.timeline();
for (let f = 0; f < flips; f++) {
  const next = f === flips - 1 ? finalChar : pool[random];
  cell
    .to(el, { rotationX: -88, duration: 0.05, ease: 'power1.in', onComplete: () => { el.textContent = next; } })
    .fromTo(el, { rotationX: 88 }, { rotationX: 0, duration: 0.06, ease: 'power1.out', immediateRender: false });
}
tl.add(cell, i * 0.06);
// ...
tl.fromTo(board, { '--glow': 0 }, { '--glow': 1, duration: 0.18 })
  .to(board, { '--glow': 0, duration: 1.1, ease: 'power2.inOut' });
```

- `immediateRender: false` en el `fromTo` encadenado, como indica el skill, para que no
  sobrescriba el estado del tween anterior.
- Si el usuario prefiere movimiento reducido, el código se pinta directamente sin animación.
- `ngOnDestroy` mata el timeline.

## 2. Fibra óptica del login

`Frontend/src/app/features/auth/auth-shell.component.ts`

Única secuencia de entrada de la aplicación, dentro de `gsap.matchMedia()`:

1. El formulario entra en 0.35 s desde el instante 0 (`autoAlpha` y `x`), para que el
   usuario pueda escribir sin esperar a la animación.
2. A la vez, los 7 hilos SVG se trazan con `strokeDashoffset` (stagger 0.09 s,
   `power3.inOut`) y el texto entra con `autoAlpha` e `y` a partir de 0.7 s.
3. Pulsos ámbar recorren cada hilo con `keyframes`, `repeat: -1` y `repeatDelay` distinto por
   hilo para que no vayan sincronizados.

Con `prefers-reduced-motion: reduce` los hilos aparecen ya dibujados y no hay pulsos;
`mm.revert()` en `ngOnDestroy` limpia todo al entrar a la aplicación.

## 3. Indicador del menú lateral

`Frontend/src/app/layout/shell.component.ts`

Tras cada `NavigationEnd`, la barra ámbar se desliza hasta la opción activa
(`y` y `height`, 0.45 s, `power3.out`) dentro de un `gsap.context` del menú. Es movimiento
que responde a la acción del usuario y muestra qué cambió.
