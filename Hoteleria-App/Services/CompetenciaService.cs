using System.Globalization;
using System.Text.Json;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services;

public sealed record PrecioAgencia(string Fuente, decimal Precio);

public sealed record HotelMercado(string Token, string Nombre, int? Estrellas, double? Calificacion, int? Resenas, decimal? PrecioNoche, IReadOnlyList<PrecioAgencia> Agencias, string? Enlace);

public sealed record ComparacionCategoria(string Categoria, int Camas, decimal PrecioNoche, decimal? FrenteACompetidores, decimal? FrenteAlMercado);

public sealed record ConsultaGuardada(ConsultaCompetencia Consulta, IReadOnlyList<HotelMercado> Hoteles);

public sealed record ResultadoConsulta(ConsultaGuardada? Guardada, string? Error, bool Reutilizada = false);

public sealed record PuntoHistorial(DateTime RealizadaEn, DateTime Entrada, int Noches, int Adultos, decimal? MediaCompetidores, decimal? MedianaMercado);

/// <summary>
/// Tarifas públicas de otros hoteles, desde Google Hotels a través de SerpApi. Solo informa: nunca
/// cambia precios. El plan gratuito es de 250 búsquedas al mes, así que solo se gasta al pedirlo,
/// se reutiliza lo reciente y hay un tope mensual.
/// </summary>
public sealed class CompetenciaService(HttpClient http, IConfiguration config, HoteleriaDbContext db, RelojHotel reloj, ILogger<CompetenciaService> logger)
{
    public const int MaximoNoches = 30;
    public const int MaximoAdultos = 8;
    private const int HorasDeVigencia = 6;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ErrorClave = "SerpApi rechazó la clave. Revise SERPAPI_API_KEY en Render: reintentar no lo soluciona.";
    private const string ErrorCuota = "SerpApi no aceptó más búsquedas: puede haberse agotado el plan del mes o haber demasiadas seguidas.";
    private const string ErrorGeneral = "No se pudo consultar el mercado en este momento. Inténtelo más tarde.";

    private readonly string? _clave = config["SERPAPI_API_KEY"]?.Trim();
    private readonly string _baseUrl = config["SERPAPI_BASE_URL"] is { Length: > 0 } url ? url.TrimEnd('/') : "https://serpapi.com";

    public bool Disponible => !string.IsNullOrWhiteSpace(_clave);

    public int LimiteMensual => int.TryParse(config["COMPETENCIA_LIMITE_MENSUAL"], out var limite) ? limite : 200;

