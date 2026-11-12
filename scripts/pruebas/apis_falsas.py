"""APIs falsas para las pruebas (Anthropic, Geoapify, MigoAPI, ApisTed y Luhnify) en 127.0.0.1:18999.
Imitan el formato de cada documentación, cuentan las llamadas y nunca usan claves reales. Las lanza correr.sh."""
import json
import re
from http.server import BaseHTTPRequestHandler, HTTPServer

LLAMADAS = []
CONSULTAS_DOCUMENTOS = []


def _estado(codigo, datos):
    return codigo, datos


def documento_falso(metodo, ruta, cabeceras, cuerpo):
    """PeruAPI, ApisTed y Luhnify con el formato de su documentación."""
    CONSULTAS_DOCUMENTOS.append({"ruta": ruta.split("?")[0], "clave": bool(cabeceras.get("X-API-KEY") or cabeceras.get("Authorization")), "cuerpo": cuerpo})
    if ruta.startswith("/dni/"):
        dni = ruta.split("/")[-1]
        if dni == "87654321":
            return _estado(404, {"success": False, "message": "No encontrado"})
        if dni == "11111111":
            return _estado(500, {"success": False, "message": "error"})
        if dni == "33333333":
            return _estado(403, {"success": False, "message": "Token inválido"})
        if dni == "44444444":
            return _estado(200, {"success": True, "dni": dni, "nombre": "DE LA CRUZ SANCHEZ MARIA DEL PILAR"})
        return _estado(200, {"success": True, "dni": dni, "nombre": "QUISPE ÑAHUI ANA MARIA"})
    if ruta.startswith("/api/reniec/dni"):
        if cuerpo.get("dni") == "11111111":
            return _estado(503, {"message": "Fuente no disponible"})
        if cuerpo.get("dni") == "33333333":
            return _estado(200, {"dni": "33333333", "nombres": "LUIS ALBERTO", "apellido_paterno": "ROJAS", "apellido_materno": "PEÑA"})
        return _estado(404, {"message": "No encontrado"})
    if ruta.startswith("/api/migraciones/verificar-"):
        tipo = "CE" if ruta.endswith("-ce") else "CPP"
        if cuerpo.get("fecha_nacimiento") != "15/03/1990":
            return _estado(200, {"tipo": tipo, "numero": cuerpo.get("numero"), "encontrado": False, "mensaje": "No encontrado"})
        return _estado(200, {"tipo": tipo, "numero": cuerpo.get("numero"), "encontrado": True, "nombre_completo": "GARCIA LOPEZ, CARLOS EDUARDO",
                             "nacionalidad": "VENEZOLANA", "fecha_nacimiento": "15/03/1990", "calidad_migratoria": "RESIDENTE"})
    if ruta.startswith("/api/v1/validate"):
        if cuerpo.get("country_code") == "zz":
            return _estado(400, {"error": "Unsupported country"})
        return _estado(200, {"valid": cuerpo.get("document_number") == "ABC123456", "message": "ok"})
    return _estado(404, {})
CONSULTAS_LUGARES = []
CONSULTAS_HOTELES = []


def hoteles_falsos(ruta):
    """Formato documentado de SerpApi, con datos hostiles a propósito."""
    from urllib.parse import parse_qs, urlparse
    q = parse_qs(urlparse(ruta).query)
    CONSULTAS_HOTELES.append({k: v[0] for k, v in q.items() if k != "api_key"} | {"tiene_clave": "api_key" in q})
    if "vacio" in q.get("q", [""])[0]:
        return {"error": "Google hasn't returned any results for this query."}
    if "roto" in q.get("q", [""])[0]:
        return {"error": "Invalid API key. clave-falsa-de-prueba"}
    def hotel(i, precio, **extra):
        return {"type": "hotel", "name": f"Hotel {i}", "property_token": f"tok{i}", "overall_rating": 4.2, "reviews": 100 + i, "extracted_hotel_class": 4,
                "link": f"https://hotel{i}.test/", "rate_per_night": {"lowest": f"${precio}", "extracted_lowest": precio},
                "prices": [{"source": "Booking.com", "rate_per_night": {"extracted_lowest": precio}}, {"source": "Agoda", "rate_per_night": {"extracted_lowest": precio + 5}}]} | extra
    return {"properties": [
        hotel(1, 100), hotel(2, 200), hotel(3, 300),
        hotel(4, 150, name="<img src=x onerror=window.__xss=1>", link="javascript:window.__xss=1"),
        {"type": "vacation rental", "name": "Departamento", "property_token": "tokR", "rate_per_night": {"extracted_lowest": 10}},
        {"type": "hotel", "name": "Sin precio", "property_token": "tokSP"},
        {"type": "hotel", "name": "Sin token"},
    ]}


