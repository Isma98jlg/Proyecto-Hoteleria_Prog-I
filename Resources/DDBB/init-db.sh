#!/usr/bin/env bash
set -euo pipefail

/opt/mssql/bin/sqlservr &
sql_pid=$!
trap 'kill -TERM "$sql_pid" 2>/dev/null || true; wait "$sql_pid"' SIGTERM SIGINT

ready=0
for attempt in $(seq 1 60); do
    if /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U "${DB_USER:-sa}" -P "$MSSQL_SA_PASSWORD" -C -b -Q "SELECT 1" >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 2
done

if [ "$ready" -ne 1 ]; then
    echo "SQL Server did not become ready; database initialization was not run." >&2
    exit 1
fi

# SQL Server can accept master connections while the first database recovery is
# still running. Give that initial recovery time to finish before issuing USE HOTEL.
sleep 20

/opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U "${DB_USER:-sa}" -P "$MSSQL_SA_PASSWORD" -C -b \
    -i /docker-entrypoint-initdb.d/01_init.sql

wait "$sql_pid"
