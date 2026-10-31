# Recursos de Base de Datos (HOTEL)

Este directorio contiene el diseño y los artefactos de la base de datos `HOTEL`.

## Archivos

| Archivo | Propósito |
|---|---|
| `init-docker.sql` | Script SQL Server autocontenido: crea la BD `HOTEL`, tablas, vista, índices, restricciones y datos semilla. Fuente de diseño de referencia. |
| `hoteleria-schema.sql` | Script de esquema **generado por EF Core Migrations** (`dotnet ef migrations script`). Incluye `__EFMigrationsHistory` (historial tipo Flyway). |
| `Dockerfile` | Imagen SQL Server que auto-provisiona la BD corriendo `init-docker.sql` en el primer arranque. |
| `plantillas-hoteleria.excalidraw` | Diagrama / plantillas del modelo. |

## Modelo (entidades)

`Pisos`, `Tipo Vip`, `Habitaciones`, `Usuarios`, `Reservas`, `Reserva Habitaciones` (N:M) y la vista `TotalPrice_Habitacion_plus_Codigo`.
Mapeado en `Hoteleria-App/Data/HoteleriaDbContext.cs`.

## Dos formas de aprovisionar la BD

### 1) La app migra + siembra (EF Core Migrations — recomendado)
Un SQL Server "limpio" (sin BD) y la app aplica las migraciones y datos al arrancar:

```bash
docker compose up -d          # SQL Server vacío en :1433
dotnet run                    # Migrate() + DbSeeder crean HOTEL (DB_AUTO_MIGRATE=true)
```

### 2) La imagen de DB ya viene provisionada (Dockerfile)
Construye una imagen que crea la BD con `init-docker.sql` al primer arranque:

```bash
docker build -t hoteleria-sql -f Resources/DDBB/Dockerfile ./Resources/DDBB
docker run -d --name hoteleria-mssql -p 1433:1433 \
  -e MSSQL_SA_PASSWORD="YourStrong@Passw0rd" hoteleria-sql
```

En este caso la app **no** debe migrar: arranca con `DB_AUTO_MIGRATE=false`
(o `UseDatabase=true` + migraciones ya aplicadas).

## Configuración por variables de entorno

Ver `.env.example` en la raíz. Los valores críticos (contraseña SA, host/puerto,
nombre de BD) se leen de variables (`DB_PASSWORD`, `DB_SERVER`, `DB_NAME`, `DB_USER`,
`DB_TRUST_CERT`), tanto por Docker Compose como por la app (`Program.cs`).
