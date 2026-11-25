# Backend — Cash Management API (.NET 10)

API REST para la gestión de caja y turnos de una empresa proveedora de internet.

## Stack

| Tema                  | Tecnología                                                      |
|-----------------------|-----------------------------------------------------------------|
| Framework             | .NET 10 (LTS), ASP.NET Core Web API                             |
| Arquitectura          | Onion / Clean Architecture (Domain → Application → Infrastructure → Api) |
| Base de datos         | SQL Server 2022                                                 |
| Acceso a datos        | Entity Framework Core 10 (CRUD) + Dapper (procedimientos y funciones) |
| Seguridad             | JWT **cifrado (JWE)**, BCrypt, AES-256 para datos sensibles     |
| Validaciones          | FluentValidation                                                |
| Logs                  | Serilog (consola + archivos separados por categoría)            |
| Documentación         | Swagger / OpenAPI (Swashbuckle)                                 |
| Carga masiva          | ClosedXML (.xlsx) y CsvHelper (.csv)                            |

## Estructura

```
Backend/
├── database/                         Scripts SQL (se ejecutan automáticamente al iniciar)
│   ├── 00_create_database.sql
│   ├── 01_schema.sql                 Tablas, índices y restricciones
│   ├── 02_functions_procedures.sql   Funciones y procedimientos almacenados
│   └── 03_seed.sql                   Catálogos, menú por rol, servicios, cajas, clientes demo
├── src/
│   ├── CashManagement.Domain/        Entidades, constantes y excepciones de dominio (sin dependencias)
│   ├── CashManagement.Application/   Casos de uso (servicios), DTOs, validaciones, interfaces
│   ├── CashManagement.Infrastructure/EF Core, Dapper, cifrado, JWT, correo, lectura de Excel/CSV
│   └── CashManagement.Api/           Controladores, middleware, configuración de Serilog y Swagger
└── Dockerfile
```

Las dependencias apuntan siempre hacia el dominio: `Api → Application → Domain` e
`Infrastructure → Application`. La capa de aplicación solo conoce interfaces
(`IApplicationDbContext`, `ITokenService`, `IFieldEncryptor`, `IStoredProcedureService`...).

## Ejecución

### Con Docker (recomendado)

Desde la raíz del repositorio:

```bash
docker compose up --build
```

- API: http://localhost:5000 — Swagger: http://localhost:5000/swagger
- Mailpit (correos de recuperación de contraseña): http://localhost:8025

### Local

Requisitos: .NET 10 SDK y SQL Server (o LocalDB).

```bash
cd Backend/src/CashManagement.Api
dotnet run --launch-profile http
```

En `Development` usa `(localdb)\MSSQLLocalDB` (`appsettings.Development.json`). Al iniciar,
`DbInitializer` crea la base `CashManagementDb`, ejecuta los scripts de `database/` y crea los
usuarios de demostración. Swagger: http://localhost:5170/swagger

## Usuarios de demostración

| Rol           | Usuario      | Contraseña   |
|---------------|--------------|--------------|
| Administrador | `admin2026`  | `Admin2026`  |
| Gestor        | `gestor2026` | `Gestor2026` |
| Cajero        | `cajero01`   | `Cajero2026` |
| Cajero        | `cajero02`   | `Cajero2026` |

`cajero01` y `cajero02` están asignados a la **Caja 01** (el máximo permitido es 2), y `cajero02`
también a la **Caja 02**.

## Seguridad

- **JWT con claims encriptadas:** el token es un JWE (`alg: dir`, `enc: A256CBC-HS512`) que además
  va firmado con HS256. Las claims (id, usuario, rol) no se pueden leer sin la clave del servidor.
  En cada petición se revalida en la BD que el usuario siga activo y con sesión abierta: un
  usuario bloqueado o que cerró sesión pierde el acceso aunque su token no haya expirado.
  Un JWT solo firmado (sin cifrar) se rechaza. Las pruebas están en la carpeta
  *10. Seguridad JWT* de la colección de Postman.
