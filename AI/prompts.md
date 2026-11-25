# Prompts utilizados

Prompts principales usados con el agente de IA, agrupados por tarea.

## 1. Script de la base de datos

> Partiendo del diagrama de la prueba, ayúdame a escribir el script de SQL Server. Hay que
> agregar lo que le falta al modelo para cumplir los requisitos: velocidad y precio en el
> servicio, monto y contrato en los pagos, identificación del usuario, una tabla para saber
> qué cajero tiene abierta cada caja (los 2 cajeros no pueden operarla a la vez), el menú por
> rol y la recuperación de contraseña. Todas las tablas con borrado lógico.

> Ahora los procedimientos almacenados: uno que genere el código del turno (2 letras del tipo
> de atención + 4 números, correlativo por día) sin que se repitan si dos gestores generan a
> la vez; otro para el cambio de servicio (el contrato actual pasa a SUS y se crea uno nuevo
> en REN con la misma fecha fin), y los indicadores del dashboard.

Lo que se tomó de la respuesta:

- Tabla `cashsession` con índices únicos filtrados (`WHERE closedat IS NULL`), así la propia
  base impide que una caja tenga dos sesiones abiertas o que un cajero abra dos cajas.
- `sp_generate_turn` con `UPDLOCK, HOLDLOCK` para que el correlativo no se duplique en
  concurrencia.
- Datos iniciales (roles, estados, tipos de atención, planes, cajas, menú por rol).

Archivos: `Backend/database/01_schema.sql`, `02_functions_procedures.sql`, `03_seed.sql`.

## 2. Diseño visual y animaciones del frontend (skills frontend-design y GSAP)

> Ayúdame con la dirección visual de la aplicación usando la skill de frontend-design. Es la
> caja y los turnos de un proveedor de internet; no quiero que parezca una plantilla de
> panel de administración.

> Con GSAP quiero que el código del turno aparezca como en los tableros de las ventanillas:
> cada casilla gira por caracteres y se detiene de izquierda a derecha, y el tablero se
> ilumina. En el login, unos hilos de fibra óptica que se dibujan una vez y luego pulsos de
> luz que los recorren. Que respete prefers-reduced-motion y que se limpie al salir de la
> pantalla, porque es Angular.

Detalle de cómo se aplicó cada skill: [skills/frontend-design.md](skills/frontend-design.md)
y [skills/gsap.md](skills/gsap.md).

## 3. Bug: `ng build` compila pero el proceso nunca termina

> El `ng build` dice "Application bundle generation complete" en 11 segundos, pero el proceso
> se queda abierto y no devuelve el control. En Docker esto dejaría colgado el build de la
> imagen. Ayúdame a encontrar qué lo mantiene vivo.

Cómo se resolvió:

1. Se listaron los recursos activos de Node al terminar el build: solo quedaba vivo el
   proceso hijo de **esbuild** (`esbuild.exe --service --ping`).
2. Se probó esbuild solo, con un build y un `context()` + `dispose()`: terminaba bien. El
   problema estaba en cómo lo usaba el builder de Angular.
3. Se compiló un `main.ts` trivial y seguía colgado, así que no era el código de la app
   sino la configuración o los estilos.
4. Se envolvió `esbuild.context()` con un `Proxy` para registrar qué contextos nunca se
   cerraban: quedaban 2, ambos de estilos de componente con la **misma clave**, de
   `auth-pages.component.ts`.
5. Causa: los 3 componentes de ese archivo (login, recuperar y restablecer contraseña)
   compartían la misma constante de estilos en línea. El builder crea un contexto por
   componente con la misma clave, los sobrescribe en su caché y nunca cierra los dos
   primeros.

Solución: mover esos estilos a `auth-pages.component.scss` y usar `styleUrl` en los tres
componentes. El build pasó a terminar solo (`exit 0`).

## 4. Pruebas de seguridad del JWT

> Hazme pruebas de seguridad del JWT contra la API levantada en Docker. Quiero comprobar que
> no se puede entrar sin token, con un token alterado, con `alg: none`, con un token cifrado
> con otra clave o ya expirado; que un cajero no pueda usar endpoints de administrador; y
> que el token deje de servir cuando el usuario cierra sesión o lo bloquean. Déjalas en
> Postman para poder repetirlas.

Resultado: carpeta **10. Seguridad JWT** de la colección de Postman (16 peticiones).

| Prueba | Respuesta esperada |
|--------|--------------------|
| El token del login es un JWE de 5 partes (`dir` + `A256CBC-HS512`) y sus claims no se leen sin la clave | 200 |
| Sin token, token mal formado o alterado en un carácter | 401 |
| JWT con `alg: none` y rol de administrador | 401 |
| JWT firmado con la clave real pero sin cifrar | 401 |
| JWE cifrado con otra clave | 401 |
| Token expirado | 401 |
| Cajero en `/api/users` o en el dashboard | 403 |
| Token de un usuario que el administrador bloqueó | 401 |
| Token después de cerrar sesión | 401 |

**Hallazgo corregido:** la API aceptaba un JWT **solo firmado** (sin cifrar) si la firma era
válida, con lo que las claims viajaban legibles. Ahora `OnTokenValidated` rechaza cualquier
token que no venga cifrado (`ApiServiceExtensions.cs`).

Nota: las claves por defecto de `docker-compose.yml` son públicas en el repositorio; en un
despliegue real se reemplazan con variables de entorno (`JWT_SIGNING_KEY`,
`JWT_ENCRYPTION_KEY`, `DATA_KEY`, `DATA_HASH_KEY`).
