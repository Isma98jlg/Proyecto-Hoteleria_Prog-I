"""
Recorrido de integración de Hoteleria-App. Crea datos: úsalo solo contra una base vacía y
desechable, con la app iniciada con DEMO_ACCOUNTS=true. La configuración va en AGENTS.md.
"""
import html, json, os, re, sys, threading, time, urllib.error, urllib.parse, urllib.request, http.cookiejar
from datetime import date, timedelta

BASE = os.environ.get("BASE_URL", "http://127.0.0.1:5003").rstrip("/")
ADMIN = (os.environ["ADMIN_EMAIL"], os.environ["ADMIN_PASSWORD"])
WORKER = (os.environ["DEMO_WORKER_EMAIL"], os.environ["DEMO_WORKER_PASSWORD"])
GUEST = (os.environ["DEMO_USER_EMAIL"], os.environ["DEMO_USER_PASSWORD"])
fallos = []


def check(nombre, ok, detalle=""):
    print(("PASS " if ok else "FAIL ") + nombre + (f"  -> {detalle}" if detalle and not ok else ""), flush=True)
    if not ok:
        fallos.append(nombre)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


class Client:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect)

    def req(self, path, fields=None, raw=None):
        data = urllib.parse.urlencode(fields, doseq=True).encode() if fields is not None else raw
        try:
            r = self.op.open(urllib.request.Request(BASE + path, data=data), timeout=90)
            return r.status, r.read().decode("utf-8", "replace"), r.headers.get("Location", "")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8", "replace"), e.headers.get("Location", "")

    def get(self, path):
        return self.req(path)

    def post(self, form_path, post_path, fields):
        _, body, _ = self.get(form_path)
        fields = dict(fields)
        fields["__RequestVerificationToken"] = token(body)
        return self.req(post_path, fields)


def token(body):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body) or re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', body)
    if not m:
        raise RuntimeError("sin token: " + body[:300])
    return html.unescape(m.group(1))


def login(email, pwd):
    c = Client()
    s, _, loc = c.post("/Account/Login", "/Account/Login", {"Input.Email": email, "Input.Password": pwd, "Input.RememberMe": "false"})
    return c, s, loc


def persona(doc, email, nombre="Ana"):
    return {"Persona.TipoDocumento": "DNI", "Persona.NumeroDocumento": doc, "Persona.Nombres": nombre, "Persona.Apellidos": "Prueba",
            "Persona.Nacionalidad": "PE", "Persona.Telefono": "999", "Persona.EMail": email}


def error_de(body):
    m = re.search(r'validation-summary-errors[^>]*>(.*?)</div>', body, re.S)
    return re.sub(r"<[^>]+>", " ", m.group(1)).strip() if m else ""


def crear_reserva(c, usuario, inicio, habitacion, noches=1):
    return c.post("/Reservas", "/Reservas?handler=Crear", {"Reserva.UsuarioId": usuario, "Reserva.InicioReserva": inicio, "Reserva.TotalNoches": noches, "Reserva.HabitacionIds": habitacion})


def estado_reserva(c, rid, estado):
    p = f"/Reservas/Editar?id={rid}"
    return c.post(p, p, {"Reserva.EstadoReserva": estado})


def ultima_reserva(c, usuario):
    return max(int(x) for x in re.findall(r"/Reservas/Detalles\?id=(\d+)", c.get(f"/Reservas?UsuarioId={usuario}")[1]))


def id_huesped(c, doc):
    return re.search(r"/Usuarios/Detalles/(\d+)", c.get(f"/Usuarios?buscar={doc}")[1]).group(1)


def estado_hab(c, h):
    m = re.search(r"Estado</span><strong[^>]*>([A-Z]+)", c.get(f"/Habitaciones/Detalles/{h}")[1])
    return m.group(1) if m else "?"


def enlace(body):
    m = re.search(r'readonly value="(http[^"]+)"', body)
    return html.unescape(m.group(1)) if m else None


d = lambda n: (date.today() + timedelta(days=n)).isoformat()
hoy = date.today().isoformat()

for _ in range(90):
    try:
        if Client().get("/health")[0] == 200:
            break
    except Exception:
        pass
    time.sleep(2)

