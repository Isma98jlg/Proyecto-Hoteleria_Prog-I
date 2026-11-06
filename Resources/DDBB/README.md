# Base de datos

La base es PostgreSQL y las migraciones de `Hoteleria-App/Migrations/` son la fuente de verdad.

- `hoteleria-schema.sql`: script idempotente generado desde las migraciones. La app lo aplica por sí sola al arrancar; sirve para revisarlo o ejecutarlo a mano.
- `verify-auth.sql`: comprueba que existan los cargos, el administrador y la restricción contra solapes.
- `plantillas-hoteleria.excalidraw`: diseño del modelo.

En local, PostgreSQL se levanta con el `docker-compose.yml` de la raíz, dentro de la red de Docker del proyecto.
