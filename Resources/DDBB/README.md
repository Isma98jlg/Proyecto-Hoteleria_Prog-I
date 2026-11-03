# Recursos de Base de Datos (HOTEL)

Este directorio contiene el diseño y los artefactos de la base de datos `HOTEL`.

## Archivos

| Archivo | Propósito |
|---|---|
| `init-docker.sql` | Script SQL Server autocontenido e idempotente: crea `HOTEL` y aplica el esquema generado por las migraciones. |
| `hoteleria-schema.sql` | Script de esquema **generado por EF Core Migrations** (`dotnet ef migrations script`). Incluye `__EFMigrationsHistory` (historial tipo Flyway). |
| `Dockerfile`, `init-db.sh` | Imagen SQL Server y entrypoint que espera al servidor y ejecuta `init-docker.sql`. |
| `verify-auth.sql` | Comprobaciones de las tablas, cargo y cuenta admin después del arranque de la app. |
| `plantillas-hoteleria.excalidraw` | Diagrama / plantillas del modelo. |

## Modelo (entidades)

`Pisos`, `Tipo Vip`, `Habitaciones`, `Usuarios`, `Reservas`, `Reserva Habitaciones` (N:M) y la vista `TotalPrice_Habitacion_plus_Codigo`.
Mapeado en `Hoteleria-App/Data/HoteleriaDbContext.cs`.

## Dos formas de aprovisionar la BD

### Inicio local con Docker y autenticación
El archivo `.env` debe existir en la raíz. El Compose construye la imagen de SQL Server del proyecto; el entrypoint espera la disponibilidad de SQL y ejecuta el esquema. Después, la app aplica migraciones pendientes y crea los datos de referencia, los cargos y la cuenta admin.

```bash
docker compose up -d --build  # SQL Server inicializado en :1433
cd Hoteleria-App
dotnet run                    # migraciones + sembrado (DB_AUTO_MIGRATE=true)
```

Abre `http://localhost:5003`; las páginas privadas redirigen a `/Account/Login`. Las cuentas vienen de `.env`:

| Cargo | Correo por defecto | Contraseña por defecto |
|---|---|---|
| admin | `admin@hoteleria.com` | `Admin123!` |
| trabajador | `trabajador@hoteleria.com` | `Worker123!` |
| usuario | `usuario@hoteleria.com` | `User123!` |

En Development, después del login la consola del navegador imprime usuario, cargo, roles y permisos. Nunca imprime contraseñas. Ejecuta `verify-auth.sql` después de que la app haya terminado de iniciar.

Después de iniciar la aplicación, ejecuta `verify-auth.sql` desde SSMS o Azure Data Studio conectado a la base `HOTEL`. El resultado debe incluir `resultado = OK`, el ID compartido, el cargo `admin` y las columnas de permisos.

`DB_AUTO_MIGRATE` debe permanecer en `true` para que el sembrado cree la cuenta y los cargos, incluso cuando `init-docker.sql` ya haya creado el esquema. El SQL inicial construye tablas; el hash de contraseña lo genera Identity desde la app, nunca queda escrito en el script.

## Configuración por variables de entorno

Ver `.env.example` en la raíz. Los valores críticos (contraseña SA, host/puerto,
nombre de BD) se leen de variables (`DB_PASSWORD`, `DB_SERVER`, `DB_NAME`, `DB_USER`,
`DB_TRUST_CERT`), tanto por Docker Compose como por la app (`Program.cs`).

### Variables nuevas para autenticación

A partir de la incorporación del sistema de login, se han añadido dos variables nuevas al archivo `.env` (y replicadas en `.env.example`):

- `ADMIN_EMAIL=admin@hoteleria.com` — Correo electrónico de la cuenta de administrador que se creará automáticamente al arrancar si no existe.
- `ADMIN_PASSWORD=Admin123!` — Contraseña de desarrollo del administrador (cambiar en producción).

Estas credenciales se almacenan en la tabla `AspNetUsers` de SQL Server (mediante ASP.NET Core Identity). La primera vez que la app inicia con `DB_AUTO_MIGRATE=true`, aplicará la migración `Identity` y creará la cuenta de admin con los datos definidos aquí.

### Modelo de personal y autenticación

La migración `IdentityAndHotelRoles` crea las tablas de autenticación y `Empleados`:

- `AspNetUsers` — Cuentas de acceso (UserName, Email, PasswordHash, SecurityStamp).
- `Roles` — Catálogo de cargos del hotel y roles de Identity en una sola tabla. Usa `cargo_id` como PK, `cargo_nombre` como nombre del cargo, y los permisos booleanos `ver`, `crud_requerimientos`, `crud_pisos`, `crud_usuarios`, `crud_habitaciones` y `crud_tipo_vip`.
- `AspNetUserRoles` — Tabla intermedia N:N entre usuarios y roles.
- `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens` — Metadatos de sesión y proveedores de login externos.
- `Empleados` — Extensión 1:1 de `Usuarios`; su PK `usuario_id` también es FK a `Usuarios.usuario_id`.

`AspNetUsers.Id` también es `bigint` y FK a `Usuarios.usuario_id`. Así la persona, su perfil laboral y su cuenta de acceso comparten el mismo ID. `Usuarios.cargo_id` referencia `Roles.cargo_id`; el cargo puede quedar vacío para un huésped. Los cargos iniciales son `usuario`, `admin` y `trabajador`. La aplicación solo permite autenticarse a cuentas de personas con un registro en `Empleados`. ASP.NET Identity emite una cookie de sesión y valida las credenciales almacenadas en SQL Server.

`hoteleria-schema.sql` es el script idempotente generado desde las migraciones. `init-docker.sql` crea la base y aplica ese esquema en una instalación nueva. Al iniciar la aplicación con `DB_AUTO_MIGRATE=true`, se siembran los datos de referencia y la cuenta admin; su ID debe corresponder a `Usuarios.usuario_id = 1`.
