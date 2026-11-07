using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Distributed;

namespace Hoteleria_App.Services;

public sealed record FiltroLugar(string Clave, string Etiqueta, string Icono, string Color, string Categorias);

public sealed record LugarCercano(string Id, string Nombre, string Filtro, string? Detalle, string? Direccion, int? DistanciaMetros,
    double Latitud, double Longitud, string? Horario, string? Telefono, string? Web);

public sealed record ResultadoLugares(double Latitud, double Longitud, int RadioMetros, IReadOnlyList<LugarCercano> Lugares, bool HayMas);

/// <summary>
/// Lugares de interés cercanos desde Geoapify. Cada consulta de hasta 20 resultados cuesta 1 crédito del plan
/// gratuito, por eso se guarda en caché. La ubicación se redondea a unos 110 m, no se asocia a ningún usuario
/// y nunca se escribe en los registros. Los datos no incluyen menús ni precios.
/// </summary>
public sealed partial class LugaresService(HttpClient http, IConfiguration config, IDistributedCache cache, ILogger<LugaresService> logger)
{
    /// <summary>Radio de «Ver todos»: la API necesita un área, y más allá ya no es «su zona».</summary>
    public const int RadioTodos = 50_000;
    public const int MaximoDesplazamiento = 200;
    private const int PorPagina = 20;

    public static readonly IReadOnlySet<int> Radios = new HashSet<int> { 500, 1_000, 2_000, 5_000, 10_000, RadioTodos };

    public static readonly IReadOnlyList<FiltroLugar> Filtros =
    [
        new("restaurantes", "Restaurantes", "restaurant", "#c2410c", "catering.restaurant,catering.fast_food"),
        new("cafes", "Cafés", "local_cafe", "#92400e", "catering.cafe"),
        new("bares", "Bares", "local_bar", "#be185d", "catering.bar,catering.pub"),
        new("parques", "Parques", "park", "#15803d", "leisure.park"),
        new("bibliotecas", "Bibliotecas", "local_library", "#4338ca", "education.library"),
        new("museos", "Museos y cultura", "museum", "#7c3aed", "entertainment.museum,entertainment.culture"),
        new("cines", "Cines", "movie", "#0f766e", "entertainment.cinema"),
        new("turismo", "Turismo", "photo_camera", "#0369a1", "tourism.attraction,tourism.sights"),
        new("supermercados", "Supermercados", "shopping_cart", "#4d7c0f", "commercial.supermarket"),
        new("compras", "Centros comerciales", "local_mall", "#a21caf", "commercial.shopping_mall"),
        new("farmacias", "Farmacias", "local_pharmacy", "#dc2626", "healthcare.pharmacy"),
        new("cajeros", "Bancos y cajeros", "local_atm", "#475569", "service.financial.atm,service.financial.bank")
    ];

    private static readonly Dictionary<string, FiltroLugar> FiltrosPorClave = Filtros.ToDictionary(f => f.Clave);

