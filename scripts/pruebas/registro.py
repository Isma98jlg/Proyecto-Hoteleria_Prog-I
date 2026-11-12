"""Registro con verificación de documentos contra APIs falsas: coincidencias, privacidad, token y cuota."""
import html
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

    def post(self, ruta, datos):
        cuerpo = urllib.parse.urlencode(datos).encode()
        try:
            with self.op.open(urllib.request.Request(BASE + ruta, cuerpo), timeout=60) as r:
                return r.status, r.read().decode(), r.geturl()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.geturl()

    def token(self, ruta):
        return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', self.get(ruta)[1]).group(1))

    def entrar(self, correo, clave):
        h = self.get("/Account/Login")[1]
        t = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', h).group(1))
        self.post("/Account/Login", {"Input.Email": correo, "Input.Password": clave, "Input.RememberMe": "false", "__RequestVerificationToken": t})


def api(metodo="GET"):
    with urllib.request.urlopen(urllib.request.Request(API + "/x", method=metodo), timeout=5) as r:
        return json.loads(r.read())


def verificar(c, tipo, numero, nombres, apellidos, pais="", fecha="", consentimiento="true"):
    datos = {"Persona.TipoDocumento": tipo, "Persona.NumeroDocumento": numero, "Persona.Nombres": nombres, "Persona.Apellidos": apellidos,
             "Persona.PaisEmisor": pais, "FechaNacimiento": fecha, "consentimiento": consentimiento,
             "__RequestVerificationToken": c.token("/Account/Registro")}
    s, cuerpo, _ = c.post("/Account/Registro?handler=Verificar", datos)
    return s, (json.loads(cuerpo) if s == 200 else {}), cuerpo


def registrar(c, tipo, numero, nombres, apellidos, correo, token="", pais="", nacionalidad="Venezolana"):
    datos = {"Persona.TipoDocumento": tipo, "Persona.NumeroDocumento": numero, "Persona.Nombres": nombres, "Persona.Apellidos": apellidos,
             "Persona.PaisEmisor": pais, "Persona.Nacionalidad": nacionalidad, "Persona.Telefono": "999888777", "Persona.EMail": correo,
             "TokenVerificacion": token, "Password": "Registro-Prueba-1!", "Confirmacion": "Registro-Prueba-1!",
             "__RequestVerificationToken": c.token("/Account/Registro")}
    return c.post("/Account/Registro", datos)


def redis(*orden):
    return subprocess.run(["docker", "exec", "hoteleria-prueba-redis", "redis-cli", *orden], capture_output=True, text=True).stdout.strip()


def reiniciar_cuota():
    for clave in redis("--scan", "--pattern", "*verificacion:intentos*").split():
        redis("del", clave)


env = dict(l.strip().split("=", 1) for l in open(os.environ["PRUEBA_ENV"]) if "=" in l)
admin = Cliente()
admin.entrar("admin@grandstay.com", env["AD"])

def uid_de(numero):
    # Búsqueda exacta en la base: la del directorio también encuentra números que contienen este.
    salida = subprocess.run(["docker", "exec", "hoteleria-prueba-pg", "psql", "-U", "hotel", "-d", "hotel", "-tAc",
                             f"select usuario_id from \"Usuarios\" where numero_documento = '{numero}'"], capture_output=True, text=True).stdout.strip()
    return salida or None


def estado_de(numero):
    uid = uid_de(numero)
    if not uid:
        return None, ""
    ficha = html.unescape(admin.get(f"/Usuarios/Detalles/{uid}")[1])
    e = re.search(r"Documento: ([^<]+)</strong>", ficha)
    return (e.group(1).strip() if e else None), ficha

# --- La página
s, pagina, _ = Cliente().get("/Account/Registro")
check("la página de registro carga", s == 200)
check("  los 4 tipos de documento", all(f'value="{t}"' in pagina for t in ["DNI", "Pasaporte", "CPP", "CE"]))
check("  país emisor y fecha de nacimiento solo para su documento", 'data-solo="Pasaporte"' in pagina and 'data-solo="CPP CE"' in pagina)
check("  casilla de consentimiento", "data-registro-consentimiento" in pagina)
check("  ninguna clave en la página", "clave-falsa" not in pagina)