# --- Salud, API pública y acceso anónimo
check("health responde 200 sin sesión", Client().get("/health")[0] == 200)
check("OpenAPI publicado", '"openapi"' in Client().get("/openapi/v1.json")[1])
check("anónimo redirigido al login", "Login" in Client().get("/Reservas")[2])
check("API exige sesión", Client().get("/api/v1/habitaciones")[0] in (302, 401))

# --- Login
admin, s, loc = login(*ADMIN)
check("admin creado por el sistema entra al panel", s == 302 and loc == "/", f"{s} {loc}")
worker, s, loc = login(*WORKER)
check("trabajador aterriza en reservas", s == 302 and loc.startswith("/Reservas"), f"{s} {loc}")
guest, s, loc = login(*GUEST)
check("huésped aterriza en su portal", s == 302 and loc.startswith("/Portal"), f"{s} {loc}")
check("contraseña incorrecta rechazada", login(ADMIN[0], "incorrecta")[1] == 200)

# --- T1 huésped creado por recepción se puede reservar
doc = "AUD" + str(int(time.time()) % 10000000)
s, _, _ = worker.post("/Usuarios/Crear", "/Usuarios/Crear", persona(doc, f"{doc}@grandstay.com"))
check("recepción registra huésped", s == 302, str(s))
gid = id_huesped(worker, doc)
check("aparece en el selector", f'<option value="{gid}"' in worker.get("/Reservas")[1])
check("se le puede crear reserva", crear_reserva(worker, gid, d(30), 101)[0] == 302)

# --- T2 correo largo
s, _, _ = worker.post("/Usuarios/Crear", "/Usuarios/Crear", persona(doc + "L", "reservas.familia.perez.garcia@grandstay.com"))
check("correo de 39 caracteres aceptado", s == 302, str(s))

# --- Dominio único y correo de contacto
s, body, _ = worker.post("/Usuarios/Crear", "/Usuarios/Crear", persona(doc + "G", "ana@gmail.com"))
check("correo de acceso de otro dominio rechazado", s == 200 and "@grandstay.com" in body, str(s))
s, _, _ = worker.post("/Usuarios/Crear", "/Usuarios/Crear", {**persona(doc + "C", f"contacto{doc}@grandstay.com".lower()), "Persona.CorreoContacto": "ana.real@gmail.com"})
check("correo de contacto de cualquier proveedor aceptado", s == 302, str(s))
check("ficha muestra el correo de contacto", "ana.real@gmail.com" in worker.get(f"/Usuarios/Detalles/{id_huesped(worker, doc + 'C')}")[1])
check("login con otro dominio rechazado", login("alguien@gmail.com", "Cualquiera-12!")[1] == 200)
_, f, _ = admin.get("/Empleados")
cargo_trab = re.search(r'<option value="(\d+)">trabajador</option>', f).group(1)
s, _, _ = admin.req("/Empleados?handler=Invitar", {"Email": "ana@gmail.com", "CargoId": cargo_trab, "__RequestVerificationToken": token(f)})
check("invitación con otro dominio rechazada", s == 200, str(s))

# --- T3 reactivar cancelada / T11 retroceder estados
inicio = d(60)
crear_reserva(admin, gid, inicio, 102)
ra = ultima_reserva(admin, gid)
estado_reserva(admin, ra, "CANCELADA")
s, _, _ = crear_reserva(admin, gid, inicio, 102)
check("cancelar libera la habitación", s == 302, str(s))
s, body, _ = estado_reserva(admin, ra, "CONFIRMADA")
check("no se reactiva una cancelada", s == 200 and "no puede pasar" in body, str(s))
rb = ultima_reserva(admin, gid)
estado_reserva(admin, rb, "CONFIRMADA")
s, body, _ = estado_reserva(admin, rb, "PENDIENTE")
check("no se retrocede de confirmada a pendiente", s == 200, str(s))
_, ib, _ = admin.get("/Reservas")
admin.req(f"/Reservas?handler=Eliminar&id={rb}", {"__RequestVerificationToken": token(ib)})
check("confirmada no se puede borrar", admin.get(f"/Reservas/Detalles?id={rb}")[0] == 200)
s, _, _ = crear_reserva(admin, gid, inicio, 102)
check("doble reserva directa rechazada", s == 200, str(s))

