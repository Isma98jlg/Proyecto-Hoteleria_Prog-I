"""Eliminar huéspedes: el admin borra al huésped con sus reservas y su cuenta; el resto de reglas se mantiene."""
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
            with self.op.open(BASE + ruta, timeout=30) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def post(self, ruta, datos):
        try:
            with self.op.open(urllib.request.Request(BASE + ruta, urllib.parse.urlencode(datos).encode()), timeout=60) as r:
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
                          capture_output=True, text=True).stdout.strip()


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])
trabajador = Cliente()
trabajador.entrar("trabajador@grandstay.com", env["TR"])


def alta(numero, nombre):
    s, _, url = admin.post("/Usuarios/Crear", {"Persona.TipoDocumento": "DNI", "Persona.NumeroDocumento": numero, "Persona.Nombres": nombre, "Persona.Apellidos": "Prueba",
                                               "Persona.Nacionalidad": "Peruana", "Persona.Telefono": "999888777", "Persona.EMail": f"{nombre.lower()}.{numero}@grandstay.com",
                                               "__RequestVerificationToken": admin.token("/Usuarios/Crear")})
    return sql(f"select usuario_id from \"Usuarios\" where numero_documento = '{numero}'")


def reservar(uid, dias, estado="CONFIRMADA"):
    """Crea la reserva directamente en la base para no depender del formulario: una habitación libre en esas fechas."""
    inicio = date.today() + timedelta(days=dias)
    hab = sql(f"""select h.habitacion_id from "Habitaciones" h where not exists (select 1 from "Reserva Habitaciones" rh where rh.habitacion_id = h.habitacion_id and rh.activa and rh.inicio < date '{inicio}' + 2 and rh.fin > date '{inicio}') order by 1 limit 1""")
    rid = sql(f"""insert into "Reservas" (usuario_id, estado_reserva, fecha_reserva_creacion, inicio_reserva, total_noches) values ({uid}, '{estado}', now(), date '{inicio}', 2) returning reserva_id""").splitlines()[0]
    sql(f"""insert into "Reserva Habitaciones" (reserva_id, habitacion_id, precio_noche_pactado, inicio, fin, activa) values ({rid}, {hab}, 100, date '{inicio}', date '{inicio}' + 2, true)""")
    return rid, hab


def eliminar(cliente, uid):
    return cliente.post(f"/Usuarios/Editar/{uid}?handler=Eliminar", {"__RequestVerificationToken": cliente.token("/Usuarios/Crear")})


# --- Huésped con reservas: solo el admin, y se borran con él
uid = alta("70000001", "Rosa")
r1, h1 = reservar(uid, 10)
r2, _ = reservar(uid, 20)
r3, _ = reservar(uid, 30, "CANCELADA")
pagina = admin.get(f"/Usuarios/Editar/{uid}")[1]
check("el admin ve «Eliminar huésped» aunque tenga reservas", "Eliminar huésped" in pagina and "3 reservas" in html.unescape(pagina))
check("  el aviso dice que se borran sus reservas", "También se borrarán sus 3 reservas" in html.unescape(pagina))

pagina_t = trabajador.get(f"/Usuarios/Editar/{uid}")
if pagina_t[0] == 200:
    check("el personal no admin no ve el botón con reservas", "Eliminar huésped" not in pagina_t[1])
    s, _, url = eliminar(trabajador, uid)
    check("  y si lo fuerza, el servidor lo rechaza", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid}") == "1", (s, url))
else:
    check("el trabajador demo no administra huéspedes (sin crud_usuarios)", pagina_t[0] in (403, 404) or "AccessDenied" in pagina_t[2], pagina_t[0])

s, cuerpo, url = eliminar(admin, uid)
check("el admin elimina al huésped con reservas", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid}") == "0", (s, url))
check("  se borran sus 3 reservas", sql(f"select count(*) from \"Reservas\" where reserva_id in ({r1},{r2},{r3})") == "0")
check("  y sus líneas de habitación", sql(f"select count(*) from \"Reserva Habitaciones\" where reserva_id in ({r1},{r2},{r3})") == "0")
check("  la habitación queda libre en esas fechas", sql(f"select count(*) from \"Reserva Habitaciones\" where habitacion_id = {h1} and inicio = current_date + 10") == "0")
check("  vuelve al directorio con el aviso", "/Usuarios" in url and "Editar" not in url, url)

# --- Huésped alojado: primero el check-out
uid2 = alta("70000002", "Pedro")
reservar(uid2, 15)
rid_curso, hab_curso = reservar(uid2, 0, "EN_CURSO")
sql(f"update \"Habitaciones\" set estado = 'OCUPADO' where habitacion_id = {hab_curso}")
s, cuerpo, url = eliminar(admin, uid2)
check("huésped alojado: no se elimina", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid2}") == "1", (s, url))
check("  ni se toca ninguna de sus reservas", sql(f"select count(*) from \"Reservas\" where usuario_id = {uid2}") == "2")
detalle = html.unescape(cuerpo)
check("  el aviso pide el check-out", "check-out" in detalle, re.findall(r"check.out[^<]{0,80}", detalle)[:2])

# --- Huésped con cuenta de acceso (el demo): se borra también su cuenta
uid3 = sql("select \"Id\" from \"AspNetUsers\" where \"Email\" = 'huesped@grandstay.com'")
huesped = Cliente()
huesped.entrar("huesped@grandstay.com", env["HU"])
check("el huésped demo entra antes de borrarlo", "/Portal" in huesped.get("/Portal")[2])
eliminar(admin, uid3)
check("huésped con cuenta: se borra la persona", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid3}") == "0")
check("  y su cuenta y roles de acceso", sql(f"select count(*) from \"AspNetUsers\" where \"Id\" = {uid3}") == "0" and sql(f"select count(*) from \"AspNetUserRoles\" where \"UserId\" = {uid3}") == "0")
nuevo = Cliente()
nuevo.entrar("huesped@grandstay.com", env["HU"])
check("  ya no puede iniciar sesión", "/Portal" not in nuevo.get("/Portal")[2])

# --- Sin reservas ni cuenta: como antes
uid4 = alta("70000004", "Luz")
eliminar(admin, uid4)
check("huésped sin reservas: se elimina", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid4}") == "0")

# --- No sirve para borrar personal
uid_trab = sql("select \"Id\" from \"AspNetUsers\" where \"Email\" = 'trabajador@grandstay.com'")
s, _, _ = eliminar(admin, uid_trab)
check("no elimina personal por esta vía", sql(f"select count(*) from \"Usuarios\" where usuario_id = {uid_trab}") == "1", s)

registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
limpio = re.sub(r"^fail: Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]$", "", registro, flags=re.M)
check("sin errores inesperados", not re.search(r"^(fail|crit):", limpio, re.M), re.findall(r"^(?:fail|crit):.*", limpio, re.M)[:3])
print("\nFALLOS:", fallos if fallos else "ninguno")