def lugares_falsos(ruta):
    """Formato documentado de Geoapify, con datos hostiles a propósito."""
    from urllib.parse import parse_qs, urlparse
    q = parse_qs(urlparse(ruta).query)
    CONSULTAS_LUGARES.append({k: v[0] for k, v in q.items() if k != "apiKey"} | {"tiene_clave": "apiKey" in q})
    lon, lat, radio = map(float, q["filter"][0].replace("circle:", "").split(","))
    desde = int(q.get("offset", ["0"])[0])
    categorias = q["categories"][0].split(",")
    features = []
    total = 20 if desde == 0 else 5
    for i in range(total):
        cat = categorias[i % len(categorias)]
        features.append({"type": "Feature", "geometry": {"type": "Point", "coordinates": [lon + 0.002 + i * 0.001, lat]}, "properties": {
            "name": f"Lugar {desde + i}", "lat": lat, "lon": lon + 0.002 + i * 0.001, "distance": (i + 1) * 90 + desde, "place_id": f"id-{desde + i}",
            "categories": [cat], "address_line2": "Calle Falsa 123, Lima", "opening_hours": "Mo-Fr 09:00-18:00; Su off",
            "website": "https://ejemplo.test/lugar", "contact": {"phone": "+51 1 234-5678"}, "catering": {"cuisine": "peruvian;pizza"}}})
    if desde == 0:
        features[1]["properties"].update({"name": "<img src=x onerror=window.__xss=1>", "website": "javascript:window.__xss=1", "contact": {"phone": "javascript:alert(1)"}})
        features[2]["properties"].pop("name")
        features[3]["properties"].pop("distance")
    return {"type": "FeatureCollection", "features": features}


def texto_ultimo_usuario(mensajes):
    for m in reversed(mensajes):
        if m["role"] != "user":
            continue
        c = m["content"]
        if isinstance(c, str):
            return c
        partes = [b.get("text", "") for b in c if b.get("type") == "text"]
        if partes:
            return " ".join(partes)
    return ""


def resultados_de_herramienta(mensajes):
    c = mensajes[-1]["content"]
    if isinstance(c, str):
        return []
    return [b for b in c if b.get("type") == "tool_result"]


def contenido(resultado):
    c = resultado.get("content", "")
    return c if isinstance(c, str) else " ".join(b.get("text", "") for b in c)


def herramienta(nombre, entrada):
    return [{"type": "tool_use", "id": f"toolu_{len(LLAMADAS)}", "name": nombre, "input": entrada}]