# --- Editar fechas y habitaciones
s, _, _ = admin.post(f"/Reservas/Editar?id={rb}", f"/Reservas/Editar?id={rb}", {"Reserva.EstadoReserva": "CONFIRMADA", "Reserva.InicioReserva": d(61), "Reserva.TotalNoches": 2, "Reserva.HabitacionIds": [102, 103]})
det = admin.get(f"/Reservas/Detalles?id={rb}")[1]
check("editar fechas y habitaciones", s == 302 and "2" in det and "103" in det, str(s))

# --- T4 privacidad
check("huésped no ve directorio", "AccessDenied" in guest.get("/Usuarios")[2])
check("huésped no ve ficha ajena", "AccessDenied" in guest.get(f"/Usuarios/Detalles/{gid}")[2])
check("huésped no ve reservas ajenas", "AccessDenied" in guest.get(f"/Reservas/Detalles?id={rb}")[2])
for ruta in ["/Habitaciones", "/Habitaciones/Detalles/102", "/Habitaciones/PorPiso", "/Habitaciones/PorTipoVip", "/api/v1/habitaciones"]:
    check(f"huésped no entra a {ruta}", "AccessDenied" in guest.get(ruta)[2])
check("navegación del huésped sin Habitaciones", 'href="/Habitaciones' not in guest.get("/Portal")[1])
check("huésped no entra al panel", "AccessDenied" in guest.get("/")[2])
for ruta in ["/Empleados", "/Cargos", "/TiposVip"]:
    check(f"trabajador no entra a {ruta}", "AccessDenied" in worker.get(ruta)[2])
check("trabajador no ve ficha de admin", worker.get("/Usuarios/Detalles/1")[0] == 404)

# --- T5
check("filtro inválido no da 500", admin.get("/Reservas?UsuarioId=abc")[0] == 200)

# --- T6 mantenimiento
p = "/Habitaciones/Editar/201"
s, _, _ = worker.post(p, p, {"Habitacion.NroPiso": 2, "Habitacion.NroCamas": 2, "Habitacion.TipoVipNombre": "ESTANDAR", "Habitacion.Estado": "MANTENIMIENTO"})
check("marcar en mantenimiento", s == 302, str(s))
fut = worker.get(f"/Reservas?handler=Disponibles&inicio={d(100)}&noches=1")[1]
ahora = worker.get(f"/Reservas?handler=Disponibles&inicio={hoy}&noches=1")[1]
check("mantenimiento no bloquea fechas futuras", '"habitacionId":201' in fut)
check("mantenimiento bloquea hoy", '"habitacionId":201' not in ahora)
s, _, _ = worker.post(p, p, {"Habitacion.NroPiso": 2, "Habitacion.NroCamas": 2, "Habitacion.TipoVipNombre": "ESTANDAR", "Habitacion.Estado": "OCUPADO"})
check("no se marca ocupada a mano", s == 200, str(s))
worker.post(p, p, {"Habitacion.NroPiso": 2, "Habitacion.NroCamas": 2, "Habitacion.TipoVipNombre": "ESTANDAR", "Habitacion.Estado": "LIBRE"})

# --- T7 check-in / check-out
s, _, _ = crear_reserva(worker, gid, hoy, 202, noches=2)
rc = ultima_reserva(worker, gid)
check("reserva de hoy", s == 302, str(s))
check("confirmar no ocupa la habitación", estado_hab(worker, 202) == "LIBRE")
check("check-in", estado_reserva(worker, rc, "EN_CURSO")[0] == 302)
check("check-in ocupa la habitación", estado_hab(worker, 202) == "OCUPADO")
check("ocupada no se cambia a mano", worker.post("/Habitaciones/Editar/202", "/Habitaciones/Editar/202", {"Habitacion.NroPiso": 2, "Habitacion.NroCamas": 2, "Habitacion.TipoVipNombre": "VIP", "Habitacion.Estado": "LIBRE"})[0] == 200)
check("check-out", estado_reserva(worker, rc, "FINALIZADA")[0] == 302)
check("check-out libera la habitación", estado_hab(worker, 202) == "LIBRE")
s, _, _ = crear_reserva(worker, gid, d(40), 203)
rf = ultima_reserva(worker, gid)
s, body, _ = estado_reserva(worker, rf, "CONFIRMADA")
s, body, _ = estado_reserva(worker, rf, "EN_CURSO")
check("check-in fuera de fecha rechazado", s == 200 and "dentro de las fechas" in body, str(s))

