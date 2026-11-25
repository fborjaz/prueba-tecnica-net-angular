# Ventanilla: gestión de caja y turnos (.NET 10 + Angular)

Sistema para la gestión de caja de una empresa que brinda servicios de internet: usuarios por
rol (administrador, gestor, cajero), cajas con máximo 2 cajeros que no pueden operarlas a la
vez, turnos por tipo de atención, clientes, contratos de servicio y pagos.

## Estructura del repositorio

| Carpeta / archivo     | Contenido                                                              |
|-----------------------|------------------------------------------------------------------------|
| `Backend/`            | API REST .NET 10 (arquitectura Onion) — [README](Backend/README.md)     |
| `Backend/database/`   | Scripts SQL: esquema, **procedimientos almacenados y funciones**, datos iniciales |
| `Frontend/`           | Aplicación Angular modularizada — [README](Frontend/README.md)         |
| `postman/`            | Colección con el CRUD de cada tabla, registro de usuarios y login, más archivos de ejemplo para la carga masiva |
| `AI/`                 | Skills y prompts usados con IA durante el desarrollo — [README](AI/README.md) |
| `docker-compose.yml`  | SQL Server, Mailpit, API y frontend                                    |

## Ejecutar con Docker

```bash
docker compose up --build
```

| Servicio  | URL                              |
|-----------|----------------------------------|
| Frontend  | http://localhost:8080            |
| API       | http://localhost:5000/swagger    |
| Mailpit   | http://localhost:8025 (correos de recuperación de contraseña) |

El primer arranque puede tardar unos minutos mientras SQL Server se inicializa; la API espera a
que la base esté lista. Al iniciar, la API crea la base de datos, ejecuta los scripts de `Backend/database` y crea los
usuarios de demostración:

| Rol           | Usuario      | Contraseña   |
|---------------|--------------|--------------|
| Administrador | `admin2026`  | `Admin2026`  |
| Gestor        | `gestor2026` | `Gestor2026` |
| Cajero        | `cajero01`   | `Cajero2026` |
| Cajero        | `cajero02`   | `Cajero2026` |

Los logs de Serilog quedan en el volumen `api-logs`:

```bash
docker compose exec api ls logs
docker compose exec api cat logs/audit-$(date +%Y%m%d).log
```

## Requisitos de la prueba

| Requisito | Dónde |
|-----------|-------|
| DDD / Onion | `Backend/src` (Domain, Application, Infrastructure, Api) |
| Eliminaciones lógicas | `isdeleted` + filtro global en `AppDbContext` |
| Swagger | `/swagger` con autenticación Bearer |
| Entity Framework / Dapper | EF Core para el CRUD, Dapper para procedimientos y funciones |
| **JWT con claims encriptadas** | Token JWE (`dir` + `A256CBC-HS512`) firmado con HS256 |
| Stored procedures / funciones | `sp_generate_turn`, `sp_change_contract_service`, `sp_dashboard_indicators`, `fn_count_attended_turns`, `fn_local_today` |
| Contraseña, identificación y correo protegidos | BCrypt para la contraseña; AES-256 + HMAC para correo e identificación |
| **Docker** | `Backend/Dockerfile`, `Frontend/Dockerfile`, `docker-compose.yml` |
| **Serilog** (CRUD, errores, alertas, inicios de sesión) | `logs/audit-*.log`, `logs/security-*.log`, `logs/errors-*.log`, `logs/app-*.log` |
| Frontend modularizado, rutas a módulos, protección de rutas | `Frontend/src/app` |
| Menú desde la base de datos según el rol | Tablas `menu` / `rolmenu` + `GET /api/menu` |
| README por proyecto | `Backend/README.md`, `Frontend/README.md` |
| Colección de Postman | `postman/CashManagement.postman_collection.json` |
| Skills y prompts de IA | `AI/` |

## Decisiones sobre puntos ambiguos del enunciado

- **Cliente:** se trata como entidad de datos que registra el cajero, no como usuario con
  inicio de sesión (el enunciado no define pantallas para el cliente).
- **Contraseña "encriptada":** se guarda con hash BCrypt, que es irreversible y es la práctica
  recomendada; correo e identificación sí se cifran con AES porque deben poder mostrarse.
- **Cambio de forma de pago:** se actualiza en el contrato vigente y queda registrado en la
  auditoría; no se versiona el contrato porque no cambian las condiciones del servicio.
- **Correlativo del turno:** reinicia cada día por tipo de atención (`AC0001`, `PS0001`…).
- **Bloqueo de usuarios:** automático al tercer intento fallido y también manual desde la
  administración; se desbloquea por el administrador o recuperando la contraseña.
- **Concurrencia de caja:** el cajero debe *abrir la caja*; mientras la tenga abierta, el otro
  cajero asignado no puede abrirla ni operar sobre ella.
- **Turnos solo a cajas abiertas:** el gestor solo puede generar o mover turnos a una caja que
  un cajero tenga abierta (lo valida `sp_generate_turn` y el servicio); así ningún turno queda
  en una caja sin nadie que lo atienda.
