# Uso de IA en el desarrollo

Esta carpeta documenta los **skills** y **prompts** usados con un agente de IA durante la
prueba, como pide el enunciado.

## Para qué se usó

Se documentan estos usos principales:

1. **Script de la base de datos:** tablas que faltaban en el modelo de la prueba y
   procedimientos almacenados (`sp_generate_turn`, `sp_change_contract_service`,
   `sp_dashboard_indicators`).
2. **Diseño visual y animaciones del frontend:** con los skills **frontend-design** y **GSAP**.
3. **Un bug difícil de rastrear:** el `ng build` compilaba pero el proceso nunca terminaba.
4. **Pruebas de seguridad del JWT:** tokens alterados, sin firma, sin cifrar, con otra clave,
   expirados, roles sin permiso y sesiones cerradas o bloqueadas (carpeta *10. Seguridad JWT*
   de Postman). Encontraron un fallo que se corrigió: la API aceptaba tokens sin cifrar.

Los prompts están en [prompts.md](prompts.md). Todo lo generado se revisó, se compiló y se
probó con la API y la base de datos funcionando antes de integrarlo.

## Skills utilizados

| Skill | Para qué se usó | Detalle |
|-------|-----------------|---------|
| **frontend-design** | Identidad visual "Ventanilla": paleta, tipografía, layout y la pantalla de turno split-flap como elemento central | [skills/frontend-design.md](skills/frontend-design.md) |
| **gsap-core** | Tweens, timelines, eases, `autoAlpha` y `gsap.matchMedia()` para movimiento reducido | [skills/gsap.md](skills/gsap.md) |
| **gsap-frameworks** | Ciclo de vida en Angular: crear en `ngAfterViewInit`, acotar selectores al componente y revertir en `ngOnDestroy` | [skills/gsap.md](skills/gsap.md) |

## Dónde se ve en la aplicación

| Pantalla | Qué aporta | Archivo |
|----------|------------|---------|
| Login | Haz de fibra óptica que se traza y pulsos de luz que lo recorren (timeline GSAP) | `Frontend/src/app/features/auth/auth-shell.component.ts` |
| Procesos de caja y Turnos | Tablero split-flap: cada casilla gira por caracteres hasta el código del turno (`PS0001`) y el tablero se ilumina | `Frontend/src/app/shared/components/turn-display.component.ts` |
| Menú lateral | Barra ámbar que se desliza a la opción activa al navegar | `Frontend/src/app/layout/shell.component.ts` |
