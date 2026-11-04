# Guía para agentes y colaboradores

Este archivo reúne los pasos mínimos para preparar la base de datos local y ejecutar Hoteleria-App desde un clon limpio.

## Estructura relevante

- `docker-compose.yml`: servicio SQL Server y volumen persistente de la base de datos.
- `Resources/DDBB/Dockerfile`: construye la imagen local de SQL Server.
- `Resources/DDBB/init-db.sh`: espera a que SQL Server esté listo y ejecuta el SQL de inicialización.
- `Resources/DDBB/init-docker.sql`: crea `HOTEL` y su esquema inicial.
- `Resources/DDBB/hoteleria-schema.sql`: esquema generado a partir de las migraciones EF Core.
- `Resources/DDBB/verify-auth.sql`: consultas para verificar las tablas y las cuentas de autenticación.
- `Hoteleria-App/`: aplicación ASP.NET Core Razor Pages; se ejecuta en el host con .NET, no en el Compose actual.
- `scripts/smoke-test.ps1`: recorrido de integración que requiere la app y la base de datos levantadas.

El Compose configura únicamente SQL Server. No se versionan imágenes, contenedores ni volúmenes Docker: Docker Compose construye la imagen desde el Dockerfile y el código fuente.

## Requisitos

- Docker Desktop activo y con contenedores Linux habilitados.
- .NET SDK 10.
- PowerShell 7 para el script de humo (opcional).

## Primera ejecución desde un clon

Desde la raíz del repositorio, crea la configuración local:

```powershell
Copy-Item .env.example .env
```

Edita `.env` antes de iniciar. Define una contraseña fuerte para `DB_PASSWORD`; no guardes credenciales reales en Git ni compartas el archivo `.env`. El Compose usa `DB_PASSWORD` como `MSSQL_SA_PASSWORD` del contenedor. `.env` está excluido por `.gitignore`; `.env.example` solo documenta las variables necesarias.

Construye e inicia SQL Server:

```powershell
docker compose up -d --build mssql
docker compose ps
docker compose logs -f mssql
```

Espera a que el servicio `mssql` aparezca como `healthy`. Su healthcheck prueba la conexión con `sqlcmd`; el entrypoint ejecuta `init-docker.sql` después de que SQL Server esté disponible.

Luego inicia la aplicación:

```powershell
dotnet restore .\Hoteleria-App\Hoteleria-App.csproj
dotnet run --project .\Hoteleria-App\Hoteleria-App.csproj --launch-profile http
```

El perfil `http` configura `Development` y `http://localhost:5003`. La aplicación encuentra el `.env` en la raíz del repositorio aunque se ejecute desde su subcarpeta. Con `DB_AUTO_MIGRATE=true` (valor local por defecto), al arrancar aplica migraciones pendientes y siembra datos de referencia, cargos y cuentas de demostración.

## Acceso de demostración local

Las direcciones y contraseñas se configuran en `.env` y tienen valores de desarrollo en `.env.example`:

| Cargo | Variable de correo | Variable de contraseña |
|---|---|---|
| Administrador | `ADMIN_EMAIL` | `ADMIN_PASSWORD` |
| Trabajador | `DEMO_WORKER_EMAIL` | `DEMO_WORKER_PASSWORD` |
| Usuario | `DEMO_USER_EMAIL` | `DEMO_USER_PASSWORD` |

No uses estas credenciales de demostración en un entorno publicado. Los agentes deben leer los valores localmente y evitar imprimir contraseñas en logs, informes o commits.

## Verificación

Una vez iniciada la aplicación, `Resources/DDBB/verify-auth.sql` permite revisar el esquema y la cuenta inicial desde SSMS o Azure Data Studio.

Para ejecutar el recorrido de integración desde la raíz con PowerShell 7:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

El recorrido usa las cuentas de `.env`, crea registros temporales con prefijo de prueba y los elimina al terminar. No ejecutes varios recorridos simultáneos sobre la misma base.

## Detener y conservar los datos

```powershell
docker compose stop mssql
```

`docker compose down` también detiene y elimina el contenedor, pero conserva el volumen `hoteleria-auth-data`. **No uses `docker compose down -v`** salvo que el usuario haya pedido borrar la base local: elimina permanentemente ese volumen y sus datos.

## Cambios de base de datos

- Mantén las migraciones de `Hoteleria-App/Migrations/` como fuente de verdad del modelo EF Core.
- Si cambia el esquema, actualiza y revisa los scripts SQL iniciales para que un clon nuevo pueda aprovisionar la misma versión.
- No edites el volumen ni intentes versionar un contenedor como sustituto de los archivos fuente.
- Para problemas de conexión, confirma que Docker está activo, `mssql` está `healthy`, el puerto definido por `MSSQL_PORT` está libre y `DB_SERVER` coincide con ese puerto (por defecto `localhost,1433`).

## Git y publicación

- Confirma que `.env` no se ha añadido al índice antes de crear commits (`git status` y `git check-ignore .env`).
- No agregues `bin/`, `obj/`, archivos de base de datos, contraseñas, volúmenes ni datos de prueba.
- El remoto configurado en este checkout se llama `rep-origin`. Verifica `git remote -v` antes de publicar; no hagas push a un remoto sin una instrucción explícita.
