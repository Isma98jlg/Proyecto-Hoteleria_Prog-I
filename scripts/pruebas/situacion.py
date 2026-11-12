"""Situación real de las habitaciones y revisión de consistencia."""
import html
import http.cookiejar
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
    print(("OK   " if ok else "FALLA"), nombre, "" if ok else str(extra)[:400])
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

    def post(self, ruta, datos, token_de=None):
        datos = dict(datos)
        datos["__RequestVerificationToken"] = self.token(token_de or "/Reservas")
        try:
            with self.op.open(urllib.request.Request(BASE + ruta, urllib.parse.urlencode(datos, doseq=True).encode()), timeout=60) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def token(self, ruta):
        return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', self.get(ruta)[1]).group(1))

    def entrar(self, correo, clave):
        self.post("/Account/Login", {"Input.Email": correo, "Input.Password": clave, "Input.RememberMe": "false"}, "/Account/Login")


def sql(c):
    return subprocess.run(["docker", "exec", "hoteleria-prueba-pg", "psql", "-U", "hotel", "-d", "hotel", "-tAc", c],
                          capture_output=True, text=True, encoding="utf-8").stdout.strip()


def situacion(c, h):
    cuerpo = html.unescape(c.get(f"/Habitaciones/Detalles/{h}")[1])
    m = re.search(r"Situación hoy</span><strong[^>]*>([^<]+)</strong>\s*(?:<span[^>]*>([^<]+)</span>)?", cuerpo)
    return (m.group(1).strip(), (m.group(2) or "").strip()) if m else ("?", "")


def panel(c):
    return html.unescape(c.get("/")[1])


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])
hoy = date.today().isoformat()
huesped = sql("select usuario_id from \"Usuarios\" u join \"Roles\" r on r.cargo_id = u.cargo_id where r.cargo_nombre = 'huesped' limit 1")

p = panel(admin)
check("sin reservas: «Todo en orden»", "Todo en orden" in p)
disponibles_antes = int(re.search(r"Disponibles hoy</span><strong[^>]*>(\d+)", p).group(1))

# --- Reserva para hoy de la 502 (la del caso real)
s, _, _ = admin.post("/Reservas?handler=Crear", {"Reserva.UsuarioId": huesped, "Reserva.InicioReserva": hoy, "Reserva.TotalNoches": 2, "Reserva.HabitacionIds": 502}, "/Reservas")
rid = sql("select max(reserva_id) from \"Reservas\"")
check("reserva para hoy creada", rid != "", s)
check("la 502 con llegada hoy figura «Reservada», no «Libre»", situacion(admin, 502) == ("Reservada", "Llega hoy · check-in pendiente"), situacion(admin, 502))
p = panel(admin)
check("  el panel ya no la cuenta como disponible", int(re.search(r"Disponibles hoy</span><strong[^>]*>(\d+)", p).group(1)) == disponibles_antes - 1)
check("  y dice «1 reservada(s) por llegar»", "1 reservada(s) por llegar" in p)
lista = html.unescape(admin.get("/Habitaciones?Estado=RESERVADA")[1])
check("  el filtro «Reservada hoy» la encuentra", "5V-502" in lista and "Mostrando 1 de" in lista, re.findall(r"Mostrando \d+ de \d+", lista))
check("  la tarjeta enlaza la reserva", f"/Reservas/Detalles?id={rid}" in lista or f"/Reservas/Detalles/{rid}" in lista)
libres = html.unescape(admin.get("/Habitaciones?Estado=LIBRE")[1])
check("  ya no aparece entre las libres", "5V-502" not in libres)

# --- No se puede mandar a mantenimiento con una llegada hoy
s, cuerpo, _ = admin.post("/Habitaciones/Editar/502", {"Habitacion.HabitacionId": 502, "Habitacion.NroPiso": 5, "Habitacion.NroCamas": 3, "Habitacion.TipoVipNombre": "VIP", "Habitacion.Estado": "MANTENIMIENTO"})
check("mantenimiento con reserva hoy: rechazado", "reserva para hoy" in html.unescape(cuerpo) and sql("select estado from \"Habitaciones\" where habitacion_id = 502") == "LIBRE")