    private static readonly Dictionary<string, string> Cocinas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["american"] = "Americana", ["argentinian"] = "Argentina", ["asian"] = "Asiática", ["barbecue"] = "Parrilla", ["burger"] = "Hamburguesas",
        ["chicken"] = "Pollo", ["chinese"] = "China", ["coffee"] = "Café", ["coffee_shop"] = "Café", ["fish"] = "Pescado", ["french"] = "Francesa",
        ["indian"] = "India", ["international"] = "Internacional", ["italian"] = "Italiana", ["japanese"] = "Japonesa", ["korean"] = "Coreana",
        ["mexican"] = "Mexicana", ["peruvian"] = "Peruana", ["pizza"] = "Pizza", ["regional"] = "Regional", ["sandwich"] = "Sándwiches",
        ["seafood"] = "Mariscos", ["spanish"] = "Española", ["steak_house"] = "Carnes", ["sushi"] = "Sushi", ["thai"] = "Tailandesa", ["vegetarian"] = "Vegetariana"
    };

    private static readonly DistributedCacheEntryOptions Ttl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    private readonly string? _clave = config["GEOAPIFY_API_KEY"];
    private readonly string _baseUrl = config["GEOAPIFY_BASE_URL"] is { Length: > 0 } url ? url.TrimEnd('/') : "https://api.geoapify.com";

    public bool Disponible => !string.IsNullOrWhiteSpace(_clave);

    /// <summary>Convierte «cafes,parques» en una lista ordenada. Devuelve null si alguna clave no existe; vacía significa «todo».</summary>
    public static List<string>? LeerFiltros(string? filtros)
    {
        var claves = (filtros ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Order().ToList();
        return claves.All(FiltrosPorClave.ContainsKey) ? claves : null;
    }

    public async Task<ResultadoLugares?> BuscarAsync(double latitud, double longitud, int radio, IReadOnlyList<string> filtros, int desplazamiento, CancellationToken ct = default)
    {
        if (!Disponible) return null;

        var lat = Math.Round(latitud, 3);
        var lon = Math.Round(longitud, 3);
        var latTexto = lat.ToString("0.000", CultureInfo.InvariantCulture);
        var lonTexto = lon.ToString("0.000", CultureInfo.InvariantCulture);
        var clave = $"lugares:{latTexto}:{lonTexto}:{radio}:{string.Join('+', filtros)}:{desplazamiento}";

        var guardado = await LeerCacheAsync(clave, ct);
        if (guardado is not null) return JsonSerializer.Deserialize<ResultadoLugares>(guardado);

        try
        {
            var elegidos = filtros.Count == 0 ? Filtros : filtros.Select(f => FiltrosPorClave[f]);
            var categorias = string.Join(',', elegidos.Select(f => f.Categorias));
            var url = $"{_baseUrl}/v2/places?categories={categorias}&filter=circle:{lonTexto},{latTexto},{radio}&bias=proximity:{lonTexto},{latTexto}" +
                      $"&limit={PorPagina}&offset={desplazamiento}&lang=es&apiKey={Uri.EscapeDataString(_clave!)}";
            using var documento = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            var elementos = documento.RootElement.GetProperty("features").EnumerateArray().ToList();
            var lugares = elementos.Select(Proyectar).OfType<LugarCercano>().OrderBy(l => l.DistanciaMetros ?? int.MaxValue).ToList();

            var resultado = new ResultadoLugares(lat, lon, radio, lugares, elementos.Count == PorPagina);
            await EscribirCacheAsync(clave, JsonSerializer.Serialize(resultado), ct);
            return resultado;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Solo el tipo de error: la dirección de la consulta lleva la clave y la ubicación.
            logger.LogWarning("No se pudieron consultar los lugares: {Tipo}", ex.GetType().Name);
            return null;
        }
    }

    private static LugarCercano? Proyectar(JsonElement elemento)
    {
        if (!elemento.TryGetProperty("properties", out var p)) return null;
        var nombre = Texto(p, "name");
        if (nombre is null || !Numero(p, "lat", out var lat) || !Numero(p, "lon", out var lon)) return null;

        // La documentación de Places no detalla estos campos: se leen de donde estén y, si no, se omiten.
        p.TryGetProperty("datasource", out var fuente);
        var crudo = fuente.ValueKind == JsonValueKind.Object && fuente.TryGetProperty("raw", out var r) ? r : default;
        p.TryGetProperty("contact", out var contacto);
        p.TryGetProperty("catering", out var servicio);

        var categorias = p.TryGetProperty("categories", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];

        return new LugarCercano(
            Texto(p, "place_id") ?? $"{lat}:{lon}",
            nombre,
            FiltroDe(categorias),
            Cocina(Texto(servicio, "cuisine") ?? Texto(crudo, "cuisine"), categorias),
            Texto(p, "address_line2") ?? Texto(p, "formatted"),
            Numero(p, "distance", out var distancia) ? (int)Math.Round(distancia) : null,
            lat, lon,
            Horario(Texto(p, "opening_hours") ?? Texto(crudo, "opening_hours")),
            Telefono(Texto(contacto, "phone") ?? Texto(crudo, "phone") ?? Texto(crudo, "contact:phone")),
            WebSegura(Texto(p, "website") ?? Texto(crudo, "website") ?? Texto(crudo, "contact:website")));
    }

    private static string FiltroDe(List<string> categorias) =>
        Filtros.FirstOrDefault(f => f.Categorias.Split(',').Any(k => categorias.Any(c => c == k || c.StartsWith(k + ".", StringComparison.Ordinal))))?.Clave ?? "turismo";

    private static string? Cocina(string? cuisine, List<string> categorias)
    {
        var valor = cuisine?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            ?? categorias.Where(c => c.StartsWith("catering.", StringComparison.Ordinal) && c.Count(x => x == '.') == 2).Select(c => c[(c.LastIndexOf('.') + 1)..]).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (Cocinas.TryGetValue(valor, out var etiqueta)) return etiqueta;
        var legible = valor.Replace('_', ' ');
        return char.ToUpperInvariant(legible[0]) + legible[1..];
    }

    /// <summary>Solo direcciones http o https: el dato viene de un tercero y acaba en un enlace.</summary>
    private static string? WebSegura(string? web)
    {
        if (string.IsNullOrWhiteSpace(web)) return null;
        var completa = web.Contains(':', StringComparison.Ordinal) ? web : $"https://{web}";
        return Uri.TryCreate(completa, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    private static string? Telefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var primero = telefono.Split(';')[0].Trim();
        return TelefonoValido().IsMatch(primero) ? primero : null;
    }

    private static string? Horario(string? horario)
    {
        if (string.IsNullOrWhiteSpace(horario)) return null;
        return Dias().Replace(horario, m => m.Value switch
        {
            "Mo" => "Lu", "Tu" => "Ma", "We" => "Mi", "Th" => "Ju", "Fr" => "Vi", "Sa" => "Sá", "Su" => "Do", "PH" => "Festivos", "off" => "cerrado", _ => m.Value
        });
    }

    [GeneratedRegex(@"\b(Mo|Tu|We|Th|Fr|Sa|Su|PH|off)\b")]
    private static partial Regex Dias();

    [GeneratedRegex(@"^\+?[0-9 ()\-.]{5,25}$")]
    private static partial Regex TelefonoValido();

    private static string? Texto(JsonElement objeto, string nombre) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()
            : null;

    private static bool Numero(JsonElement objeto, string nombre, out double numero)
    {
        numero = 0;
        return objeto.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDouble(out numero);
    }

    // La caché es opcional: si falla, se consulta el servicio externo.
    private async Task<string?> LeerCacheAsync(string clave, CancellationToken ct)
    {
        try { return await cache.GetStringAsync(clave, ct); }
        catch (Exception ex) when (EsFalloDeCache(ex)) { return null; }
    }

    private async Task EscribirCacheAsync(string clave, string valor, CancellationToken ct)
    {
        try { await cache.SetStringAsync(clave, valor, Ttl, ct); }
        catch (Exception ex) when (EsFalloDeCache(ex)) { }
    }

    private static bool EsFalloDeCache(Exception ex) => ex is StackExchange.Redis.RedisException or TimeoutException;
}