def responder(cuerpo):
    mensajes = cuerpo["messages"]
    pedido = texto_ultimo_usuario(mensajes)
    resultados = resultados_de_herramienta(mensajes)
    fecha = "2030-03-10"

    if resultados:
        salida = contenido(resultados[0])
        if resultados[0].get("is_error"):
            return [{"type": "text", "text": f"El sistema rechazó la operación: {salida}"}], "end_turn"
        if "ATACAR_ID" in pedido:
            return herramienta("preparar_reserva", {"fecha_entrada": fecha, "noches": 2, "personas": 1, "habitacion_ids": [999999]}), "tool_use"
        if "ATACAR_CAPACIDAD" in pedido:
            primero = int(re.search(r": (\d+)\|", salida).group(1))
            return herramienta("preparar_reserva", {"fecha_entrada": fecha, "noches": 2, "personas": 50, "habitacion_ids": [primero]}), "tool_use"
        if "SOLO_BUSCAR" in pedido:
            return [{"type": "text", "text": "Encontré opciones. ¿Cuál prefiere?"}], "end_turn"
        primero = int(re.search(r": (\d+)\|", salida).group(1))
        return herramienta("preparar_reserva", {"fecha_entrada": fecha, "noches": 2, "personas": 2, "habitacion_ids": [primero]}), "tool_use"

    if "historia del Perú" in pedido:
        # El modelo desobedece y escribe la respuesta junto a la llamada: el servidor debe descartarla.
        return [{"type": "text", "text": "Machu Picchu fue construida por los incas."}] + herramienta("fuera_de_tema", {}), "tool_use"
    if "<script>" in pedido:
        return [{"type": "text", "text": "Eco <script>window.__xss=1</script> sin ejecutar"}], "end_turn"
    if "sin datos" in pedido:
        return [{"type": "text", "text": "¿Qué fechas y cuántas personas?"}], "end_turn"
    if "descartar" in pedido:
        return herramienta("descartar_propuesta", {}), "tool_use"
    if "fecha pasada" in pedido:
        return herramienta("buscar_habitaciones", {"fecha_entrada": "2001-01-01", "noches": 2}), "tool_use"
    if "negarse" in pedido:
        return [], "refusal"
    return herramienta("buscar_habitaciones", {"fecha_entrada": fecha, "noches": 2, "personas": 2}), "tool_use"


class Manejador(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _enviar(self, datos):
        crudo = json.dumps(datos).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(crudo)))
        self.end_headers()
        self.wfile.write(crudo)

    def do_GET(self):
        if self.path.startswith("/dni/"):
            self._documento("GET", {})
            return
        if self.path.startswith("/search.json"):
            self._enviar(hoteles_falsos(self.path))
            return
        if self.path.startswith("/v2/places"):
            self._enviar(lugares_falsos(self.path))
            return
        self._enviar({"llamadas": len(LLAMADAS), "detalle": LLAMADAS, "lugares": CONSULTAS_LUGARES, "hoteles": CONSULTAS_HOTELES, "documentos": CONSULTAS_DOCUMENTOS})

    def do_DELETE(self):
        LLAMADAS.clear()
        CONSULTAS_LUGARES.clear()
        CONSULTAS_DOCUMENTOS.clear()
        CONSULTAS_HOTELES.clear()
        self._enviar({"ok": True})

    def _documento(self, metodo, cuerpo):
        codigo, datos = documento_falso(metodo, self.path, dict(self.headers), cuerpo)
        crudo = json.dumps(datos).encode()
        self.send_response(codigo)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(crudo)))
        self.end_headers()
        self.wfile.write(crudo)

    def do_POST(self):
        cuerpo = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        if self.path.startswith("/api/"):
            self._documento("POST", cuerpo)
            return
        bloques, parada = responder(cuerpo)
        LLAMADAS.append({
            "modelo": cuerpo.get("model"),
            "max_tokens": cuerpo.get("max_tokens"),
            "esfuerzo": (cuerpo.get("output_config") or {}).get("effort"),
            "mensajes": len(cuerpo["messages"]),
            "tamano_sistema": len(json.dumps(cuerpo.get("system", ""))),
            "tamano_total": len(json.dumps(cuerpo)),
            "sistema": cuerpo.get("system"),
        })
        self._enviar({
            "id": "msg_prueba", "type": "message", "role": "assistant", "model": cuerpo.get("model"),
            "content": bloques, "stop_reason": parada, "stop_sequence": None, "stop_details": None,
            "container": None,
            "usage": {"input_tokens": 100, "output_tokens": 20, "cache_creation_input_tokens": 0, "cache_read_input_tokens": 0,
                      "cache_creation": None, "inference_geo": None, "server_tool_use": None, "service_tier": None},
        })


HTTPServer(("127.0.0.1", 18999), Manejador).serve_forever()