    public async Task<int> BusquedasDelMesAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var inicioMes = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await db.ConsultasCompetencia.CountAsync(c => c.RealizadaEn >= inicioMes, ct);
    }

    public string? Validar(string ciudad, DateTime entrada, int noches, int adultos)
    {
        if (string.IsNullOrWhiteSpace(ciudad) || ciudad.Trim().Length > 60) return "Indique una ciudad de hasta 60 caracteres.";
        if (entrada.Date < reloj.Hoy) return "La fecha de entrada no puede ser anterior a hoy.";
        if (entrada.Date > reloj.Hoy.AddDays(365)) return "La fecha de entrada no puede superar un año.";
        if (noches is < 1 or > MaximoNoches) return $"Las noches deben estar entre 1 y {MaximoNoches}.";
        if (adultos is < 1 or > MaximoAdultos) return $"Los adultos deben estar entre 1 y {MaximoAdultos}.";
        return null;
    }

    /// <summary>Última foto guardada para esos criterios. No gasta búsquedas.</summary>
    public async Task<ConsultaGuardada?> UltimaAsync(string ciudad, DateTime entrada, int noches, int adultos, CancellationToken ct = default)
    {
        var fecha = Fecha(entrada);
        var nombre = ciudad.Trim();
        var consulta = await db.ConsultasCompetencia.AsNoTracking()
            .Where(c => c.Ciudad == nombre && c.Entrada == fecha && c.Noches == noches && c.Adultos == adultos)
            .OrderByDescending(c => c.RealizadaEn)
            .FirstOrDefaultAsync(ct);
        return consulta is null ? null : new(consulta, Leer(consulta.Resultado));
    }

    public async Task<ResultadoConsulta> ConsultarAsync(string ciudad, DateTime entrada, int noches, int adultos, CancellationToken ct = default)
    {
        if (!Disponible) return new(null, "La comparación de tarifas no está configurada: falta SERPAPI_API_KEY.");
        if (Validar(ciudad, entrada, noches, adultos) is { } invalido) return new(null, invalido);

        var nombre = ciudad.Trim();
        var reciente = await UltimaAsync(nombre, entrada, noches, adultos, ct);
        if (reciente is not null && reciente.Consulta.RealizadaEn > DateTime.UtcNow.AddHours(-HorasDeVigencia))
            return new(reciente, null, Reutilizada: true);

        if (await BusquedasDelMesAsync(ct) >= LimiteMensual)
            return new(reciente, $"Se alcanzó el tope de {LimiteMensual} búsquedas de este mes. Se muestra la última consulta guardada, si existe.");

        var fecha = Fecha(entrada);
        var (hoteles, fallo) = await BuscarAsync(nombre, fecha, noches, adultos, ct);
        if (hoteles is null) return new(reciente, fallo);

        var consulta = new ConsultaCompetencia
        {
            RealizadaEn = DateTime.UtcNow,
            Ciudad = nombre,
            Entrada = fecha,
            Noches = noches,
            Adultos = adultos,
            Resultado = JsonSerializer.Serialize(hoteles, Json)
        };
        db.ConsultasCompetencia.Add(consulta);
        await db.SaveChangesAsync(ct);
        return new(new(consulta, hoteles), null);
    }

    private async Task<(List<HotelMercado>? Hoteles, string? Fallo)> BuscarAsync(string ciudad, DateTime entrada, int noches, int adultos, CancellationToken ct)
    {
        var parametros = new Dictionary<string, string>
        {
            ["engine"] = "google_hotels",
            ["q"] = $"hoteles en {ciudad}",
            ["check_in_date"] = entrada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["check_out_date"] = entrada.AddDays(noches).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["adults"] = adultos.ToString(CultureInfo.InvariantCulture),
            ["currency"] = "USD",
            ["hl"] = "es",
            ["api_key"] = _clave!
        };
        var url = $"{_baseUrl}/search.json?" + string.Join('&', parametros.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        try
        {
            using var respuesta = await http.GetAsync(url, ct);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            using var documento = JsonDocument.Parse(cuerpo);
            var raiz = documento.RootElement;

            if (raiz.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                // Sin resultados no es un fallo: una ciudad puede no tener hoteles para esas fechas.
                if (error.GetString()!.Contains("hasn't returned any results", StringComparison.OrdinalIgnoreCase)) return ([], null);
                logger.LogWarning("SerpApi rechazó la consulta con el estado {Estado}.", (int)respuesta.StatusCode);
                var claveInvalida = error.GetString()!.Contains("API key", StringComparison.OrdinalIgnoreCase);
                return (null, MotivoDe(respuesta.StatusCode, claveInvalida));
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning("SerpApi respondió con el estado {Estado}.", (int)respuesta.StatusCode);
                return (null, MotivoDe(respuesta.StatusCode, false));
            }

            if (!raiz.TryGetProperty("properties", out var propiedades) || propiedades.ValueKind != JsonValueKind.Array) return ([], null);
            return (propiedades.EnumerateArray().Select(Proyectar).OfType<HotelMercado>().ToList(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Solo el tipo de error: la dirección de la consulta lleva la clave.
            logger.LogWarning("No se pudo consultar SerpApi: {Tipo}", ex.GetType().Name);
            return (null, ErrorGeneral);
        }
    }

    private static string MotivoDe(System.Net.HttpStatusCode estado, bool claveInvalida) => estado switch
    {
        _ when claveInvalida => ErrorClave,
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => ErrorClave,
        System.Net.HttpStatusCode.TooManyRequests => ErrorCuota,
        _ => ErrorGeneral
    };

    private static HotelMercado? Proyectar(JsonElement p)
    {
        var nombre = Texto(p, "name");
        var token = Texto(p, "property_token");
        if (nombre is null || token is null) return null;
        // Google Hotels también puede devolver alquileres vacacionales, que no son competencia directa.
        if (Texto(p, "type") is { } tipo && !tipo.Equals("hotel", StringComparison.OrdinalIgnoreCase)) return null;

        var agencias = new List<PrecioAgencia>();
        if (p.TryGetProperty("prices", out var precios) && precios.ValueKind == JsonValueKind.Array)
        {
            foreach (var precio in precios.EnumerateArray())
            {
                if (Texto(precio, "source") is { } fuente && PrecioDe(precio) is { } valor) agencias.Add(new(fuente, valor));
            }
        }

        return new HotelMercado(token, nombre,
            Numero(p, "extracted_hotel_class") is { } estrellas ? (int)estrellas : null,
            Numero(p, "overall_rating"),
            Numero(p, "reviews") is { } resenas ? (int)resenas : null,
            PrecioDe(p) ?? (agencias.Count > 0 ? agencias.Min(a => a.Precio) : null),
            agencias,
            EnlaceSeguro(Texto(p, "link")));
    }

    private static decimal? PrecioDe(JsonElement elemento) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty("rate_per_night", out var tarifa) && Numero(tarifa, "extracted_lowest") is { } valor and > 0
            ? Math.Round((decimal)valor, 2)
            : null;

    /// <summary>Compara cada categoría propia. Una habitación con tantas camas como adultos, o la más ajustada que los aloje.</summary>
    public async Task<List<ComparacionCategoria>> CompararAsync(IReadOnlyList<HotelMercado> hoteles, IReadOnlyCollection<string> seguidos, int adultos, CancellationToken ct = default)
    {
        var habitaciones = await db.Habitaciones.AsNoTracking()
            .Where(h => h.NroCamas >= adultos)
            .Select(h => new { h.TipoVipNombre, h.NroCamas, h.TipoVip.PrecioCama })
            .ToListAsync(ct);

        var mercado = hoteles.Where(h => h.PrecioNoche is not null).Select(h => h.PrecioNoche!.Value).ToList();
        var competidores = hoteles.Where(h => h.PrecioNoche is not null && seguidos.Contains(h.Token)).Select(h => h.PrecioNoche!.Value).ToList();
        var mediaCompetidores = competidores.Count > 0 ? competidores.Average() : (decimal?)null;
        var medianaMercado = Mediana(mercado);

        return habitaciones
            .GroupBy(h => h.TipoVipNombre)
            .Select(g =>
            {
                var elegida = g.OrderBy(h => h.NroCamas).First();
                var precio = elegida.NroCamas * elegida.PrecioCama;
                return new ComparacionCategoria(g.Key, elegida.NroCamas, precio, Diferencia(precio, mediaCompetidores), Diferencia(precio, medianaMercado));
            })
            .OrderBy(c => c.PrecioNoche)
            .ToList();
    }

    public async Task<List<PuntoHistorial>> HistorialAsync(string ciudad, IReadOnlyCollection<string> seguidos, int cantidad = 8, CancellationToken ct = default)
    {
        var nombre = ciudad.Trim();
        var consultas = await db.ConsultasCompetencia.AsNoTracking()
            .Where(c => c.Ciudad == nombre)
            .OrderByDescending(c => c.RealizadaEn)
            .Take(cantidad)
            .ToListAsync(ct);

        return consultas.Select(c =>
        {
            var hoteles = Leer(c.Resultado);
            var propios = hoteles.Where(h => h.PrecioNoche is not null && seguidos.Contains(h.Token)).Select(h => h.PrecioNoche!.Value).ToList();
            return new PuntoHistorial(c.RealizadaEn, c.Entrada, c.Noches, c.Adultos,
                propios.Count > 0 ? Math.Round(propios.Average(), 2) : null,
                Mediana(hoteles.Where(h => h.PrecioNoche is not null).Select(h => h.PrecioNoche!.Value).ToList()));
        }).ToList();
    }

    public static decimal? Mediana(List<decimal> valores)
    {
        if (valores.Count == 0) return null;
        var orden = valores.Order().ToList();
        var medio = orden.Count / 2;
        return orden.Count % 2 == 1 ? orden[medio] : Math.Round((orden[medio - 1] + orden[medio]) / 2, 2);
    }

    private static decimal? Diferencia(decimal propio, decimal? referencia) =>
        referencia is > 0 ? Math.Round((propio - referencia.Value) / referencia.Value * 100, 1) : null;

    private static List<HotelMercado> Leer(string resultado)
    {
        try { return JsonSerializer.Deserialize<List<HotelMercado>>(resultado, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static DateTime Fecha(DateTime fecha) => DateTime.SpecifyKind(fecha.Date, DateTimeKind.Unspecified);

    /// <summary>Solo https: el dato viene de un tercero y acaba en un enlace.</summary>
    private static string? EnlaceSeguro(string? enlace) =>
        Uri.TryCreate(enlace, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;

    private static string? Texto(JsonElement objeto, string nombre) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()
            : null;

    private static double? Numero(JsonElement objeto, string nombre) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDouble(out var numero)
            ? numero
            : null;
}
