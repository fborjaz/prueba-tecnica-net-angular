# Frontend — Ventanilla (Angular 21)

Interfaz web para la gestión de caja y turnos: login, bienvenida, dashboard, usuarios, cajas,
turnos, procesos de caja, clientes y servicios.

## Stack

| Tema           | Tecnología                                                        |
|----------------|-------------------------------------------------------------------|
| Framework      | Angular 21 con **NgModules** y carga diferida (`loadChildren`)    |
| Estado         | Signals (aplicación *zoneless*)                                   |
| Formularios    | Reactive Forms con validaciones iguales a las de la API           |
| UI             | Angular Material/CDK (diálogos, snackbars, barra de progreso, tooltips) + sistema visual propio |
| Animaciones    | **GSAP** (pantalla de turno split-flap, login, menú)              |
| Tipografías    | Archivo y Big Shoulders Display (empaquetadas con `@fontsource`, sin CDN) |

## Estructura

```
src/app/
├── core/                    Se carga una vez (CoreModule)
│   ├── guards/              authGuard, guestGuard, roleGuard (rol + menú de la BD)
│   ├── interceptors/        AuthInterceptor, LoadingInterceptor, ErrorInterceptor
│   ├── models/              Interfaces de la API
│   ├── services/            Sesión, menú, catálogos, notificaciones, carga
│   └── utils/validators.ts  Reglas de validación (mismas que el backend)
├── shared/                  SharedModule: componentes reutilizables
│   └── components/          Ícono, encabezado, estados, diálogos, carga masiva,
│                            formulario de cliente, pantalla de turno (GSAP)
├── layout/                  LayoutModule: estructura con menú lateral
└── features/                Un módulo por funcionalidad, cargado con loadChildren
    ├── auth/                Login, recuperar y restablecer contraseña
    ├── welcome/             Bienvenida (todos los roles)
    ├── dashboard/           Indicadores (administrador)
    ├── users/               Usuarios (administrador y gestor)
    ├── cash/                Cajas y asignación de cajeros (administrador y gestor)
    ├── turns/               Asignación de turnos (gestor)
    ├── cashier/             Procesos de caja (cajero)
    ├── clients/             Mantenimiento de clientes (cajero)
    └── services/            Planes de internet (administrador)
```

## Requisitos de la prueba

| Requisito | Implementación |
|-----------|----------------|
| Desarrollo modularizado *(obligatorio)* | `CoreModule`, `SharedModule`, `LayoutModule` y un `NgModule` por funcionalidad |
| Rutas que llaman a los módulos *(obligatorio)* | `app-routing-module.ts`: todas las rutas usan `loadChildren` |
| Protección de rutas *(obligatorio)* | `authGuard` en el área privada; `roleGuard` (`canMatch`) valida el rol **y** que la ruta exista en el menú que la BD asigna a ese rol; si no, el módulo ni se descarga |
| Menú desde la base de datos por rol | `MenuService` consume `GET /api/menu`; el sidebar se arma con esa respuesta |
| Interceptor | `AuthInterceptor` adjunta `Authorization: Bearer`, `Accept`, `Accept-Language`, `X-Client-App` y `X-Request-Id` a todas las peticiones |
| Mensajes de error y éxito | `NotifyService` (snackbars) + errores por campo devueltos por la API |
| Indicador de carga | `LoadingInterceptor` + barra de progreso global; botones con estado "Procesando…" |
| Login con recuperación de contraseña | `/auth/login`, `/auth/forgot-password`, `/auth/reset-password?token=…` |
| Validar antes de consumir el servicio | Formularios reactivos: si son inválidos se marcan y no se llama a la API |
| Bienvenida | Datos del usuario, turnos atendidos hoy según el rol, pendientes de aprobación (gestor) |
| Dashboard (solo administrador) | Sesiones activas/inactivas, bloqueados, pendientes; cajas, cajeros y gestores con filtro de fechas |
| Usuarios (administrador) | Editar, cambiar estado (activar, inactivar, bloquear), aprobar/rechazar, eliminar, carga masiva |
| Asignación de turnos (gestor) | Generar turnos por caja y tipo de atención, moverlos o eliminarlos |
| Procesos de caja (cajero) | Abrir/cerrar caja, cola de turnos, contratar, pagar, cambiar servicio o forma de pago, cancelar |
| Clientes (cajero) | Registrar, editar, activar/inactivar, eliminar, carga masiva |

## Diseño y animaciones

La dirección visual se definió con el skill **frontend-design** y las animaciones con los
skills de **GSAP**. Detalle en [`/AI`](../AI/README.md).

- **Pantalla de turno split-flap** (`shared/components/turn-display.component.ts`): al llamar
  un turno, cada casilla gira por caracteres hasta formar el código (`PS0001`) y el tablero se
  ilumina.
- **Login** (`features/auth/auth-shell.component.ts`): hilos de fibra que se trazan y pulsos
  de luz que los recorren.
- **Menú lateral** (`layout/shell.component.ts`): la barra ámbar se desliza a la opción activa.

Todas respetan `prefers-reduced-motion` y se limpian al destruir el componente.

## Ejecución

### Con Docker (recomendado)

Desde la raíz del repositorio: `docker compose up --build` y abrir http://localhost:8080.
nginx sirve la aplicación y redirige `/api` al contenedor del backend.

### Local

Requisitos: Node 22.12+ o 24, y la API corriendo en http://localhost:5170.

```bash
cd Frontend
npm install
npm start
```

Abrir http://localhost:4200. `proxy.conf.json` redirige `/api` a la API local.

## Usuarios de demostración

| Rol           | Usuario      | Contraseña   |
|---------------|--------------|--------------|
| Administrador | `admin2026`  | `Admin2026`  |
| Gestor        | `gestor2026` | `Gestor2026` |
| Cajero        | `cajero01`   | `Cajero2026` |
| Cajero        | `cajero02`   | `Cajero2026` |

La pantalla de login permite completar estos datos con un clic.

## Recorrido sugerido

1. **Cajero (`cajero01`):** en *Procesos de caja*, abrir la *Caja 01*.
2. **Gestor (en otra ventana privada):** en *Turnos*, generar turnos para la *Caja 01*. Solo
   aparecen las cajas abiertas por un cajero.
3. **Cajero:** la cola se actualiza sola; pulsar *Llamar siguiente*, buscar al cliente
   `0912345671`, registrar un pago y finalizar la atención.
4. **Cajero (`cajero02`, en otra ventana privada):** intentar abrir la *Caja 01* ocupada.
5. **Administrador:** revisar el *Dashboard* y aprobar en *Usuarios* los usuarios creados
   por el gestor.

Cada usuario tiene una sola sesión activa: si cierra sesión en otra ventana o equipo, el
resto de sus ventanas vuelve al login.