# --- DNI
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "DNI", "12345678", "", "")
consulta = api()["documentos"]
check("DNI sin escribir nombres: confirmado", r.get("estado") == "CONFIRMADO" and r.get("token"), crudo)
check("  rellena nombres y apellidos oficiales", r.get("nombres") == "Ana Maria" and r.get("apellidos") == "Quispe Ñahui" and r.get("nacionalidad") == "Peruana", crudo)
check("  llama a MigoAPI con el token y nada más", [c["ruta"] for c in consulta] == ["/dni/12345678"] and consulta[0]["clave"], consulta)
check("  fuente SUNAT (MigoAPI)", "MigoAPI" in r.get("mensaje", ""), crudo)
s, r, crudo = verificar(Cliente(), "DNI", "44444444", "", "")
check("  separa apellidos compuestos: «De La Cruz Sanchez» / «Maria Del Pilar»", r.get("apellidos") == "De La Cruz Sanchez" and r.get("nombres") == "Maria Del Pilar", crudo)
check("  no reenvía la respuesta cruda", '"nombre"' not in crudo and "success" not in crudo and "clave" not in crudo, crudo)
s, r, crudo = verificar(Cliente(), "DNI", "87654321", "", "")
check("DNI inexistente: aviso sin token", "No encontramos" in r.get("mensaje", "") and not r.get("token") and not r.get("nombres"), crudo)
s, r, crudo = verificar(Cliente(), "DNI", "11111111", "", "")
check("API caída: se puede continuar sin verificar", r.get("estado") == "SIN_VERIFICAR" and "no está disponible" in r.get("mensaje", "") and not r.get("token"), crudo)
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "DNI", "33333333", "", "")
consulta = api()["documentos"]
check("MigoAPI rechaza el token: responde RENIEC por ApisTed", r.get("estado") == "CONFIRMADO" and r.get("nombres") == "Luis Alberto" and r.get("apellidos") == "Rojas Peña" and "ApisTed" in r.get("mensaje", ""), crudo)
check("  ApisTed recibe el DNI en el cuerpo, con token", [c["ruta"] for c in consulta] == ["/dni/33333333", "/api/reniec/dni"] and consulta[1]["cuerpo"] == {"dni": "33333333"} and consulta[1]["clave"], consulta)
api("DELETE")
verificar(Cliente(), "DNI", "87654321", "", "")
check("  un DNI que MigoAPI no encuentra se busca en RENIEC", [c["ruta"] for c in api()["documentos"]] == ["/dni/87654321", "/api/reniec/dni"])
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "DNI", "1234", "", "")
check("DNI mal formado: sin gastar consulta", "8 d" in r.get("mensaje", "") and api()["documentos"] == [], crudo)
s, r, crudo = verificar(Cliente(), "DNI", "12345678", "", "", consentimiento="false")
check("sin consentimiento no se consulta", api()["documentos"] == [] and not r.get("token") and not r.get("nombres"), crudo)

# --- CE y CPP
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "CE", "001234567", "Carlos", "Garcia")
check("CE sin fecha de nacimiento: la pide y no consulta", "fecha de nacimiento" in r.get("mensaje", "") and api()["documentos"] == [], crudo)
s, r, crudo = verificar(Cliente(), "CE", "001234567", "carlos eduardo", "García López", fecha="1990-03-15")
consulta = api()["documentos"]
check("CE: confirmado y con datos oficiales", r.get("estado") == "CONFIRMADO" and r.get("nombres") == "Carlos Eduardo" and r.get("apellidos") == "Garcia Lopez" and r.get("nacionalidad") == "Venezolana", crudo)
check("  envía número y fecha dd/mm/aaaa a ApisTed", consulta and consulta[-1]["ruta"] == "/api/migraciones/verificar-ce" and consulta[-1]["cuerpo"] == {"numero": "001234567", "fecha_nacimiento": "15/03/1990"}, consulta)
check("  no reenvía la calidad migratoria ni la fecha", "RESIDENTE" not in crudo and "15/03/1990" not in crudo, crudo)
s, r, crudo = verificar(Cliente(), "CE", "001234567", "Carlos", "Garcia", fecha="1991-01-01")
check("CE con otra fecha: no encontrado", "no encontró" in r.get("mensaje", ""), crudo)
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "CPP", "123456789", "Carlos", "Lopez", fecha="1990-03-15")
check("CPP usa su propia ruta y confirma", r.get("estado") == "CONFIRMADO" and api()["documentos"][-1]["ruta"] == "/api/migraciones/verificar-cpp", crudo)

