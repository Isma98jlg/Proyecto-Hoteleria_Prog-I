using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Hoteleria_App.Services;

public sealed record LugarEncontrado(string Nombre, double Latitud, double Longitud);

public sealed record ClimaActual(string Lugar, double Temperatura, double Sensacion, int Humedad, double Viento, string Descripcion, string Icono);

/// <summary>
/// Clima actual desde Open-Meteo, que no requiere clave. La ubicación se redondea a unos 11 km antes de
/// consultarla y guardarla en caché, sin asociarla a ningún usuario, y nunca se escribe en los registros.
/// </summary>
public sealed class ClimaService(HttpClient http, IDistributedCache cache, ILogger<ClimaService> logger)
{
    private const string UrlClima = "https://api.open-meteo.com/v1/forecast";
    private const string UrlCiudades = "https://geocoding-api.open-meteo.com/v1/search";
    private static readonly DistributedCacheEntryOptions Ttl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) };

    public static bool CoordenadasValidas(double latitud, double longitud) => latitud is >= -90 and <= 90 && longitud is >= -180 and <= 180;

    public async Task<ClimaActual?> PorCoordenadasAsync(double latitud, double longitud, string lugar = "Su zona", CancellationToken ct = default)
    {
        var lat = Math.Round(latitud, 1).ToString("0.0", CultureInfo.InvariantCulture);
        var lon = Math.Round(longitud, 1).ToString("0.0", CultureInfo.InvariantCulture);
        var clave = $"clima:{lat}:{lon}";

        var guardado = await LeerCacheAsync(clave, ct);
        if (guardado is not null) return JsonSerializer.Deserialize<ClimaActual>(guardado)! with { Lugar = lugar };

        try
        {
            var url = $"{UrlClima}?latitude={lat}&longitude={lon}&timezone=auto&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day";
            using var documento = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            var actual = documento.RootElement.GetProperty("current");
            var (descripcion, icono) = Describir(actual.GetProperty("weather_code").GetInt32(), actual.GetProperty("is_day").GetInt32() == 1);
            var clima = new ClimaActual(lugar,
                actual.GetProperty("temperature_2m").GetDouble(),
                actual.GetProperty("apparent_temperature").GetDouble(),
                actual.GetProperty("relative_humidity_2m").GetInt32(),
                actual.GetProperty("wind_speed_10m").GetDouble(),
                descripcion, icono);
            await EscribirCacheAsync(clave, JsonSerializer.Serialize(clima), ct);
            return clima;
        }
        catch (Exception ex) when (EsFalloExterno(ex))
        {
            logger.LogWarning("No se pudo consultar el clima: {Tipo}", ex.GetType().Name);
            return null;
        }
    }

    public async Task<ClimaActual?> PorCiudadAsync(string ciudad, CancellationToken ct = default) =>
        await BuscarLugarAsync(ciudad, ct) is { } lugar
            ? await PorCoordenadasAsync(lugar.Latitud, lugar.Longitud, lugar.Nombre, ct)
            : null;

    /// <summary>Convierte el nombre de una ciudad en coordenadas.</summary>
    public async Task<LugarEncontrado?> BuscarLugarAsync(string ciudad, CancellationToken ct = default)
    {
        try
        {
            var url = $"{UrlCiudades}?count=1&language=es&format=json&name={Uri.EscapeDataString(ciudad.Trim())}";
            using var documento = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            if (!documento.RootElement.TryGetProperty("results", out var resultados) || resultados.GetArrayLength() == 0) return null;

            var primero = resultados[0];
            var nombre = primero.GetProperty("name").GetString() ?? ciudad;
            var lugar = primero.TryGetProperty("country", out var pais) ? $"{nombre}, {pais.GetString()}" : nombre;
            return new LugarEncontrado(lugar, primero.GetProperty("latitude").GetDouble(), primero.GetProperty("longitude").GetDouble());
        }
        catch (Exception ex) when (EsFalloExterno(ex))
        {
            logger.LogWarning("No se pudo buscar la ciudad: {Tipo}", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Códigos meteorológicos WMO usados por Open-Meteo.</summary>
    private static (string Descripcion, string Icono) Describir(int codigo, bool esDeDia) => codigo switch
    {
        0 => ("Despejado", esDeDia ? "clear_day" : "clear_night"),
        1 => ("Mayormente despejado", esDeDia ? "clear_day" : "clear_night"),
        2 => ("Parcialmente nublado", esDeDia ? "partly_cloudy_day" : "partly_cloudy_night"),
        3 => ("Nublado", "cloud"),
        45 or 48 => ("Niebla", "foggy"),
        51 or 53 or 55 => ("Llovizna", "rainy"),
        56 or 57 => ("Llovizna helada", "rainy"),
        61 or 63 or 65 => ("Lluvia", "rainy"),
        66 or 67 => ("Lluvia helada", "rainy"),
        71 or 73 or 75 or 77 => ("Nieve", "ac_unit"),
        80 or 81 or 82 => ("Chubascos", "rainy"),
        85 or 86 => ("Chubascos de nieve", "ac_unit"),
        95 or 96 or 99 => ("Tormenta", "thunderstorm"),
        _ => ("Sin descripción", "cloud")
    };

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

    private static bool EsFalloExterno(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException;
}