- **Contraseña:** hash **BCrypt** (irreversible). Es la práctica recomendada en lugar de un
  cifrado reversible.
- **Correo e identificación del usuario:** cifrados con **AES-256-CBC** e IV aleatorio. Para
  buscar y validar duplicados sin descifrar se guarda además un **HMAC-SHA256** (`emailhash`,
  `identificationhash`) con índice único.
- **Bloqueo:** a los 3 intentos fallidos el usuario pasa a `BLO`. Lo desbloquea el administrador
  o la recuperación de contraseña.
- **Recuperación de contraseña:** token aleatorio de un solo uso (en BD solo se guarda su SHA-256)
  válido 30 minutos, enviado por correo.

## Reglas de negocio implementadas

**Usuarios**
- El administrador crea gestores y cajeros, que quedan activos.
- El gestor crea gestores y cajeros, que quedan **pendientes (`PEN`)** hasta que un administrador
  los aprueba (`userapproval`, `dateapproval`).
- Usuario: entre 8 y 20 caracteres, letras y al menos un número, sin caracteres especiales, único.
- Contraseña: entre 8 y 30 caracteres, al menos un número y una mayúscula.

**Clientes**
- Identificación: solo números, entre 10 y 13 dígitos, única (un cliente se registra una sola vez).
- Dirección y referencia: entre 20 y 100 caracteres.
- Teléfono: solo números, inicia con `09`, al menos 10 dígitos.

**Cajas y turnos**
- Máximo 2 cajeros por caja.
- Los 2 cajeros **no pueden operar la caja al mismo tiempo**: el cajero debe *abrir la caja*
  (`cashsession`); si el otro cajero la tiene abierta, recibe un 409. Lo garantiza también un
  índice único filtrado en la BD (`WHERE closedat IS NULL`).
- Todos los procesos de caja (contratos, pagos, atención de turnos) exigen una caja abierta.
- Código del turno: 2 letras mayúsculas y 4 números, con el prefijo del tipo de atención
  (`AC0001` Atención al cliente, `PS0001` Pago de servicio, `CT`, `CS`, `CF`, `CC`). Es
  correlativo por tipo y por día, lo genera `sp_generate_turn` con bloqueo para evitar
  duplicados y lo refuerza un `CHECK` en la tabla.
- Los turnos solo se generan o mueven a cajas **abiertas** por un cajero (`sp_generate_turn`
  y `TurnService` responden 400 si la caja está cerrada).

**Contratos**
- **Cambio de servicio** (`sp_change_contract_service`): el contrato actual pasa a `SUS`
  (sustituido) y se crea uno nuevo en `REN` (renovación) que **mantiene la fecha fin**.
- **Cancelación:** estado `CAN` y fecha fin = fecha actual.
- **Cambio de forma de pago:** se actualiza en el contrato vigente (decisión: la forma de pago no
  cambia las condiciones del servicio, así que no se versiona el contrato; queda en auditoría).

**Eliminaciones lógicas:** ningún `DELETE` físico. El `DbContext` convierte los borrados en
`isdeleted = 1` y aplica un filtro global de consultas.

## Procedimientos almacenados y funciones

Script: [`database/02_functions_procedures.sql`](database/02_functions_procedures.sql)

| Objeto                         | Uso                                                              |
|--------------------------------|------------------------------------------------------------------|
| `sp_generate_turn`             | Genera el código correlativo del turno y crea la atención         |
| `sp_change_contract_service`   | Cambio de servicio (SUS → nuevo contrato REN) en una transacción |
| `sp_dashboard_indicators`      | 5 conjuntos de resultados para el dashboard del administrador    |
| `fn_count_attended_turns`      | Turnos atendidos en el día según el rol (pantalla de bienvenida) |
| `fn_local_today`               | Fecha actual en la zona horaria del negocio (UTC-05:00)          |