# --- Pasaporte
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "Pasaporte", "ABC123456", "John", "Smith", pais="ES")
consulta = api()["documentos"]
check("pasaporte válido: solo formato válido, sin nombres oficiales", r.get("estado") == "FORMATO_VALIDO" and r.get("token") and not r.get("nombres"), crudo)
check("  envía país, tipo y número a Luhnify", consulta and consulta[-1]["cuerpo"] == {"country_code": "es", "document_type": "passport", "document_number": "ABC123456"}, consulta)
s, r, crudo = verificar(Cliente(), "Pasaporte", "XYZ99999", "John", "Smith", pais="ES")
check("pasaporte con formato inválido: sin verificar", r.get("estado") == "SIN_VERIFICAR" and "formato" in r.get("mensaje", ""), crudo)
reiniciar_cuota()
api("DELETE")
s, r, crudo = verificar(Cliente(), "Pasaporte", "ABC123456", "John", "Smith", pais="PE")
check("pasaporte peruano: no se consulta", api()["documentos"] == [] and "peruanos" in r.get("mensaje", ""), crudo)
s, r, crudo = verificar(Cliente(), "Pasaporte", "ABC123456", "John", "Smith")
check("pasaporte sin país emisor: lo pide", "país" in r.get("mensaje", "").lower(), crudo)

# --- Registro completo
reiniciar_cuota()
c = Cliente()
s, r, _ = verificar(c, "DNI", "12345678", "", "")
s, cuerpo, url = registrar(c, "DNI", "12345678", r["nombres"], r["apellidos"], "ana.quispe@grandstay.com", token=r["token"])
check("registro con DNI confirmado", s == 200 and "/Portal" in url, f"{s} {url}")
estado, ficha = estado_de("12345678")
check("  queda confirmado, con fuente y fecha", estado == "Confirmado" and "SUNAT (MigoAPI)" in ficha, estado)
check("  nacionalidad peruana automática", "Peruana" in ficha)

c = Cliente()
reiniciar_cuota()
s, r, _ = verificar(c, "CE", "001234567", "", "", fecha="1990-03-15")
registrar(c, "CE", "001234567", "Carlos", "Gomez", "carlos.gomez@grandstay.com", token=r["token"])
check("cambiar los nombres tras verificar anula la verificación", estado_de("001234567")[0] == "Sin verificar", estado_de("001234567")[0])

c = Cliente()
s, r, _ = verificar(c, "CPP", "123456789", "", "", fecha="1990-03-15")
tok = r["token"]
registrar(c, "CPP", "123456789", r["nombres"], r["apellidos"], "carlos.lopez@grandstay.com", token=tok)
check("registro con CPP confirmado", estado_de("123456789")[0] == "Confirmado", estado_de("123456789")[0])
registrar(Cliente(), "CPP", "123456789", r["nombres"], r["apellidos"], "otro.lopez@grandstay.com", token=tok)
check("  el mismo token no se reutiliza", estado_de("123456789")[0] == "Confirmado")

registrar(Cliente(), "DNI", "22223333", "Luis", "Rojas", "luis.rojas@grandstay.com", token="inventado-por-el-navegador", nacionalidad="")
check("token inventado: sin verificar", estado_de("22223333")[0] == "Sin verificar", estado_de("22223333")[0])

