"""Recorrido del asistente contra la API falsa: flujo, ataques, límites y número de llamadas."""
import http.cookiejar
import json
import os
import re
import subprocess
import urllib.parse
import urllib.request

BASE = "http://localhost:5077"
API = "http://127.0.0.1:18999"
fallos = []


def check(nombre, ok, extra=""):
    print(("OK   " if ok else "FALLA"), nombre, "" if ok else str(extra)[:300])
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
        cuerpo = urllib.parse.urlencode(datos).encode()
        try:
            with self.op.open(urllib.request.Request(BASE + ruta, cuerpo), timeout=60) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def token(self, ruta):
        html = self.get(ruta)[1]
        return re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)

    def entrar(self, correo, clave):
        t = self.token("/Account/Login")
        html = self.get("/Account/Login")[1]
        nombres = re.findall(r'name="([^"]*(?:mail|ass)[^"]*)"', html, re.I)
        return self.post("/Account/Login", {nombres[0]: correo, nombres[1]: clave, "__RequestVerificationToken": t})

    def chat(self, accion, mensaje=None):
        datos = {"__RequestVerificationToken": self.token("/Portal/Asistente")}
        if mensaje is not None:
            datos["mensaje"] = mensaje
        s, cuerpo, _ = self.post(f"/Portal/Asistente?handler={accion}", datos)
        return s, json.loads(cuerpo) if s == 200 else cuerpo


def api(metodo="GET"):
    with urllib.request.urlopen(urllib.request.Request(API + "/x", method=metodo), timeout=5) as r:
        return json.loads(r.read())


def redis(*orden):
    return subprocess.run(["docker", "exec", "hoteleria-prueba-redis", "redis-cli", *orden], capture_output=True, text=True).stdout.strip()


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
h = Cliente()
h.entrar("huesped@grandstay.com", env["HU"])
check("el huésped abre el asistente", h.get("/Portal/Asistente")[0] == 200)
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])
check("el administrador no entra al asistente", "AccessDenied" in admin.get("/Portal/Asistente")[2])
anonimo = Cliente()
check("sin sesión no se entra", "Login" in anonimo.get("/Portal/Asistente")[2])

# Datos incompletos: una llamada, sin propuesta.
api("DELETE")
s, d = h.chat("Enviar", "sin datos todavía")
check("pregunta lo que falta", s == 200 and d["mensajes"][-1]["texto"].startswith("¿Qué"), d)
check("  sin propuesta", d["propuesta"] is None)
check("  una sola llamada al modelo", api()["llamadas"] == 1, api()["llamadas"])

# Mensaje completo: buscar + preparar, sin una tercera llamada.
api("DELETE")
s, d = h.chat("Enviar", "Somos dos personas, del 10 al 12 de marzo de 2030")
info = api()
check("mensaje completo: crea propuesta", d.get("propuesta") is not None, d)
check("  exactamente dos llamadas (no hay una tercera)", info["llamadas"] == 2, info["llamadas"])
check("  usa Opus 5.5 con esfuerzo bajo", info["detalle"][0]["modelo"] == "claude-opus-5-5" and info["detalle"][0]["esfuerzo"] == "low", info["detalle"][0])
print("     instrucciones:", info["detalle"][0]["tamano_sistema"], "caracteres; petición completa:", info["detalle"][0]["tamano_total"])
pendiente = d["propuesta"]
check("  el total sale del sistema", pendiente["total"] not in ("", "0"), pendiente)

# La búsqueda queda disponible para el siguiente mensaje.
api("DELETE")
s, d = h.chat("Enviar", "sin datos, una duda")
sistema = json.dumps(api()["detalle"][0]["sistema"], ensure_ascii=False)
check("el siguiente mensaje recuerda la búsqueda sin repetirla", "Última búsqueda" in sistema and "id|codigo" in sistema, sistema[:200])
check("  y recuerda la propuesta pendiente", "Propuesta pendiente" in sistema)
check("  la propuesta sigue en pantalla", d["propuesta"] is not None)

