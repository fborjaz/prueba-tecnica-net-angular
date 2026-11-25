# Skill: frontend-design

**Qué es:** un skill de diseño de interfaces que obliga a definir una dirección visual propia
antes de escribir código: anclar las decisiones en el tema del producto, armar un sistema de
tokens (color, tipografía, layout), revisarlo contra los resultados genéricos típicos y recién
entonces construir, con autocrítica y un piso de calidad (responsive, foco visible,
movimiento reducido, contraste).

## Cómo se aplicó

### 1. Anclar en el tema

El producto es la **ventanilla de atención** de un proveedor de internet: turnos en pantallas
LED, cajas, fibra óptica, planes medidos en Mbps. De ahí sale el nombre de la interfaz,
**Ventanilla**, y todas las decisiones visuales.

### 2. Sistema de tokens

| Token | Valor | Rol |
|-------|-------|-----|
| Tinta | `#1B2430` | Texto principal y barra lateral |
| Papel frío | `#EEF1F4` | Fondo de trabajo |
| Superficie | `#FFFFFF` | Paneles |
| Ámbar LED | `#F2A900` | Códigos de turno y acción primaria |
| Verde fibra | `#0F7C7A` | Estados correctos, en servicio |
| Alerta | `#C2412D` | Errores, bloqueos |

- **Tipografía:** *Archivo* (variable) para toda la interfaz, con números tabulares; *Big
  Shoulders Display*, condensada y de señalética, solo para códigos de turno, cifras y
  títulos. Escala de razón 1.25.
- **Radios por jerarquía:** 4 px controles, 8 px paneles, píldora para estados.
- **Layout:** barra lateral de tinta con el menú de la BD y contenido alineado a la izquierda
  sobre papel frío; tablas densas en lugar de tarjetas repetidas.

### 3. Revisión contra lo genérico

Antes de codificar se descartó:

- El kit de tarjetas SaaS idénticas con sombra gris: se usan paneles planos con borde y
  tablas densas.
- Etiquetas en mayúsculas sobre cada título y separadores con punto medio.
- Un acento de color por decoración: el ámbar solo aparece donde significa "turno" o
  "acción principal".

### 4. Un solo elemento memorable

**El tablero de turno split-flap** (`turn-display.component.ts`): la pieza que identifica a
la aplicación. Se reutiliza en la pantalla del cajero ("Atendiendo") y en la del gestor
("Último turno asignado"), y la bienvenida usa la misma estética para el contador de turnos
atendidos. El resto de la interfaz se mantiene sobrio.

### 5. Movimiento con propósito

- Una sola secuencia de entrada, en el login (fibra que se traza).
- El resto del movimiento responde a acciones del usuario: llamar un turno (split-flap),
  navegar (barra ámbar del menú).
- Todo respeta `prefers-reduced-motion`.

### 6. Textos

Escritos desde el punto de vista del usuario, en voz activa y con la misma acción de punta a
punta ("Registrar pago" → "Pago registrado correctamente"). Los estados vacíos indican qué
hacer: "Pida a un gestor que le asigne una caja".

### 7. Piso de calidad

Responsive hasta móvil (menú lateral como cajón), enlace "Saltar al contenido", foco visible
en ámbar, etiquetas en todos los campos, `aria-live` en el tablero de turno y en los errores.