## Logs (Serilog)

Carpeta `logs/` (en Docker se monta como volumen):

| Archivo            | Contenido                                                                 |
|--------------------|---------------------------------------------------------------------------|
| `app-*.log`        | Log general: peticiones HTTP, alertas (4xx) y errores                     |
| `audit-*.log`      | **Creación, actualización y eliminación** con entidad, id, usuario y valores anteriores y nuevos (los campos sensibles se enmascaran) |
| `security-*.log`   | **Inicios y cierres de sesión**, intentos fallidos, bloqueos y recuperación de contraseña |
| `errors-*.log`     | Solo errores y fallos críticos                                            |

La auditoría se genera automáticamente desde el `ChangeTracker` de EF Core. Las operaciones que
hacen los procedimientos almacenados se auditan explícitamente en el servicio que los llama.

## Endpoints principales

| Recurso      | Ruta base           | Notas                                                           |
|--------------|---------------------|-----------------------------------------------------------------|
| Auth         | `/api/auth`         | `login`, `logout`, `me`, `forgot-password`, `reset-password`    |
| Usuarios     | `/api/users`        | CRUD, `pending`, `{id}/approve`, `{id}/reject`, `{id}/status`, `bulk` |
| Clientes     | `/api/clients`      | CRUD, `identification/{id}`, `{id}/status`, `bulk`              |
| Cajas        | `/api/cash`         | CRUD, `{id}/cashiers`, `mine`, `session`, `{id}/open`, `session/close` |
| Turnos       | `/api/turns`        | CRUD, `queue`, `{id}/start`, `{id}/complete`, `{id}/cancel`     |
| Contratos    | `/api/contracts`    | CRUD, `{id}/service`, `{id}/payment-method`, `{id}/cancel`      |
| Pagos        | `/api/payments`     | Registrar, consultar, anular                                    |
| Servicios    | `/api/services`     | CRUD de planes con sus equipos                                  |
| Catálogos    | `/api/catalogs`     | Roles, estados, tipos de atención, formas de pago (CRUD)        |
| Menú         | `/api/menu`         | Menú del rol autenticado, leído desde la BD                     |
| Dashboard    | `/api/dashboard`    | `welcome` (todos), `indicators?from&to` (administrador)         |

Todas las respuestas tienen el formato:

```json
{ "success": true, "message": "Operación realizada correctamente.", "data": { }, "errors": null }
```

La colección de Postman está en [`/postman`](../postman). Está ordenada para correrla completa
con el Runner sobre una base recién creada: cada petición guarda en variables los ids que usan
las siguientes y valida el código HTTP esperado. Para la carga masiva, el directorio de trabajo
de Postman debe ser la raíz del repositorio. Desde la línea de comandos:

```bash
npx newman run postman/CashManagement.postman_collection.json --env-var baseUrl=http://localhost:5000
```

## Configuración

Variables relevantes (`appsettings.json`, sobrescribibles por variables de entorno como `Jwt__SigningKey`):

| Clave                                  | Descripción                                   |
|----------------------------------------|-----------------------------------------------|
| `ConnectionStrings:DefaultConnection`  | Cadena de conexión a SQL Server               |
| `Jwt:SigningKey` / `Jwt:EncryptionKey` | Claves de firma y de cifrado del token        |
| `Jwt:ExpirationMinutes`                | Vigencia del token                            |
| `Encryption:Key` / `Encryption:HashKey`| Claves AES y HMAC de los datos sensibles      |
| `Smtp:*`                               | Servidor de correo (Mailpit en Docker)        |
| `App:FrontendUrl`                      | URL del frontend para el enlace de recuperación |
| `Cors:Origins`                         | Orígenes permitidos                           |

> Las claves incluidas son valores de desarrollo. En producción deben definirse por variables de entorno o un gestor de secretos.
