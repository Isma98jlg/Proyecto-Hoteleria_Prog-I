# Guía para agentes y colaboradores

Pasos y reglas para preparar, ejecutar, probar y desplegar Hoteleria-App.

## Estructura relevante

- `Hoteleria-App/`: aplicación ASP.NET Core Razor Pages (.NET 10).
  - `Services/`: reglas de negocio. `ReservaService` y `CuentaService` son el único punto de entrada para reservas, cuentas y cargos.
  - `Security/`: políticas de permisos y su caché.
  - `Hubs/`: SignalR. `Api/`: API REST `/api/v1`.
  - `Migrations/`: fuente de verdad del esquema.
  - `Services/Ml/`: recomendación y ranking de habitaciones con ML.NET.
- `docker-compose.yml`: entorno local completo (app, PostgreSQL y Redis) en una red propia de Docker.
- `Dockerfile` y `render.yaml`: imagen de la app y Blueprint de Render.
- `Resources/DDBB/`: script SQL generado desde las migraciones y consultas de verificación.
- `scripts/smoke_test.py`: recorrido de integración; crea datos, úsalo solo en una base desechable.

## Reglas de seguridad y configuración

- **Sin credenciales en el código.** Contraseñas, claves y cadenas de conexión llegan por variables de entorno (o `.env` en local). No añadas valores de respaldo: si falta una variable obligatoria, la app se detiene con un mensaje claro.
- **Obligatoria:** `DATABASE_URL`. En Compose se forma con `POSTGRES_USER`, `POSTGRES_PASSWORD` y `POSTGRES_DB`.
- **Administrador:** `ADMIN_EMAIL` y `ADMIN_PASSWORD` solo se usan para crearlo cuando no existe ninguno, y son obligatorias en ese caso. Después, su nombre, correo y contraseña se cambian en la app, en «Mis datos» y «Cambiar contraseña»; editar las variables no modifica la cuenta y se pueden retirar.
- **Autenticación en la base de datos** con ASP.NET Core Identity, sin proveedores externos.
- `.env` no se versiona. Antes de un commit, comprueba `git status` y `git check-ignore .env`. No imprimas contraseñas en logs, informes ni commits.
- No agregues `bin/`, `obj/`, archivos de base de datos ni datos de prueba.

## Reglas de negocio que no se deben saltar