reiniciar_cuota()
c = Cliente()
s, r, _ = verificar(c, "Pasaporte", "ABC123456", "", "", pais="ES")
registrar(c, "Pasaporte", "ABC123456", "John", "Smith", "john.smith@grandstay.com", token=r["token"], pais="es", nacionalidad="Española")
estado, ficha = estado_de("ABC123456")
check("registro con pasaporte: formato válido y país emisor", estado == "Formato válido" and "emitido en ES" in ficha, estado)

s, cuerpo, _ = registrar(Cliente(), "Pasaporte", "QWE12345", "Mia", "Lee", "mia.lee@grandstay.com", pais="")
check("registro de pasaporte sin país emisor: rechazado", s == 200 and "Elija el pa" in html.unescape(cuerpo))
s, cuerpo, _ = registrar(Cliente(), "Otro", "999", "Mia", "Lee", "mia.lee2@grandstay.com")
check("tipo de documento no permitido: rechazado", s == 200 and "tipo de documento" in html.unescape(cuerpo))

# --- El personal edita los nombres: la verificación se reinicia
uid = uid_de("12345678")
form = admin.get(f"/Usuarios/Editar/{uid}")[1]
tok_e = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', form).group(1))
s, _, _ = admin.post(f"/Usuarios/Editar/{uid}", {"Persona.TipoDocumento": "DNI", "Persona.NumeroDocumento": "12345678", "Persona.Nombres": "Ana Maria", "Persona.Apellidos": "Quispe",
                                                 "Persona.Nacionalidad": "Peruana", "Persona.Telefono": "999888777", "Persona.EMail": "ana.quispe@grandstay.com", "__RequestVerificationToken": tok_e})
check("editar los nombres reinicia la verificación", estado_de("12345678")[0] == "Sin verificar", (s, estado_de("12345678")[0]))

# --- Personal en ventanilla: verificar y autocompletar
def verificar_personal(c, ruta, tipo, numero, nombres="", apellidos="", pais="", fecha="", consentimiento="true"):
    datos = {"Persona.TipoDocumento": tipo, "Persona.NumeroDocumento": numero, "Persona.Nombres": nombres, "Persona.Apellidos": apellidos,
             "Persona.PaisEmisor": pais, "FechaNacimiento": fecha, "consentimiento": consentimiento, "__RequestVerificationToken": c.token(ruta)}
    s, cuerpo, url = c.post(ruta + "?handler=Verificar", datos)
    try:
        return s, json.loads(cuerpo), url
    except ValueError:
        return s, {}, url


def guardar_personal(c, ruta, tipo, numero, nombres, apellidos, nacionalidad, correo, token="", pais=""):
    return c.post(ruta, {"Persona.TipoDocumento": tipo, "Persona.NumeroDocumento": numero, "Persona.Nombres": nombres, "Persona.Apellidos": apellidos,
                         "Persona.PaisEmisor": pais, "Persona.Nacionalidad": nacionalidad, "Persona.Telefono": "999888777", "Persona.EMail": correo,
                         "TokenVerificacion": token, "__RequestVerificationToken": c.token(ruta)})


reiniciar_cuota()
s, pagina, _ = admin.get("/Usuarios/Crear")
check("alta en ventanilla: bloque de verificación", s == 200 and 'id="verificar-documento"' in pagina and "verificacion-personal.js" in pagina)
check("  edición en ventanilla: bloque de verificación", 'id="verificar-documento"' in admin.get(f"/Usuarios/Editar/{uid}")[1])

s, r, _ = verificar_personal(admin, "/Usuarios/Crear", "CE", "007654321", consentimiento="false")
check("personal sin consentimiento: no consulta", r.get("token") is None and "autoriza" in r.get("mensaje", ""), r)

s, r, _ = verificar_personal(admin, "/Usuarios/Crear", "CE", "007654321", fecha="1990-03-15")
check("personal: CE autocompleta nombres, apellidos y nacionalidad",
      r.get("nombres") == "Carlos Eduardo" and r.get("apellidos") == "Garcia Lopez" and r.get("nacionalidad") == "Venezolana" and r.get("token"), r)
