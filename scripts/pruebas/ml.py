"""ML.NET: historial de demostración, entrenamiento, orden e insignias del portal, registro de búsquedas y privacidad."""
import html
import http.cookiejar
import json
import os
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from datetime import date, timedelta

BASE = "http://localhost:5077"
fallos = []


def check(nombre, ok, extra=""):
    print(("OK   " if ok else "FALLA"), nombre, "" if ok else str(extra)[:500])
    if not ok:
        fallos.append(nombre)


class Cliente:
    def __init__(self):
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def get(self, ruta):
        try:
            with self.op.open(BASE + ruta, timeout=60) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def post(self, ruta, datos):
        try:
            with self.op.open(urllib.request.Request(BASE + ruta, urllib.parse.urlencode(datos, doseq=True).encode()), timeout=120) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def token(self, ruta):
        return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', self.get(ruta)[1]).group(1))

    def entrar(self, correo, clave):
        t = self.token("/Account/Login")
        self.post("/Account/Login", {"Input.Email": correo, "Input.Password": clave, "Input.RememberMe": "false", "__RequestVerificationToken": t})


def sql(consulta):
    return subprocess.run(["docker", "exec", "hoteleria-prueba-pg", "psql", "-U", "hotel", "-d", "hotel", "-tAc", consulta],
                          capture_output=True, text=True, encoding="utf-8").stdout.strip()


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])

# --- Historial de demostración
huespedes = int(sql("select count(*) from \"Usuarios\" where tipo_documento = 'DNI' and numero_documento like '99%'"))
reservas = int(sql("select count(*) from \"Reservas\" r join \"Usuarios\" u using (usuario_id) where u.numero_documento like '99%' and r.estado_reserva = 'FINALIZADA'"))
busquedas = int(sql("select count(*) from \"Busquedas\""))
check("36 huéspedes de demostración con DNI 99xxxxxx", huespedes == 36, huespedes)
check("  entre 3 y 6 reservas pasadas cada uno, todas finalizadas", 108 <= reservas <= 216, reservas)
check("  una búsqueda por reserva, cada una con exactamente una elegida", busquedas == reservas and sql("select count(*) from \"Busquedas\" b where (select count(*) from \"Busqueda Resultados\" r where r.busqueda_id = b.busqueda_id and r.elegida) <> 1") == "0", (busquedas, reservas))
check("  ninguna reserva de demostración bloquea habitaciones (todas inactivas)", sql("select count(*) from \"Reserva Habitaciones\" rh join \"Reservas\" r using (reserva_id) join \"Usuarios\" u using (usuario_id) where u.numero_documento like '99%' and rh.activa") == "0")
nombres = sql("select nombres || ' ' || apellidos from \"Usuarios\" where numero_documento like '99%' order by usuario_id limit 4").splitlines()
check("  nombres con aspecto real", all(re.fullmatch(r"[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+( [A-ZÁÉÍÓÚÑ][a-záéíóúñ]+){2,3}", n) for n in nombres), nombres)
check("  sin correo de contacto", sql("select count(*) from \"Usuarios\" where numero_documento like '99%' and correo_contacto is not null") == "0")

# --- Entrenamiento y página de modelos
s, pagina, _ = admin.post("/Modelos?handler=Entrenar", {"__RequestVerificationToken": admin.token("/Modelos")})
pagina = html.unescape(pagina)
check("el admin reentrena desde /Modelos", s == 200 and "Modelos reentrenados" in pagina, s)
check("  los dos modelos quedan activos", pagina.count(">Activo<") == 2, re.findall(r">(Activo|Sin modelo)<", pagina))
ndcg = re.findall(r"NDCG@3 (?:modelo|orden anterior)</small><strong[^>]*>(\d+)%", pagina)
check("  el ranking ordena mejor que el orden anterior (NDCG@3)", len(ndcg) == 2 and int(ndcg[0]) > int(ndcg[1]), ndcg)
print("     NDCG@3 modelo / orden anterior:", ndcg)

# --- Portal: huésped sin historial → «Más reservada»
huesped = Cliente()
huesped.entrar("huesped@grandstay.com", env["HU"])
inicio = (date.today() + timedelta(days=12)).isoformat()


def disponibles(cliente, ruta, **extra):
    s, cuerpo, _ = cliente.get(f"{ruta}?" + urllib.parse.urlencode({"handler": "Disponibles", "inicio": inicio, "noches": 2, **extra}))
    return json.loads(cuerpo) if s == 200 else {}


antes = int(sql("select count(*) from \"Busquedas\""))
r = disponibles(huesped, "/Portal/Reservar", personas=2)
check("portal: orden personalizado y búsqueda registrada", r.get("orden", "").startswith("personalizado") and r.get("busquedaId"), {k: r.get(k) for k in ("orden", "busquedaId")})
check("  sin historial, la insignia es «Más reservada»", [h["destacado"] for h in r["habitaciones"] if h.get("destacado")] == ["Más reservada"], [h.get("destacado") for h in r["habitaciones"]])
check("  guarda la búsqueda con todo lo mostrado", int(sql("select count(*) from \"Busquedas\"")) == antes + 1
      and sql(f"select count(*) from \"Busqueda Resultados\" where busqueda_id = {r['busquedaId']}") == str(len(r["habitaciones"])))
