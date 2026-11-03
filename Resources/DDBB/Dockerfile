# ============================================================================
# Imagen SQL Server para Hoteleria. El entrypoint espera a que SQL Server esté
# listo y ejecuta init-docker.sql para crear la estructura de HOTEL.
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

# Inicializador real; la imagen oficial no ejecuta SQL automáticamente.
COPY --chmod=755 ./init-db.sh /usr/local/bin/init-db.sh
COPY ./init-docker.sql /docker-entrypoint-initdb.d/01_init.sql

ENTRYPOINT ["/usr/local/bin/init-db.sh"]

EXPOSE 1433