s, _, url = guardar_personal(admin, "/Usuarios/Crear", "CE", "007654321", r["nombres"], r["apellidos"], r["nacionalidad"], "carlos.garcia@grandstay.com", token=r["token"])
estado, ficha = estado_de("007654321")
check("  alta guardada como confirmada por Migraciones", estado == "Confirmado" and "ApisTed" in ficha, (s, url, estado))

s, r, _ = verificar_personal(admin, "/Usuarios/Crear", "CPP", "765432100", fecha="1990-03-15")
guardar_personal(admin, "/Usuarios/Crear", "CPP", "765432100", "Pedro", "Garcia", "Venezolana", "pedro.garcia@grandstay.com", token=r.get("token", ""))
check("  si el personal cambia los nombres oficiales, queda sin verificar", estado_de("765432100")[0] == "Sin verificar", estado_de("765432100")[0])

s, r, _ = verificar_personal(admin, f"/Usuarios/Editar/{uid}", "DNI", "12345678")
check("personal: DNI en edición autocompleta", r.get("nombres") == "Ana Maria" and r.get("apellidos") == "Quispe Ñahui" and r.get("nacionalidad") == "Peruana", r)
guardar_personal(admin, f"/Usuarios/Editar/{uid}", "DNI", "12345678", r["nombres"], r["apellidos"], "Peruana", "ana.quispe@grandstay.com", token=r["token"])
check("  edición guardada como confirmada", estado_de("12345678")[0] == "Confirmado", estado_de("12345678")[0])

s, r, _ = verificar_personal(admin, "/Usuarios/Crear", "DNI", "87654321")
check("personal: DNI inexistente, sin datos", r.get("nombres") is None and "No encontramos" in r.get("mensaje", ""), r)
s, r, _ = verificar_personal(admin, "/Usuarios/Crear", "Pasaporte", "ABC123456", pais="ES")
check("personal: pasaporte da formato válido sin inventar nombres", r.get("estado") == "FORMATO_VALIDO" and r.get("token") and not r.get("nombres"), r)

anon = Cliente()
s, cuerpo, url = anon.post("/Usuarios/Crear?handler=Verificar", {"Persona.TipoDocumento": "DNI", "Persona.NumeroDocumento": "12345678", "consentimiento": "true",
                                                                 "__RequestVerificationToken": anon.token("/Account/Registro")})
check("sin sesión no se puede autocompletar", "ANA" not in cuerpo.upper() or "Login" in url, (s, url))
huesped = Cliente()
huesped.entrar("huesped@grandstay.com", env["HU"])
s, cuerpo, url = huesped.get("/Usuarios/Crear")
check("un huésped no accede al alta en ventanilla", s in (403, 404) or "AccessDenied" in url or "Login" in url, (s, url))

registro_personal = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
check("autocompletar tampoco escribe datos en el registro", not any(x in registro_personal for x in ["007654321", "765432100", "GARCIA", "Carlos Eduardo"]))

# --- Límite por hora
reiniciar_cuota()
api("DELETE")
for _ in range(5):
    verificar(Cliente(), "DNI", "12345678", "Ana", "Quispe")
s, r, crudo = verificar(Cliente(), "DNI", "12345678", "Ana", "Quispe")
check("límite de 5 verificaciones por hora en el registro público", "límite" in r.get("mensaje", "") and len(api()["documentos"]) == 5, (len(api()["documentos"]), crudo))

registro = open(os.environ["PRUEBA_LOG"], encoding="utf-8", errors="replace").read()
check("el registro no guarda documentos, nombres ni claves", not any(x in registro for x in ["12345678", "001234567", "ABC123456", "QUISPE", "Quispe", "clave-falsa", "15/03/1990"]))
limpio = re.sub(r"^fail: Microsoft\.EntityFrameworkCore\.Database\.Command\[20102\]$", "", registro, flags=re.M)
check("sin errores inesperados", not re.search(r"^(fail|crit):", limpio, re.M), re.findall(r"^(?:fail|crit):.*", limpio, re.M)[:3])

print("\nFALLOS:", fallos if fallos else "ninguno")
