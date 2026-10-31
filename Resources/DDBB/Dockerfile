# ============================================================================
# Imagen de base de datos SQL Server para Hoteleria (auto-provisionada).
# En el PRIMER arranque, el entrypoint de mssql ejecuta los scripts de
# /docker-entrypoint-initdb.d/ (aquí init-docker.sql) y la base "HOTEL" queda
# creada con esquema + vista + índices + datos semilla.
#
# Construir (desde la raíz del repo):
#   docker build -t hoteleria-sql -f Resources/DDBB/Dockerfile ./Resources/DDBB
#
# Ejecutar:
#   docker run -d --name hoteleria-mssql -p 1433:1433 \
#     -e MSSQL_SA_PASSWORD="YourStrong@Passw0rd" hoteleria-sql
#
# La contraseña SA NO se hornea: se aporta en tiempo de ejecución (env).
# ============================================================================

FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y \
    MSSQL_PID=Developer

# Script de inicialización (esquema + semilla) — se ejecuta una sola vez.
COPY ./init-docker.sql /docker-entrypoint-initdb.d/01_init.sql

EXPOSE 1433