# --- T9 concurrencia
_, f, _ = admin.get("/Reservas")
tok = token(f)
exitos_max = 0
for i in range(12):
    res = []
    campos = {"Reserva.UsuarioId": gid, "Reserva.InicioReserva": d(150 + i * 3), "Reserva.TotalNoches": 1, "Reserva.HabitacionIds": 301, "__RequestVerificationToken": tok}
    ts = [threading.Thread(target=lambda: res.append(admin.req("/Reservas?handler=Crear", campos)[0])) for _ in range(8)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    exitos_max = max(exitos_max, res.count(302))
    if 500 in res:
        fallos.append("concurrencia devolvió 500")
print("     concurrencia: máximo de reservas exitosas por ronda =", exitos_max, flush=True)
check("concurrencia sin doble reserva", exitos_max == 1, str(exitos_max))

# --- Registro público y portal
nuevo = f"reg{doc}@grandstay.com".lower()
anon = Client()
s, body, _ = anon.post("/Account/Registro", "/Account/Registro", {**persona("R" + doc, nuevo), "Password": "corta", "Confirmacion": "corta"})
check("registro rechaza contraseña débil", s == 200)
anon = Client()
s, _, loc = anon.post("/Account/Registro", "/Account/Registro", {**persona("R" + doc, nuevo), "Password": "Registro-Prueba-1!", "Confirmacion": "Registro-Prueba-1!", "Cargo": "admin", "Persona.CargoId": 1})
check("registro crea cuenta e inicia sesión", s == 302 and "Portal" in loc, f"{s} {loc}")
check("registrado queda como huésped", "AccessDenied" in anon.get("/Reservas")[2])
s, body, _ = anon.get(f"/Portal/Reservar?handler=Disponibles&q=vip&inicio={d(70)}&noches=2")
tipos = admin.get("/TiposVip")[1]
check("los tipos iniciales son ESTANDAR y VIP", "ESTANDAR" in tipos and "VIP" in tipos and not any(x in tipos for x in ("SUPERIOR", "SUITE", "PRESIDENCIAL")))
datos = json.loads(body) if s == 200 else {"habitaciones": []}
coinciden = [h for h in datos["habitaciones"] if h["coincide"]]
check("portal busca habitaciones", bool(coinciden) and all(h["categoria"] == "VIP" for h in coinciden), body[:200])
check("cada habitación indica su piso", all(h["ubicacion"].startswith("Piso ") for h in datos["habitaciones"]), body[:200])
filtradas = json.loads(anon.get(f"/Portal/Reservar?handler=Disponibles&personas=2&inicio={d(70)}&noches=2")[1])["habitaciones"]
check("personas filtra por número exacto de camas", any(h["coincide"] for h in filtradas) and all(h["camas"] == 2 for h in filtradas if h["coincide"]))
s, _, _ = anon.post(f"/Portal/Reservar?Inicio={d(70)}&Noches=2", f"/Portal/Reservar", {"Inicio": d(70), "Noches": 2, "HabitacionIds": 303})
check("huésped reserva desde el portal", s == 302, str(s))
portal = anon.get("/Portal")[1]
mia = re.search(r"handler=Cancelar&amp;id=(\d+)|id=(\d+)&amp;handler=Cancelar|Cancelar\?id=(\d+)", portal)
mid = next(g for g in mia.groups() if g) if mia else None
check("reserva visible en el portal y ya confirmada", mid is not None and "Confirmada" in portal and "Pendiente" not in portal)
s, detalle, _ = anon.get(f"/Portal/Detalle/{mid}")
check("detalle propio muestra la habitación elegida", s == 200 and "303" in detalle and "VIP" in detalle and "Segundo Piso" in detalle, str(s))
check("detalle ajeno no disponible", guest.get(f"/Portal/Detalle/{mid}")[0] == 404)
_, f, _ = worker.get("/Reservas")
guest.req(f"/Portal?handler=Cancelar&id={mid}", {"__RequestVerificationToken": token(guest.get("/Account/CambiarContrasena")[1])})
check("otro huésped no cancela reservas ajenas", "Confirmada" in anon.get("/Portal")[1])
_, f, _ = anon.get("/Portal")
anon.req(f"/Portal?handler=Cancelar&id={mid}", {"__RequestVerificationToken": token(f)})
check("huésped cancela su reserva", "Cancelada" in anon.get("/Portal")[1])

# --- Invitación de personal
_, f, _ = admin.get("/Empleados")
cargo_id = re.search(r'<option value="(\d+)">trabajador</option>', f).group(1)
inv = f"staff{doc}@grandstay.com".lower()
s, _, _ = admin.req("/Empleados?handler=Invitar", {"Email": inv, "CargoId": cargo_id, "__RequestVerificationToken": token(f)})
url = enlace(admin.get("/Empleados")[1])
check("admin genera invitación", s == 302 and url is not None, str(s))
ruta_inv = urllib.parse.urlsplit(url).path + "?" + urllib.parse.urlsplit(url).query
anon2 = Client()
s, body, _ = anon2.post(ruta_inv, ruta_inv, {**persona("S" + doc, "otro@gmail.com", "Staff"), "Password": "Invitado-Prueba-1!", "Confirmacion": "Invitado-Prueba-1!"})
check("invitado completa registro", s == 302, f"{s} {error_de(body)}")
c, s, loc = login(inv, "Invitado-Prueba-1!")
check("invitado entra con el correo de la invitación y cargo trabajador", s == 302 and loc.startswith("/Reservas"), f"{s} {loc}")
check("la invitación es de un solo uso", "no es válido" in Client().get(ruta_inv)[1])
check("enlace falso rechazado", "no es válido" in Client().get("/Account/Invitacion?token=falso")[1])

# --- Activación de huésped de mostrador y restablecer contraseña
_, f, _ = worker.get(f"/Usuarios/Detalles/{gid}")
worker.req(f"/Usuarios/Detalles/{gid}?handler=Activacion", {"__RequestVerificationToken": token(f)})
url = enlace(worker.get(f"/Usuarios/Detalles/{gid}")[1])
check("recepción genera activación", url is not None)
ruta_act = urllib.parse.urlsplit(url).path + "?" + urllib.parse.urlsplit(url).query
s, body, _ = Client().post(ruta_act, ruta_act, {"Password": "Activado-Prueba-1!", "Confirmacion": "Activado-Prueba-1!"})
check("huésped activa su cuenta", s == 302, f"{s} {error_de(body)}")
c, s, loc = login(f"{doc}@grandstay.com", "Activado-Prueba-1!")
check("huésped activado entra a su portal", s == 302 and "Portal" in loc, f"{s} {loc}")
_, f, _ = worker.get(f"/Usuarios/Detalles/{gid}")
worker.req(f"/Usuarios/Detalles/{gid}?handler=Restablecer", {"__RequestVerificationToken": token(f)})
url = enlace(worker.get(f"/Usuarios/Detalles/{gid}")[1])
ruta_res = urllib.parse.urlsplit(url).path + "?" + urllib.parse.urlsplit(url).query
s, _, _ = Client().post(ruta_res, ruta_res, {"Password": "Restablecida-1!", "Confirmacion": "Restablecida-1!"})
check("restablecer contraseña", s == 302 and login(f"{doc}@grandstay.com", "Restablecida-1!")[1] == 302, str(s))

# --- T8 bloqueo
for _ in range(5):
    login(nuevo, "incorrecta")
_, s, _ = login(nuevo, "Registro-Prueba-1!")
check("cuenta bloqueada tras 5 fallos", s == 200, str(s))

# --- CRUD admin
p = "/TiposVip"
s, _, _ = admin.post(p, p + "?handler=Crear", {"nombre": "econo", "sigla": "x", "precioCama": "25.50"})
check("crear tipo", s == 302 and "ECONO" in admin.get(p)[1], str(s))
s, _, _ = admin.post(p, p + "?handler=Guardar&nombre=ECONO", {"precioCama": "30"})
check("editar precio", s == 302 and "value=\"30" in admin.get(p)[1], str(s))
s, _, _ = admin.post("/Habitaciones/Crear", "/Habitaciones/Crear", {"Habitacion.HabitacionId": 999, "Habitacion.NroPiso": 1, "Habitacion.NroCamas": 1, "Habitacion.TipoVipNombre": "ECONO", "Habitacion.Estado": "LIBRE"})
check("crear habitación con número propio", s == 302 and admin.get("/Habitaciones/Detalles/999")[0] == 200, str(s))
s, _, _ = admin.post("/Habitaciones/Crear", "/Habitaciones/Crear", {"Habitacion.HabitacionId": 999, "Habitacion.NroPiso": 1, "Habitacion.NroCamas": 1, "Habitacion.TipoVipNombre": "ECONO", "Habitacion.Estado": "LIBRE"})
check("número repetido rechazado", s == 200, str(s))
s, _, _ = admin.post(p, p + "?handler=Eliminar&nombre=ECONO", {})
check("tipo con habitaciones no se borra", "ECONO" in admin.get(p)[1])
s, _, _ = admin.post("/Habitaciones/Editar/999", "/Habitaciones/Editar/999?handler=Eliminar", {})
check("eliminar habitación", s == 302 and admin.get("/Habitaciones/Detalles/999")[0] == 404, str(s))
admin.post(p, p + "?handler=Eliminar&nombre=ECONO", {})
check("eliminar tipo", "ECONO" not in admin.get(p)[1])
s, _, _ = admin.post("/Habitaciones", "/Habitaciones?handler=GuardarPiso", {"PisoNro": 77, "PisoRef": "Azotea"})
check("crear piso", "Azotea" in admin.get("/Habitaciones")[1])
s, _, _ = admin.post("/Habitaciones", "/Habitaciones?handler=EliminarPiso&nroPiso=77", {})
check("eliminar piso", s == 302 and "Azotea" not in admin.get("/Habitaciones")[1], str(s))
s, _, _ = admin.post("/Habitaciones", "/Habitaciones?handler=EliminarPiso&nroPiso=1", {})
check("piso con habitaciones no se borra", "Planta Baja" in admin.get("/Habitaciones")[1])

p = "/Cargos"
s, _, _ = admin.post(p, p + "?handler=Crear", {"nombre": "Supervisor"})
body = admin.get(p)[1]
check("crear cargo", s == 302 and "supervisor" in body, str(s))
m = re.search(r'handler=Eliminar&amp;id=(\d+)[^"]*"[^>]*>\s*<button', body) or re.search(r'id=(\d+)&amp;handler=Eliminar', body)
sup = m.group(1) if m else None
s, _, _ = admin.post(p, p + f"?handler=Guardar&id={sup}", {"ver": "true", "crudRequerimientos": "true"})
check("editar permisos de cargo", s == 302, str(s))
s, _, _ = admin.post(p, p + f"?handler=Eliminar&id={sup}", {})
check("eliminar cargo", "supervisor" not in admin.get(p)[1])

emp = re.findall(r"/Empleados/Editar/(\d+)", admin.get("/Empleados")[1])
pw = next(i for i in emp if WORKER[0] in admin.get(f"/Empleados/Editar/{i}")[1])
s, body2, _ = admin.post(f"/Empleados/Editar/{pw}", f"/Empleados/Editar/{pw}", {**persona("EMP1", WORKER[0], "Trabajador"), "CargoId": cargo_id, "FechaContratacion": hoy, "Sueldo": "1500.50"})
lista = admin.get("/Empleados")[1]
check("editar empleado", s == 302 and ("1,500.50" in lista or "1.500,50" in lista), f"{s} {error_de(body2)}")

# --- API autenticada
s, body, _ = worker.get("/api/v1/habitaciones?q=vip")
j = json.loads(body)
check("API de búsqueda", s == 200 and j["motor"] == "postgresql" and all(r["categoria"] == "VIP" for r in j["resultados"]) and "usuario" not in body.lower(), body[:200])
s, body, _ = worker.get(f"/api/v1/disponibilidad?inicio={d(90)}&noches=2")
check("API de disponibilidad", s == 200 and "habitacionId" in body)
check("API valida noches", worker.get(f"/api/v1/disponibilidad?inicio={d(90)}&noches=0")[0] == 400)

# --- Ventanas flotantes
s, body, _ = admin.get("/Habitaciones/Crear?modal=1")
check("ventana flotante sin menú de la app", s == 200 and "Módulos HMS" not in body and "ventana-flotante-interna" in body, str(s))
check("logo sin menú desplegable", "user-profile-dropdown" not in admin.get("/Reservas")[1])
s, body, _ = admin.post("/Habitaciones?modal=1", "/Habitaciones?handler=GuardarPiso&modal=1", {"PisoNro": 78, "PisoRef": "Prueba"})
check("guardar en ventana avisa a la página principal", s == 200 and "'listo'" in body and "/Habitaciones" in body, f"{s} {body[:120]}")
admin.post("/Habitaciones", "/Habitaciones?handler=EliminarPiso&nroPiso=78", {})

# --- SignalR con long polling
s, body, _ = Client().req("/hubs/hotel/negotiate?negotiateVersion=1", raw=b"")
check("hub exige sesión", s in (302, 401), str(s))
s, body, _ = worker.req("/hubs/hotel/negotiate?negotiateVersion=1", raw=b"")
check("hub negocia con sesión", s == 200, str(s))
tk = json.loads(body)["connectionToken"]
worker.req(f"/hubs/hotel?id={tk}", raw=b'{"protocol":"json","version":1}\x1e')
recibido = []


def sondear():
    fin = time.time() + 40
    while time.time() < fin and "reservaActualizada" not in "".join(recibido):
        recibido.append(worker.get(f"/hubs/hotel?id={tk}")[1])


hilo = threading.Thread(target=sondear)
hilo.start()
time.sleep(3)
crear_reserva(admin, gid, d(300), 401)
hilo.join(45)
txt = "".join(recibido)
check("SignalR entrega el aviso de reserva en vivo", "reservaActualizada" in txt, txt[:200])
check("el aviso no lleva datos personales", "Prueba" not in txt and "@" not in txt)

# --- Asistente: el permiso se comprueba sin llamar al modelo.
check("huésped abre su asistente", guest.get("/Portal/Asistente")[0] == 200)
check("personal no usa el asistente del huésped", "AccessDenied" in admin.get("/Portal/Asistente")[2])

# --- Clima: permisos y validación, sin llamar al servicio externo.
check("personal no consulta el clima del huésped", "AccessDenied" in admin.get("/Portal/Clima?lat=1&lon=1")[2])
check("coordenadas fuera de rango rechazadas", guest.get("/Portal/Clima?lat=500&lon=1")[0] == 400)
check("clima sin datos rechazado", guest.get("/Portal/Clima")[0] == 400)

# --- Explora tu zona: permisos y validación, sin llamar al servicio externo.
check("huésped abre Explora tu zona", guest.get("/Portal/Explora")[0] == 200)
check("personal no usa Explora tu zona", "AccessDenied" in admin.get("/Portal/Explora")[2])
check("radio no permitido rechazado", guest.get("/Portal/Explora?handler=Buscar&lat=1&lon=1&radio=7")[0] == 400)
check("filtro desconocido rechazado", guest.get("/Portal/Explora?handler=Buscar&lat=1&lon=1&filtros=casinos")[0] == 400)
check("coordenadas de lugares fuera de rango rechazadas", guest.get("/Portal/Explora?handler=Buscar&lat=500&lon=1")[0] == 400)

# --- Tarifas de la competencia: permisos, sin llamar al servicio externo.
check("administrador abre la competencia", admin.get("/Competencia")[0] == 200)
check("personal no ve la competencia", "AccessDenied" in worker.get("/Competencia")[2])
check("huésped no ve la competencia", "AccessDenied" in guest.get("/Competencia")[2])

# Vistas restantes
for ruta in ["/Habitaciones/PorPiso", "/Habitaciones/PorTipoVip", "/Privacy", "/Reservas/Crear", "/Portal/Perfil", "/Account/CambiarContrasena"]:
    s = admin.get(ruta)[0]
    check(f"carga {ruta}", s == 200, str(s))
check("exportar CSV", "Codigo,Piso" in admin.get("/Habitaciones?handler=Export")[1])
s, body, _ = guest.post("/Account/CambiarContrasena", "/Account/CambiarContrasena", {"Actual": GUEST[1], "Nueva": "Guest-Nueva-12!", "Confirmacion": "Guest-Nueva-12!"})
check("cambiar contraseña", s == 302 and login(GUEST[0], "Guest-Nueva-12!")[1] == 302, f"{s}")
s, body, _ = guest.post("/Portal/Perfil", "/Portal/Perfil", persona("GUEST1", GUEST[0], "Huesped"))
check("editar mis datos", s == 302, f"{s} {error_de(body)}")

print(f"\nFALLOS: {len(fallos)}", fallos)
sys.exit(1 if fallos else 0)