# --- Check-in: pasa a ocupada
admin.post(f"/Reservas/Editar?id={rid}", {"Reserva.EstadoReserva": "EN_CURSO"})
estado, detalle = situacion(admin, 502)
check("tras el check-in figura «Ocupada» con su salida", estado == "Ocupada" and detalle.startswith("Hasta el"), (estado, detalle))
check("  sin alertas", "Todo en orden" in panel(admin))

# --- Inconsistencias forzadas en la base
sql("update \"Habitaciones\" set estado = 'LIBRE' where habitacion_id = 502")
p = panel(admin)
check("estancia en curso con la habitación libre: alerta", f"La reserva #{rid} está en curso, pero la habitación 502 figura libre" in p)
check("  y la ficha lo marca", situacion(admin, 502)[0] == "Ocupada")
sql("update \"Habitaciones\" set estado = 'OCUPADO' where habitacion_id = 502")

sql("update \"Habitaciones\" set estado = 'OCUPADO' where habitacion_id = 301")
check("ocupada sin estancia: alerta", "La habitación 301 figura ocupada, pero no tiene una estancia en curso" in panel(admin))
check("  y su ficha avisa", situacion(admin, 301) == ("Ocupada", "Sin estancia en curso: revise"), situacion(admin, 301))
sql("update \"Habitaciones\" set estado = 'LIBRE' where habitacion_id = 301")

inicio = (date.today() - timedelta(days=5)).isoformat()
r2 = sql(f"insert into \"Reservas\" (usuario_id, estado_reserva, fecha_reserva_creacion, inicio_reserva, total_noches) values ({huesped}, 'EN_CURSO', now(), date '{inicio}', 2) returning reserva_id").splitlines()[0]
sql(f"insert into \"Reserva Habitaciones\" values ({r2}, 101, 100, date '{inicio}', date '{inicio}' + 2, true)")
sql("update \"Habitaciones\" set estado = 'OCUPADO' where habitacion_id = 101")
r3 = sql(f"insert into \"Reservas\" (usuario_id, estado_reserva, fecha_reserva_creacion, inicio_reserva, total_noches) values ({huesped}, 'CONFIRMADA', now(), date '{inicio}', 1) returning reserva_id").splitlines()[0]
sql(f"insert into \"Reserva Habitaciones\" values ({r3}, 102, 50, date '{inicio}', date '{inicio}' + 1, true)")
p = panel(admin)
check("check-out vencido: alerta", f"Reserva #{r2}: el check-out venció" in p)
check("  la habitación dice «Salida pendiente»", situacion(admin, 101)[1].startswith("Salida pendiente desde"), situacion(admin, 101))
check("llegada que nunca se registró: alerta", f"Reserva #{r3}: terminó el" in p and "sin check-in" in p)
sql(f"update \"Reserva Habitaciones\" set fin = fin + 1 where reserva_id = {r3}")
check("línea con fechas distintas a su reserva: alerta", f"Reserva #{r3}: la habitación 102 no coincide" in panel(admin))
check("  cada alerta enlaza a la reserva", f'href="/Reservas/Detalles/{r2}"' in panel(admin))

# --- Páginas antiguas
s, _, url = admin.get("/Habitaciones/PorPiso?piso=4")
check("«Por piso» lleva a la vista principal filtrada", url.endswith("/Habitaciones?Piso=4"), url)
s, _, url = admin.get("/Habitaciones/PorTipoVip")
check("«Por categoría» lleva a la vista principal", url.endswith("/Habitaciones"), url)

# --- La disponibilidad sigue igual: la 502 no se ofrece hoy
s, cuerpo, _ = admin.get(f"/Reservas?handler=Disponibles&inicio={hoy}&noches=1")
check("el selector no ofrece la 502 hoy", '"habitacionId":502' not in cuerpo, cuerpo[:200])

registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
limpio = re.sub(r"^fail: Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]$", "", registro, flags=re.M)
check("sin errores inesperados", not re.search(r"^(fail|crit):", limpio, re.M), re.findall(r"^(?:fail|crit):.*\n.*", limpio, re.M)[:3])
print("\nFALLOS:", fallos if fallos else "ninguno")