- **Correos:** el correo de acceso siempre usa el dominio único `DOMINIO_CORREO` (por defecto `grandstay.com`) y no es un buzón real. `Usuarios.correo_contacto` guarda el correo real, de cualquier proveedor, para futuros envíos; cualquier correo saliente debe usar ese campo.
- **Cuentas:** el registro público siempre crea un huésped. El personal entra por invitación del administrador y el cargo lo define la invitación.
- **Verificación de documentos (registro público y ventanilla):** documentos aceptados: DNI, pasaporte, CPP y carné de extranjería. DNI con MigoAPI (padrón de SUNAT, devuelve el nombre en un solo texto: apellidos primero) y, si no lo resuelve, RENIEC por ApisTed con el mismo `APISTED_TOKEN`; CE y CPP con ApisTed (Migraciones, puede tardar hasta 40 s) y pasaporte con Luhnify (solo formato). La verificación es automática al completar el documento y exige el consentimiento marcado: nombres, apellidos y nacionalidad se rellenan con los datos oficiales y quedan bloqueados (el pasaporte se escribe a mano, porque Luhnify solo comprueba el formato). Como devuelve datos oficiales, el registro público tiene 5 consultas por hora por IP; el personal, 40 por hora. La fecha de nacimiento solo se pide para CE y CPP, se envía a ApisTed y no se guarda. Si la API no confirma o el huésped continúa sin verificar, escribe sus datos y la cuenta se crea «sin verificar». El estado vale solo para los datos comprobados: cambiar el documento o los nombres lo reinicia.
- **Cargos:** `Usuarios.cargo_id` y los roles de Identity solo se cambian con `CuentaService.AsignarCargoAsync`.
- **Huésped:** solo crea y consulta sus reservas en `/Portal`, y ve sus datos. No accede a Habitaciones ni a la API; solo ve las habitaciones disponibles al reservar y las suyas en el detalle.
- **Datos personales:** el directorio y las fichas exigen `crud_usuarios`. Los avisos de SignalR y el índice de Algolia no llevan datos personales.
- **Estados de reserva:** toda reserva nueva nace `CONFIRMADA`, porque bloquear la habitación es lo único que hay que validar (no hay pagos). Después: `CONFIRMADA → EN_CURSO → FINALIZADA`; se puede cancelar antes del check-in. No hay transiciones hacia atrás. `PENDIENTE` queda solo para reservas antiguas.
- **Eliminar huéspedes:** solo con `CuentaService.EliminarHuespedAsync`. Sin reservas ni cuenta, cualquiera con `crud_usuarios`; con ellas, solo el administrador, y se borran sus reservas, sus invitaciones y su cuenta en la misma transacción (no se puede deshacer). Si está alojado (reserva «En curso»), primero el check-out.
- **Disponibilidad:** la decide PostgreSQL. La restricción `ex_habitacion_sin_solapes` impide solapes; las columnas `inicio`, `fin` y `activa` de `Reserva Habitaciones` deben coincidir siempre con la reserva.
- **Elegir habitación:** usa siempre `_SelectorHabitaciones` y `SelectorHabitaciones`. Personas equivale a camas (coincidencia exacta), aunque una reserva puede combinar habitaciones. Lo que no coincide no se oculta: aparece debajo como «Otras habitaciones libres en esas fechas». Algolia filtra y PostgreSQL decide qué está libre.
- **ML.NET (recomendación y ranking):** solo en el portal del huésped, y solo ordena y destaca: Algolia filtra y PostgreSQL decide qué está libre. La recomendación (factorización de matrices) aprende de las reservas no canceladas y marca «Para ti» (sin historial, «Más reservada»). El ranking (LightGBM) aprende de `Busquedas` y `Busqueda Resultados`: qué se mostró en cada búsqueda del portal y qué se reservó. Las búsquedas no guardan el texto escrito, se borran con el huésped, y se conservan 30 días sin reserva y 180 con ella. Los modelos viven en memoria y se reentrenan al arrancar, cada 6 horas o desde `/Modelos` (admin). Si fallan o faltan datos, el selector ordena como antes. El personal no ve insignias ni se registran sus búsquedas.
- **Número de habitación:** es la clave primaria y no cambia. Empieza por su piso y sigue con dos dígitos (piso 4 → 401 a 499); `CodigoHabitacion.EncajaConPiso` lo valida al crear y al cambiar de piso. Se muestra como `4V-401` (piso, sigla de la categoría y número).
- **Estado de la habitación:** `OCUPADO` solo lo cambian el check-in y el check-out. Lo que se muestra es la **situación de hoy** (`HabitacionService.SituacionesHoyAsync`): estado + reservas, así una habitación libre con una reserva que cubre hoy figura «Reservada». No muestres `Habitacion.Estado` en crudo. `RevisarConsistenciaAsync` alimenta la «Revisión de consistencia» del panel (ocupada sin estancia, estancia con la habitación libre, check-out vencido, llegada no registrada, líneas desalineadas) y no corrige nada sola. No se pone en mantenimiento una habitación con una reserva para hoy.
- **IA (asistente del huésped, `/Portal/Asistente`; el personal no tiene chat):** disponibilidad y precios salen de `ReservaService`, y el modelo no crea reservas. `preparar_reserva` deja una propuesta validada en el servidor, y solo el botón «Confirmar reserva» la crea, a nombre de quien inició sesión. Solo atiende reservas y dudas del sistema: otros temas reciben una respuesta fija del servidor (`fuera_de_tema`) y no se reenvían al modelo. El saldo de la API es escaso: cuida los tokens en cada cambio (instrucciones cortas, resultados compactos, sin llamadas innecesarias) y mantén los límites `ASISTENTE_LIMITE_USUARIO` y `ASISTENTE_LIMITE_DIARIO`. La conversación vive en Redis 30 minutos y nunca se registra su contenido.

## Ventanas flotantes

Los formularios y detalles se abren sobre la página actual. Para sumar una ruta, añádela a `RUTAS_FLOTANTES` en `wwwroot/js/ventana-flotante.js`: los enlaces se interceptan solos y la página se muestra con `_LayoutModal`. `VentanaFlotanteMiddleware` convierte las redirecciones de `?modal=1` en un aviso a la página principal.

## Estilos

- Tailwind está **precompilado** en `Hoteleria-App/wwwroot/css/tailwind.css` (se versiona); el navegador ya no descarga Tailwind. Tras cambiar clases en vistas o JavaScript, regenera con Node: `npm install` (una vez) y `npm run css`. La paleta y la tipografía viven en `tailwind.config.js`; `site.css` usa las mismas variables (`--gs-marino`, `--gs-laton`…).
- Paleta formal: azul marino para las acciones, latón como acento y grises fríos. No uses colores sueltos (`bg-orange-500`, `#ea580c`): usa los tokens (`bg-primary`, `text-secondary`, `bg-tertiary-fixed`…).
- Los archivos con `asp-append-version` se guardan un año en el navegador; CSS, JS y SVG van comprimidos (Brotli/Gzip). El HTML no se comprime a propósito (BREACH).

## Avisos y envíos

- **Resultado de cada acción:** después de crear, guardar o eliminar, deja el mensaje en `TempData["MensajeExito"]` o `TempData["MensajeError"]`. `_AvisoGlobal` (en los tres layouts y en el login) lo muestra arriba en cualquier página, también al cerrarse una ventana flotante. `_Mensajes` solo muestra enlaces que hay que copiar.
- **Filtros como una sola búsqueda:** en una página con filtros, ninguno borra a los demás. Los enlaces de filtro (chips, páginas, exportar) y las redirecciones tras una acción usan `FiltrosActuales.Con(...)`, que parte de todos los filtros activos y solo cambia uno; los formularios de búsqueda llevan como campo oculto los filtros que se eligen fuera de ellos. Las listas de opciones (categorías, cargos…) salen de la base de datos, no de valores fijos.
- **Sin envíos dobles:** `wwwroot/js/envio-seguro.js` (cargado desde `_Head`) bloquea los botones de todo formulario hasta la respuesta y muestra «Guardando…», «Creando…», etc. Los botones que hacen `fetch` usan `window.envioSeguro.bloquear(boton)` o `mientras(boton, promesa)`. Un formulario que no deba bloquearse lleva `data-sin-bloqueo`.

