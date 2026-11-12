#!/bin/bash
# Prueba en un entorno desechable: compila, levanta PostgreSQL y Redis en Docker, la API falsa y la app en
# localhost:5077, corre la prueba, imprime solo el resumen y lo borra todo. Ninguna clave real.
#
#   bash scripts/pruebas/correr.sh smoke        # recorrido completo (scripts/smoke_test.py), unos 12 s
#   bash scripts/pruebas/correr.sh registro     # o ml, situacion, eliminar, asistente, explora
#   MANTENER=1 bash scripts/pruebas/correr.sh smoke   # deja el entorno vivo para mirarlo en el navegador
#
# Requisitos: Docker Desktop encendido, .NET SDK 10, Python 3 y openssl (Git Bash).
set -u
PRUEBA="${1:-smoke}"
R="$(cd "$(dirname "$0")/../.." && pwd)"
D="$R/scripts/pruebas"
W="${HOTELERIA_PRUEBAS:-${TMPDIR:-/tmp}/hoteleria-pruebas}"
mkdir -p "$W"

# Detener un entorno dejado vivo con MANTENER=1.
if [ "$PRUEBA" = parar ]; then
    for pid in $(cat "$W/pids" 2>/dev/null); do taskkill //F //T //PID "$pid" >/dev/null 2>&1 || kill "$pid" >/dev/null 2>&1; done
    docker rm -f hoteleria-prueba-pg hoteleria-prueba-redis >/dev/null 2>&1
    rm -f "$W/prueba.env" "$W/pids"
    echo "Entorno detenido."
    exit 0
fi

if ! docker info >/dev/null 2>&1; then echo "Docker no responde: encienda Docker Desktop."; exit 1; fi

# Compilación limpia: una carpeta reutilizada puede quedar sin alguna dependencia.
rm -rf "$W/app"
dotnet build "$R/Hoteleria-App/Hoteleria-App.csproj" -o "$W/app" -nologo -v q > "$W/build.txt" 2>&1 || { grep -E " error " "$W/build.txt" | sort -u | head; exit 1; }
rm -rf "$W/app/wwwroot" && cp -r "$R/Hoteleria-App/wwwroot" "$W/app/"

# Contraseñas de prueba nuevas en cada corrida; nunca se imprimen.
printf 'PG=%s\nAD=Prueba-%s\nHU=Prueba-%s\nTR=Prueba-%s\n' "$(openssl rand -hex 16)" "$(openssl rand -hex 8)" "$(openssl rand -hex 8)" "$(openssl rand -hex 8)" > "$W/prueba.env"
. "$W/prueba.env"

docker rm -f hoteleria-prueba-pg hoteleria-prueba-redis >/dev/null 2>&1
docker run -d --name hoteleria-prueba-pg -e POSTGRES_USER=hotel -e POSTGRES_PASSWORD="$PG" -e POSTGRES_DB=hotel -p 127.0.0.1:15439:5432 postgres:17-alpine >/dev/null
docker run -d --name hoteleria-prueba-redis -p 127.0.0.1:16389:6379 redis:7-alpine >/dev/null
until docker exec hoteleria-prueba-pg pg_isready -U hotel >/dev/null 2>&1; do sleep 1; done

python -I "$D/apis_falsas.py" > "$W/apis.log" 2>&1 &
PID_APIS=$!

export DATABASE_URL="postgres://hotel:$PG@127.0.0.1:15439/hotel?sslmode=disable" REDIS_URL="redis://127.0.0.1:16389"
export ADMIN_EMAIL="admin@grandstay.com" ADMIN_PASSWORD="$AD" DEMO_ACCOUNTS=true
export DEMO_WORKER_EMAIL="trabajador@grandstay.com" DEMO_WORKER_PASSWORD="$TR" DEMO_USER_EMAIL="huesped@grandstay.com" DEMO_USER_PASSWORD="$HU"
export ANTHROPIC_API_KEY="clave-falsa-de-prueba" ANTHROPIC_BASE_URL="http://127.0.0.1:18999"
export GEOAPIFY_API_KEY="clave-falsa-de-prueba" GEOAPIFY_BASE_URL="http://127.0.0.1:18999"
export MIGO_API_TOKEN="clave-falsa-de-prueba" MIGO_API_BASE_URL="http://127.0.0.1:18999"
export APISTED_TOKEN="clave-falsa-de-prueba" APISTED_BASE_URL="http://127.0.0.1:18999/api"
export LUHNIFY_API_KEY="clave-falsa-de-prueba" LUHNIFY_BASE_URL="http://127.0.0.1:18999"
export ASPNETCORE_URLS="http://localhost:5077" ASPNETCORE_ENVIRONMENT=Production HOTEL_TIMEZONE="America/Bogota"
# El recorrido completo prueba el límite real de peticiones; las demás pruebas hacen muchas seguidas.
export RATE_LIMIT_POR_MINUTO=$([ "$PRUEBA" = smoke ] && echo 20 || echo 500)
[ "$PRUEBA" = ml ] && export DEMO_HISTORIAL=true

(cd "$W/app" && exec dotnet Hoteleria-App.dll --contentRoot "$W/app" > "$W/app.log" 2>&1) &
PID_APP=$!
# PID de Windows de cada proceso (Git Bash los expone en /proc/<pid>/winpid), para poder detenerlos después.
sleep 1
for pid in $PID_APP $PID_APIS; do cat "/proc/$pid/winpid" 2>/dev/null; done > "$W/pids"

limpiar() {
    # Se detiene por PID (no por patrón de texto, que podría coincidir con este mismo script).
    for pid in $PID_APP $PID_APIS; do
        [ -r "/proc/$pid/winpid" ] && taskkill //F //T //PID "$(cat /proc/$pid/winpid)" >/dev/null 2>&1
        kill "$pid" >/dev/null 2>&1
    done
    for pid in $(cat "$W/pids" 2>/dev/null); do taskkill //F //T //PID "$pid" >/dev/null 2>&1; done
    docker rm -f hoteleria-prueba-pg hoteleria-prueba-redis >/dev/null 2>&1
    rm -f "$W/prueba.env" "$W/pids"
}
[ "${MANTENER:-0}" = 1 ] || trap limpiar EXIT

for i in $(seq 1 120); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5077/health)" = 200 ] && break
    if ! kill -0 "$PID_APP" 2>/dev/null; then echo "La app no arrancó:"; grep -aE "^(fail|crit)|Exception" "$W/app.log" | head -5; exit 1; fi
    sleep 2
done

inicio=$(date +%s)
if [ "$PRUEBA" = smoke ]; then
    export BASE_URL=http://localhost:5077 PYTHONUTF8=1
    python -I "$R/scripts/smoke_test.py" > "$W/resultado.txt" 2>&1
    grep -aE "^FAIL|FALLOS|concurrencia:" "$W/resultado.txt"
else
    PRUEBA_ENV="$W/prueba.env" PRUEBA_LOG="$W/app.log" PYTHONUTF8=1 python -I "$D/$PRUEBA.py" > "$W/resultado.txt" 2>&1
    grep -av "^OK" "$W/resultado.txt"
fi
echo "duración: $(( $(date +%s) - inicio )) s · interbloqueos reintentados: $(grep -c 'will be retried' "$W/app.log")"
if [ "${MANTENER:-0}" = 1 ]; then echo "Entorno vivo en http://localhost:5077 (contraseñas en $W/prueba.env). Deténgalo con: bash $D/correr.sh parar"; fi
exit 0
