"""Explora tu zona contra una API falsa de Geoapify: consultas, seguridad, caché y permisos."""
import http.cookiejar
import json
import os
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request

BASE = "http://localhost:5077"
API = "http://127.0.0.1:18999"
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

    def entrar(self, correo, clave):
        html = self.get("/Account/Login")[1]
        t = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)
        nombres = re.findall(r'name="([^"]*(?:mail|ass)[^"]*)"', html, re.I)
        cuerpo = urllib.parse.urlencode({nombres[0]: correo, nombres[1]: clave, "__RequestVerificationToken": t}).encode()
        self.op.open(urllib.request.Request(BASE + "/Account/Login", cuerpo), timeout=30)


def api(metodo="GET"):
    with urllib.request.urlopen(urllib.request.Request(API + "/x", method=metodo), timeout=5) as r:
        return json.loads(r.read())


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
h = Cliente()
h.entrar("huesped@grandstay.com", env["HU"])
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])

s, html, _ = h.get("/Portal/Explora")
check("el huésped abre la página", s == 200)
check("  atribución de Geoapify y OpenStreetMap", "geoapify.com" in html and "openstreetmap.org/copyright" in html)
check("  12 filtros", html.count("data-explora-filtro=") == 12, html.count("data-explora-filtro="))
check("  la clave no llega al navegador", "clave-falsa-de-prueba" not in html)
portal = h.get("/Portal")[1]
check("el portal destaca la función", "explora-banner" in portal and "explora-boton" in portal)
check("el administrador no ve el botón", "explora-boton" not in admin.get("/Portal")[1])
check("el administrador no entra", "AccessDenied" in admin.get("/Portal/Explora")[2])
check("sin sesión no se entra", "Login" in Cliente().get("/Portal/Explora?handler=Buscar&lat=1&lon=1")[2])

base = "/Portal/Explora?handler=Buscar&lat=-12.12049&lon=-77.03011"
api("DELETE")
s, cuerpo, _ = h.get(base + "&radio=2000")
d = json.loads(cuerpo)
consulta = api()["lugares"][0]
check("busca y responde", s == 200 and d["lugares"], cuerpo[:200])
check("  usa la clave en el servidor", consulta["tiene_clave"] is True)
check("  la clave no vuelve al navegador", "clave-falsa" not in cuerpo)
check("  redondea la ubicación", consulta["filter"] == "circle:-77.030,-12.120,2000", consulta["filter"])
check("  ordena por cercanía", consulta["bias"] == "proximity:-77.030,-12.120", consulta["bias"])
check("  pide 20 (1 crédito)", consulta["limit"] == "20", consulta["limit"])
check("  sin filtros incluye todas las categorías", all(k in consulta["categories"] for k in ["catering.restaurant", "leisure.park", "education.library", "healthcare.pharmacy"]), consulta["categories"])
distancias = [l["distanciaMetros"] for l in d["lugares"] if l["distanciaMetros"] is not None]
check("  del más cercano al más lejano", distancias == sorted(distancias), distancias)
check("  omite lugares sin nombre", len(d["lugares"]) == 19, len(d["lugares"]))
check("  avisa de que hay más", d["hayMas"] is True)

hostil = next(l for l in d["lugares"] if "onerror" in l["nombre"])
check("web javascript: descartada", hostil["web"] is None, hostil)
check("teléfono hostil descartado", hostil["telefono"] is None, hostil)
normal = d["lugares"][0]
check("horario en español", normal["horario"] == "Lu-Vi 09:00-18:00; Do cerrado", normal["horario"])
check("teléfono y web válidos", normal["telefono"] == "+51 1 234-5678" and normal["web"].startswith("https://"), normal)

api("DELETE")
h.get(base + "&radio=2000")
check("repetir la búsqueda no gasta créditos", api()["lugares"] == [], api()["lugares"])
claves = subprocess.run(["docker", "exec", "hoteleria-prueba-redis", "redis-cli", "--scan", "--pattern", "*lugares*"], capture_output=True, text=True).stdout.split()
check("  el resultado está en Redis", len(claves) == 1, claves)
ttl = int(subprocess.run(["docker", "exec", "hoteleria-prueba-redis", "redis-cli", "ttl", claves[0]], capture_output=True, text=True).stdout)
check("  y caduca en una hora o menos", 0 < ttl <= 3600, ttl)

api("DELETE")
s, cuerpo, _ = h.get(base + "&radio=500&filtros=parques,bibliotecas")
consulta = api()["lugares"][0]
check("filtros combinados", consulta["categories"] == "education.library,leisure.park", consulta["categories"])
check("  radio de 500 m", consulta["filter"].endswith(",500"), consulta["filter"])
check("  cada lugar con su filtro", {l["filtro"] for l in json.loads(cuerpo)["lugares"]} == {"parques", "bibliotecas"}, cuerpo[:200])

api("DELETE")
h.get(base + "&radio=50000")
check("«Ver todos» usa 50 km", api()["lugares"][0]["filter"].endswith(",50000"))

api("DELETE")
s, cuerpo, _ = h.get(base + "&radio=2000&desplazamiento=20")
check("«Ver más» pide la página siguiente", api()["lugares"][0]["offset"] == "20" and json.loads(cuerpo)["hayMas"] is False, cuerpo[:150])

for nombre, ruta, esperado in [
    ("radio no permitido", base + "&radio=7", 400),
    ("filtro desconocido", base + "&filtros=casinos", 400),
    ("inyección en filtro", base + "&filtros=parques%26apiKey%3Dx", 400),
    ("desplazamiento excesivo", base + "&desplazamiento=9999", 400),
    ("latitud fuera de rango", "/Portal/Explora?handler=Buscar&lat=500&lon=1", 400),
    ("sin coordenadas", "/Portal/Explora?handler=Buscar", 400),
]:
    s = h.get(ruta)[0]
    check(f"rechaza: {nombre}", s == esperado, s)

s, cuerpo, _ = h.get("/Portal/Explora?handler=Ciudad&ciudad=Lima")
d = json.loads(cuerpo) if s == 200 else {}
check("ciudad real a coordenadas", s == 200 and "Lima" in d.get("nombre", "") and abs(d["latitud"] + 12.04) < 1, cuerpo[:200])
s, cuerpo, _ = h.get("/Portal/Explora?handler=Ciudad&ciudad=zzzzqqqxxxx")
check("ciudad inexistente: aviso", s == 404 and "error" in cuerpo, f"{s} {cuerpo[:100]}")
check("el clima sigue funcionando", "Lima" in h.get("/Portal/Clima?ciudad=Lima")[1])

registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
check("el registro no guarda clave ni ubicación", "clave-falsa" not in registro and "-12.120" not in registro and "-77.030" not in registro)
registro = re.sub(r"^fail: Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]$", "", registro, flags=re.M)
check("sin errores inesperados", not re.search(r"^(fail|crit):", registro, re.M), re.findall(r"^(?:fail|crit):.*", registro, re.M)[:3])

print("\nFALLOS:", fallos if fallos else "ninguno")