check("  no guarda el texto escrito, solo si lo hubo", "q" not in sql("select column_name from information_schema.columns where table_name = 'Busquedas'").split())

# --- Portal: con historial de lujo → «Para ti» en habitaciones de piso alto
uid = sql("select \"Id\" from \"AspNetUsers\" where \"Email\" = 'huesped@grandstay.com'")
lujo = sql("select habitacion_id from \"Habitaciones\" where nro_piso >= 4 and nro_camas = 2 order by 1").split()
for i, hab in enumerate(lujo * 2):
    rid = sql(f"insert into \"Reservas\" (usuario_id, estado_reserva, fecha_reserva_creacion, inicio_reserva, total_noches) values ({uid}, 'FINALIZADA', now(), current_date - {60 + i * 10}, 2) returning reserva_id").splitlines()[0]
    sql(f"insert into \"Reserva Habitaciones\" values ({rid}, {hab}, 300, current_date - {60 + i * 10}, current_date - {58 + i * 10}, false)")
admin.post("/Modelos?handler=Entrenar", {"__RequestVerificationToken": admin.token("/Modelos")})
r = disponibles(huesped, "/Portal/Reservar", personas=2)
para_ti = [h["codigo"] for h in r["habitaciones"] if h.get("destacado") == "Para ti"]
check("con historial de lujo: insignias «Para ti»", len(para_ti) >= 1, [h.get("destacado") for h in r["habitaciones"]])
primera = r["habitaciones"][0]
check("  la primera de la lista es de piso alto (como su historial)", str(primera["habitacionId"]) in lujo, (primera["codigo"], lujo))
print("     orden:", [h["codigo"] for h in r["habitaciones"] if h["coincide"]], "· para ti:", para_ti)

# --- Reservar desde esa búsqueda marca la elegida
elegida = primera["habitacionId"]
s, _, url = huesped.post("/Portal/Reservar", {"Inicio": inicio, "Noches": 2, "HabitacionIds": elegida, "BusquedaId": r["busquedaId"], "__RequestVerificationToken": huesped.token("/Portal/Reservar")})
check("reservar marca la búsqueda como reservada", sql(f"select reservada_en is not null from \"Busquedas\" where busqueda_id = {r['busquedaId']}") == "t", (s, url))
check("  y la habitación elegida", sql(f"select habitacion_id from \"Busqueda Resultados\" where busqueda_id = {r['busquedaId']} and elegida") == str(elegida))

ajena = sql("select busqueda_id from \"Busquedas\" b join \"Usuarios\" u using (usuario_id) where u.numero_documento like '99%' and reservada_en is not null limit 1")
marcadas_antes = sql(f"select string_agg(habitacion_id::text, ',') from \"Busqueda Resultados\" where busqueda_id = {ajena} and elegida")
r2 = disponibles(huesped, "/Portal/Reservar", personas=2)
libre = next(h["habitacionId"] for h in r2["habitaciones"] if h["coincide"] and h["habitacionId"] != elegida)
huesped.post("/Portal/Reservar", {"Inicio": inicio, "Noches": 2, "HabitacionIds": libre, "BusquedaId": ajena, "__RequestVerificationToken": huesped.token("/Portal/Reservar")})
check("una búsqueda ajena no se puede marcar", sql(f"select string_agg(habitacion_id::text, ',') from \"Busqueda Resultados\" where busqueda_id = {ajena} and elegida") == marcadas_antes)

# --- El personal: sin personalizar ni registrar
antes = int(sql("select count(*) from \"Busquedas\""))
r = disponibles(admin, "/Reservas/Crear", personas=2)
check("personal: sin insignias, sin búsqueda registrada", r.get("habitaciones") and not any(h.get("destacado") for h in r["habitaciones"]) and not r.get("busquedaId") and int(sql("select count(*) from \"Busquedas\"")) == antes, r.get("orden"))

# --- Eliminar al huésped borra sus búsquedas
victima = sql("select usuario_id from \"Usuarios\" where numero_documento like '99%' order by usuario_id limit 1")
tenia = sql(f"select count(*) from \"Busquedas\" where usuario_id = {victima}")
admin.post(f"/Usuarios/Editar/{victima}?handler=Eliminar", {"__RequestVerificationToken": admin.token("/Usuarios/Crear")})
check("eliminar al huésped borra sus búsquedas en cascada", tenia != "0" and sql(f"select count(*) from \"Busquedas\" where usuario_id = {victima}") == "0", tenia)

# --- Sin datos personales en el log
registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
check("el log resume el entrenamiento con cantidades", "Modelos entrenados:" in registro)
check("  y no nombra huéspedes", not any(n.split()[0] in registro for n in nombres))
limpio = re.sub(r"^fail: Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]$", "", registro, flags=re.M)
check("sin errores inesperados", not re.search(r"^(fail|crit):", limpio, re.M), re.findall(r"^(?:fail|crit):.*\n.*", limpio, re.M)[:3])
print("\nFALLOS:", fallos if fallos else "ninguno")