## Primera ejecución

Requisito: Docker Desktop.

```powershell
Copy-Item .env.example .env   # y cambia todos los valores
docker compose up -d --build
```

Abre `http://localhost:5003`. Para depurar con .NET SDK 10, levanta solo `postgres` y `redis` y ejecuta la app en el host con `dotnet run --project .\Hoteleria-App\Hoteleria-App.csproj --launch-profile http`; en ese caso usa `DATABASE_URL` y `REDIS_URL` de `.env`. Con `DB_AUTO_MIGRATE=true` (valor por defecto), el arranque aplica migraciones y crea los datos de referencia y el administrador. `DEMO_ACCOUNTS=true` crea además un trabajador y un huésped de prueba. `DEMO_HISTORIAL=true` crea una sola vez 36 huéspedes con nombres comunes, DNI del rango 99xxxxxx (sin asignar por RENIEC) y sin correo de contacto, con reservas pasadas y búsquedas para entrenar ML.NET.

Si la clave de `DATABASE_URL` tiene símbolos, escríbela con formato URL (`@` es `%40`).

## Integraciones opcionales

Sin sus variables, la app sigue funcionando:

| Variable | Sin ella |
|---|---|
| `REDIS_URL` | Caché en memoria y SignalR en una sola instancia |
| `ALGOLIA_APP_ID`, `ALGOLIA_ADMIN_KEY` | La búsqueda usa PostgreSQL |
| `ANTHROPIC_API_KEY` | El asistente del huésped queda desactivado |
| `GEOAPIFY_API_KEY` | «Explora tu zona» queda desactivado |
| `MIGO_API_TOKEN`, `APISTED_TOKEN`, `LUHNIFY_API_KEY` | El registro funciona, pero el documento queda «sin verificar» |

«Explora tu zona» (`/Portal/Explora`) usa Geoapify por el servidor y mapa de OpenStreetMap, y debe mostrar la atribución de ambos. La ubicación se redondea a unos 110 m y no se asocia al usuario ni se registra; cada consulta de hasta 20 lugares cuesta 1 crédito, por eso va en caché una hora.

El clima del huésped (`/Portal/Clima`) usa Open-Meteo, sin clave. La ubicación solo se pide al pulsar el botón, se redondea a unos 11 km y no se asocia al usuario ni se registra.

`/health` informa del estado de Redis (`conectado`, `sin_conexion` o `no_configurado`). Solo la base de datos hace que devuelva 503, porque Redis es opcional.

## Verificación

```powershell
dotnet build .\Hoteleria-App\Hoteleria-App.csproj
```

Para el recorrido completo, levanta la app contra una **base vacía** con `DEMO_ACCOUNTS=true` y define en el entorno `BASE_URL`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `DEMO_WORKER_EMAIL`, `DEMO_WORKER_PASSWORD`, `DEMO_USER_EMAIL` y `DEMO_USER_PASSWORD`:

```powershell
python .\scripts\smoke_test.py
```

## Despliegue en Render

1. Sube la rama a GitHub.
2. En Render: **New > Blueprint**, elige el repositorio y usa `render.yaml`.
3. Completa `ADMIN_EMAIL`, `ADMIN_PASSWORD` y `HOTEL_TIMEZONE`, y las claves opcionales de Algolia y Anthropic. `ADMIN_EMAIL` debe usar el dominio de `DOMINIO_CORREO` (por ejemplo, `admin@grandstay.com`); con otro dominio, la app se detiene al crear el administrador. El correo real se añade después, en la app.
4. Comprueba `/health` y entra con el administrador.

En el plan gratuito el servicio se duerme tras 15 minutos, Postgres expira a los 30 días y Key Value pierde sus datos al reiniciarse.

## Detener y conservar los datos

```powershell
docker compose stop
```

`docker compose down` conserva el volumen `hoteleria-pg-data`. **No uses `docker compose down -v`** salvo que el usuario haya pedido borrar la base local.

## Cambios de base de datos

- Crea una migración para cada cambio de esquema y regenera `Resources/DDBB/hoteleria-schema.sql`:

```powershell
dotnet ef migrations script --idempotent --project .\Hoteleria-App\Hoteleria-App.csproj -o .\Resources\DDBB\hoteleria-schema.sql
```

- No edites el volumen a mano ni versiones contenedores como sustituto de los archivos fuente.

## Git y publicación

El remoto configurado se llama `rep-origin`. Verifica `git remote -v` antes de publicar y no hagas push sin una instrucción explícita.