# El modelo no crea reservas: nada aparece hasta pulsar el botón.
check("sin confirmar no hay reserva", "Todavía no tiene reservas" in h.get("/Portal")[1])
api("DELETE")
s, d = h.chat("Confirmar")
check("confirmar crea la reserva", d.get("reservaCreada") is True, d)
check("  confirmar no gasta tokens", api()["llamadas"] == 0, api()["llamadas"])
html = h.get("/Portal")[1]
check("  aparece en Mis reservas, confirmada", "Todavía no tiene reservas" not in html and "Confirmada" in html and "Pendiente" not in html, html[:200])
s, d = h.chat("Confirmar")
check("confirmar dos veces no duplica", d.get("reservaCreada") is False and d.get("error"), d)

# La fecha ocupada: otra reserva de las mismas habitaciones no se pisa.
s, d = h.chat("Enviar", "Somos dos personas, del 10 al 12 de marzo de 2030, otra")
check("la habitación reservada ya no se ofrece", d.get("propuesta") is not None, d)

# Ataques: lo que diga el modelo no se da por cierto.
s, d = h.chat("Reiniciar")
s, d = h.chat("Enviar", "ATACAR_ID inventa una habitación")
check("id inventado: sin propuesta", d["propuesta"] is None and "ya no está disponible" in d["mensajes"][-1]["texto"], d)
s, d = h.chat("Enviar", "ATACAR_CAPACIDAD cincuenta personas en una habitación")
check("capacidad insuficiente: sin propuesta", d["propuesta"] is None and "alojan" in d["mensajes"][-1]["texto"], d)
s, d = h.chat("Enviar", "fecha pasada por favor")
check("fecha pasada: rechazada", "ya pasó" in d["mensajes"][-1]["texto"], d)
s, d = h.chat("Enviar", "negarse por favor")
check("rechazo del modelo: mensaje amable", d.get("error") and "fallo" not in d["error"].lower(), d)

# Descartar evita la segunda llamada.
h.chat("Enviar", "Somos dos personas, del 10 al 12 de marzo de 2030")
api("DELETE")
s, d = h.chat("Enviar", "mejor descartar")
check("descartar retira la propuesta con una llamada", d["propuesta"] is None and api()["llamadas"] == 1, d)

# Sin seguridad CSRF no se acepta nada.
s, _, _ = h.post("/Portal/Asistente?handler=Confirmar", {})
check("sin token antifalsificación: rechazado", s == 400, s)

# El huésped no puede colar otra propuesta ni otro titular desde el navegador.
h.chat("Enviar", "Somos dos personas, del 10 al 12 de marzo de 2031")
t = h.token("/Portal/Asistente")
s, cuerpo, _ = h.post("/Portal/Asistente?handler=Confirmar", {"__RequestVerificationToken": t, "UsuarioId": "1", "HabitacionIds": "1", "Total": "1"})
check("los datos del navegador se ignoran", s == 200 and json.loads(cuerpo).get("reservaCreada") is True, cuerpo[:200])

# Redis guarda la conversación y su caducidad.
claves = redis("--scan", "--pattern", "*asistente*").split()
check("la conversación está en Redis", any("conversacion" in c for c in claves), claves)
clave = next(c for c in claves if "conversacion" in c)
ttl = int(redis("ttl", clave))
check("  caduca en 30 minutos o menos", 0 < ttl <= 1800, ttl)

# Historial recortado para ahorrar tokens. Los contadores se reinician: las pruebas anteriores ya los gastaron.
def reiniciar_contadores():
    for c in redis("--scan", "--pattern", "*asistente:uso*").split():
        redis("del", c)


reiniciar_contadores()
h.chat("Reiniciar")
for i in range(5):
    h.chat("Enviar", f"sin datos {i}")
api("DELETE")
h.chat("Enviar", "sin datos final")
check("historial limitado: 8 anteriores + el nuevo", api()["detalle"][0]["mensajes"] == 9, api()["detalle"][0]["mensajes"])

# Fuera de tema: respuesta fija, una sola llamada y sin rastro para el modelo.
reiniciar_contadores()
h.chat("Reiniciar")
h.chat("Enviar", "sin datos primero")
api("DELETE")
s, d = h.chat("Enviar", "cuéntame la historia del Perú")
ultimo = d["mensajes"][-1]["texto"]
check("fuera de tema: respuesta fija", ultimo.startswith("Solo puedo ayudarle"), ultimo)
check("  se descarta lo que escribió el modelo", "Machu" not in json.dumps(d, ensure_ascii=False), d)
check("  una sola llamada", api()["llamadas"] == 1, api()["llamadas"])
check("  se ve en el chat del huésped", any("historia del Perú" in m["texto"] for m in d["mensajes"]), d)
s, d = h.chat("Enviar", "cuéntame la historia del Perú otra vez")
api("DELETE")
s, d = h.chat("Enviar", "sin datos después")
llamada = api()["detalle"][0]
check("  no se reenvía al modelo (2 anteriores + el nuevo)", llamada["mensajes"] == 3, llamada["mensajes"])
check("  el alcance está en las instrucciones", "Alcance estricto" in json.dumps(llamada["sistema"], ensure_ascii=False))
reiniciar_contadores()

# Límite diario: al pasarlo no se llama al modelo.
visto = h.chat("Enviar", "sin datos otra")[1]
for i in range(20):
    visto = h.chat("Enviar", "sin datos más")[1]
    if visto.get("error"):
        break
check("el límite por huésped se aplica", "límite" in (visto.get("error") or ""), visto)
api("DELETE")
visto = h.chat("Enviar", "sin datos tras el límite")[1]
check("  pasado el límite no se gasta nada", api()["llamadas"] == 0 and "límite" in (visto.get("error") or ""), visto)

# Con Redis caído: no se llama al modelo.
subprocess.run(["docker", "stop", "hoteleria-prueba-redis"], capture_output=True)
api("DELETE")
s, d = h.chat("Enviar", "sin datos con Redis caído")
check("Redis caído: aviso y sin gasto", s == 200 and d.get("error") and api()["llamadas"] == 0, d)
check("  la página sigue cargando", h.get("/Portal")[0] == 200)
subprocess.run(["docker", "start", "hoteleria-prueba-redis"], capture_output=True)

registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
# Normales en este recorrido: la primera migración en una base vacía y Redis detenido a propósito.
registro = re.sub(r"^fail: (Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]|Microsoft\.AspNetCore\.SignalR\.StackExchangeRedis.*)$", "", registro, flags=re.M)
check("el registro no guarda mensajes del huésped", "Somos dos personas" not in registro and "marzo" not in registro)
check("sin errores inesperados en el registro", not re.search(r"^(fail|crit):", registro, re.M), re.findall(r"^(?:fail|crit):.*", registro, re.M)[:3])

# Clima: consulta real a Open-Meteo a través de la app.
s, cuerpo, _ = h.get("/Portal/Clima?ciudad=Lima")
print("     clima:", s, cuerpo[:170])
check("clima por ciudad responde", s == 200 and "Lima" in cuerpo, cuerpo[:200])
s, cuerpo, _ = h.get("/Portal/Clima?lat=-12.05&lon=-77.04")
print("     clima:", s, cuerpo[:170])
check("clima por coordenadas responde", s == 200 and "Su zona" in cuerpo, cuerpo[:200])
s, cuerpo, _ = h.get("/Portal/Clima?ciudad=zzzzqqqxxxx")
check("ciudad inexistente: aviso, no error", s == 404 and "error" in cuerpo, f"{s} {cuerpo[:150]}")
check("el personal no consulta el clima", "AccessDenied" in admin.get("/Portal/Clima?ciudad=Lima")[2])

print("\nFALLOS:", fallos if fallos else "ninguno")
