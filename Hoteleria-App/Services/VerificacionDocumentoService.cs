using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoteleria_App.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace Hoteleria_App.Services;

public sealed record SolicitudVerificacion(string Tipo, string Numero, string Nombres, string Apellidos, string? PaisEmisor, DateTime? FechaNacimiento);

/// <summary>Resultado y, si la fuente confirmó el documento, los datos oficiales con que se rellena el formulario.</summary>
public sealed record ResultadoVerificacion(string Estado, string Mensaje, string? Token, string? Nombres, string? Apellidos, string? Nacionalidad);

/// <summary>Resultado ya comprobado en el servidor, listo para guardarse con la cuenta.</summary>
public sealed record DocumentoVerificado(string Estado, string? Fuente, DateTime? En);

/// <summary>
/// Comprueba el documento del huésped: DNI con PeruAPI, CE y CPP con ApisTed (Migraciones) y pasaportes con
/// Luhnify. Los nombres de DNI y carnés salen de la fuente oficial, no de lo que escriba el usuario; por eso el
/// registro público exige consentimiento y tiene poca cuota por IP. Del pasaporte solo se comprueba el formato.
/// Ni la fecha de nacimiento ni la respuesta de las APIs se guardan, y nada de esto se registra.
/// </summary>
public sealed partial class VerificacionDocumentoService(HttpClient http, IConfiguration config, IDistributedCache cache, ILogger<VerificacionDocumentoService> logger)
{
    private const int VerificacionesPorHora = 5;
    private const int VerificacionesPorHoraPersonal = 40;
    private static readonly DistributedCacheEntryOptions TtlToken = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
    private static readonly DistributedCacheEntryOptions TtlContador = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    private const string NoDisponible = "La verificación automática no está disponible en este momento. Puede continuar: recepción revisará su documento.";

    /// <summary>Países con código ISO de 2 letras, para elegir el emisor del pasaporte.</summary>
    public static readonly IReadOnlyList<string> Paises = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(c => { try { return new RegionInfo(c.Name).TwoLetterISORegionName; } catch (ArgumentException) { return null; } })
        .Where(c => c is { Length: 2 } && c.All(char.IsAsciiLetterUpper))
        .Select(c => c!)
        .Distinct()
        .Order()
        .ToList();

    private string? Clave(string nombre) => config[nombre] is { Length: > 0 } valor ? valor.Trim() : null;
    private string Base(string nombre, string porDefecto) => config[nombre] is { Length: > 0 } url ? url.TrimEnd('/') : porDefecto;

    /// <summary>Comprueba la forma del documento antes de gastar una consulta. Devuelve el error o null.</summary>
    public static string? ValidarFormato(string tipo, string numero, string? paisEmisor)
    {
        numero = numero.Trim();
        return tipo switch
        {
            TiposDocumento.Dni => Dni().IsMatch(numero) ? null : "El DNI tiene 8 dígitos.",
            TiposDocumento.Ce or TiposDocumento.Cpp => Carne().IsMatch(numero) ? null : "Escriba solo los dígitos del carné.",
            TiposDocumento.Pasaporte when !Pasaporte().IsMatch(numero) => "El número de pasaporte solo lleva letras y números (de 5 a 15).",
            TiposDocumento.Pasaporte when paisEmisor is null || !Paises.Contains(paisEmisor.Trim().ToUpperInvariant()) => "Elija el país que emitió el pasaporte.",
            TiposDocumento.Pasaporte => null,
            _ => "Elija un tipo de documento válido."
        };
    }

    /// <summary>Registro público: sin sesión, con poca cuota por IP porque devuelve los datos oficiales.</summary>
    public Task<ResultadoVerificacion> VerificarAsync(SolicitudVerificacion s, string ip, CancellationToken ct = default) =>
        ConsultarYGuardarAsync(s, $"ip:{ip}", VerificacionesPorHora, publico: true, ct);

    /// <summary>Personal en ventanilla (exige permiso sobre el directorio).</summary>
    public Task<ResultadoVerificacion> AutocompletarAsync(SolicitudVerificacion s, long usuarioPersonal, CancellationToken ct = default) =>
        ConsultarYGuardarAsync(s, $"personal:{usuarioPersonal}", VerificacionesPorHoraPersonal, publico: false, ct);

    /// <summary>
    /// Consulta la fuente oficial y devuelve sus nombres para rellenar el formulario. El token queda ligado a esos
    /// datos: si se cambian antes de guardar, la ficha queda sin verificar.
    /// </summary>
    private async Task<ResultadoVerificacion> ConsultarYGuardarAsync(SolicitudVerificacion s, string cuota, int limite, bool publico, CancellationToken ct)
    {
        if (ValidarFormato(s.Tipo, s.Numero, s.PaisEmisor) is { } error) return Sin(error);
        if ((s.Tipo is TiposDocumento.Ce or TiposDocumento.Cpp) && s.FechaNacimiento is null)
            return Sin("Indique la fecha de nacimiento: Migraciones la pide para verificar el carné.");
        if (!await RegistrarIntentoAsync(cuota, limite, ct))
            return Sin(publico ? "Se alcanzó el límite de verificaciones por ahora. Puede continuar sin verificar: recepción revisará su documento." : "Se alcanzó el límite de verificaciones por ahora. Inténtelo más tarde.");

        var c = await ConsultarAsync(s, ct);
        var mensaje = publico ? c.Mensaje : c.Mensaje.Replace("Puede continuar: recepción lo revisará.", "Revise el documento en persona.")
            .Replace("Puede continuar: recepción revisará su documento.", "Revise el documento en persona.")
            .Replace("Recepción confirmará el pasaporte al llegar.", "Compruebe el pasaporte en persona.");
        if (c.Oficial is not { } o)
            return new(c.Estado, mensaje, c.Estado == EstadosVerificacion.SinVerificar ? null : await GuardarAsync(s, c.Estado, c.Fuente, ct), null, null, null);

        var token = await GuardarAsync(s with { Nombres = o.Nombres, Apellidos = o.Apellidos }, c.Estado, c.Fuente, ct);
        return new(c.Estado, publico ? $"Documento confirmado con {c.Fuente}. Completamos sus datos tal como figuran en él." : $"Documento confirmado por {c.Fuente}. Se completaron los datos oficiales: revíselos con el huésped.",
            token, o.Nombres, o.Apellidos, o.Nacionalidad);
    }

    private static ResultadoVerificacion Sin(string mensaje) => new(EstadosVerificacion.SinVerificar, mensaje, null, null, null, null);

    private async Task<Consulta> ConsultarAsync(SolicitudVerificacion s, CancellationToken ct) => s.Tipo switch
    {
        TiposDocumento.Dni => await ConsultarDniAsync(s, ct),
        TiposDocumento.Ce or TiposDocumento.Cpp => await ConsultarCarneAsync(s, ct),
        _ => await ConsultarPasaporteAsync(s, ct)
    };

    /// <summary>
    /// Recupera la verificación hecha en este registro. Solo vale una vez y solo si el documento y los nombres
    /// no cambiaron después; en otro caso, la cuenta se crea sin verificar.
    /// </summary>
    public async Task<DocumentoVerificado> ConsumirAsync(string? token, DatosPersona datos, CancellationToken ct = default)
    {
        var sinVerificar = new DocumentoVerificado(EstadosVerificacion.SinVerificar, null, null);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return sinVerificar;
        try
        {
            var clave = $"verificacion:{token}";
            var guardado = await cache.GetStringAsync(clave, ct);
            if (guardado is null) return sinVerificar;
            await cache.RemoveAsync(clave, ct);

            var v = JsonSerializer.Deserialize<VerificacionGuardada>(guardado)!;
            var pais = datos.TipoDocumento == TiposDocumento.Pasaporte ? datos.PaisEmisor?.Trim().ToUpperInvariant() : null;
            var mismos = v.Huella == Huella(datos.TipoDocumento.Trim(), datos.NumeroDocumento.Trim(), datos.Nombres, datos.Apellidos, pais);
            return mismos ? new(v.Estado, v.Fuente, v.Estado == EstadosVerificacion.SinVerificar ? null : v.En) : sinVerificar;
        }
        catch (Exception ex) when (EsFalloDeCache(ex)) { return sinVerificar; }
    }

    private async Task<Consulta> ConsultarDniAsync(SolicitudVerificacion s, CancellationToken ct)
    {
        if (Clave("PERUAPI_API_KEY") is not { } clave) return Consulta.Fallo(NoDisponible);
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{Base("PERUAPI_BASE_URL", "https://peruapi.com")}/api/dni/{s.Numero.Trim()}");
        peticion.Headers.Add("X-API-KEY", clave);
        var (estado, json) = await EnviarAsync("PeruAPI", peticion, ct);
        if (estado == HttpStatusCode.NotFound) return Consulta.Fallo("No encontramos ese DNI. Revise el número.");
        if (json is not { } d || Texto(d, "nombres") is not { } nombres) return Consulta.Fallo(NoDisponible);

        var apellidos = $"{Texto(d, "apellido_paterno")} {Texto(d, "apellido_materno")}".Trim();
        return new(EstadosVerificacion.Confirmado, "RENIEC (PeruAPI)", "Documento confirmado.",
            new DatosOficiales(Formatear(nombres), Formatear(apellidos), "Peruana"));
    }

    private async Task<Consulta> ConsultarCarneAsync(SolicitudVerificacion s, CancellationToken ct)
    {
        if (Clave("APISTED_TOKEN") is not { } token) return Consulta.Fallo(NoDisponible);
        var ruta = s.Tipo == TiposDocumento.Ce ? "verificar-ce" : "verificar-cpp";
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Base("APISTED_BASE_URL", "https://apisted.com/api")}/migraciones/{ruta}")
        {
            Content = Json(new { numero = s.Numero.Trim(), fecha_nacimiento = s.FechaNacimiento!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) })
        };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var (estado, json) = await EnviarAsync("ApisTed", peticion, ct);
        const string noEncontrado = "Migraciones no encontró ese carné con esa fecha de nacimiento.";
        if (estado == HttpStatusCode.NotFound) return Consulta.Fallo(noEncontrado);
        if (json is not { } d) return Consulta.Fallo(NoDisponible);
        if (!(d.TryGetProperty("encontrado", out var encontrado) && encontrado.ValueKind == JsonValueKind.True) || Texto(d, "nombre_completo") is not { } completo)
            return Consulta.Fallo(noEncontrado);

        // Migraciones suele escribir «APELLIDOS, NOMBRES». Sin coma no se puede separar con seguridad.
        var partes = completo.Split(',', 2, StringSplitOptions.TrimEntries);
        var (nombres, apellidos) = partes.Length == 2 ? (partes[1], partes[0]) : (completo, "");
        return new(EstadosVerificacion.Confirmado, "Migraciones (ApisTed)", "Documento confirmado.",
            new DatosOficiales(Formatear(nombres), Formatear(apellidos), Texto(d, "nacionalidad") is { } n ? Formatear(n) : null));
    }

    private async Task<Consulta> ConsultarPasaporteAsync(SolicitudVerificacion s, CancellationToken ct)
    {
        var pais = s.PaisEmisor!.Trim().ToUpperInvariant();
        if (pais == "PE") return Consulta.Fallo("Los pasaportes peruanos no se pueden validar automáticamente. Puede continuar: recepción lo revisará.");
        if (Clave("LUHNIFY_API_KEY") is not { } clave) return Consulta.Fallo(NoDisponible);

        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Base("LUHNIFY_BASE_URL", "https://luhnify.com")}/api/v1/validate")
        {
            Content = Json(new { country_code = pais.ToLowerInvariant(), document_type = "passport", document_number = s.Numero.Trim() })
        };
        peticion.Headers.Add("X-API-KEY", clave);
        var (estado, json) = await EnviarAsync("Luhnify", peticion, ct);
        // Luhnify responde 4xx cuando no conoce el formato de ese país: no es un error del huésped.
        if (json is not { } d) return Consulta.Fallo(estado is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError and not HttpStatusCode.Unauthorized and not HttpStatusCode.TooManyRequests
            ? "No podemos validar automáticamente los pasaportes de ese país. Puede continuar: recepción lo revisará."
            : NoDisponible);

        var valido = (d.TryGetProperty("valid", out var v) && v.ValueKind == JsonValueKind.True) || (d.TryGetProperty("isValid", out var iv) && iv.ValueKind == JsonValueKind.True);
        return valido
            ? new(EstadosVerificacion.FormatoValido, "Luhnify", "El número tiene un formato válido para ese país. Recepción confirmará el pasaporte al llegar.", null)
            : Consulta.Fallo("Ese número no tiene un formato válido para un pasaporte de ese país. Revíselo.");
    }

    /// <summary>Los nombres oficiales llegan en mayúsculas: «ANA MARÍA» se guarda como «Ana María».</summary>
    private static string Formatear(string texto) =>
        CultureInfo.GetCultureInfo("es-PE").TextInfo.ToTitleCase(texto.Trim().ToLowerInvariant());

    /// <summary>Cuerpo JSON con su tamaño indicado: algunos servidores rechazan los envíos por partes.</summary>
    private static StringContent Json(object cuerpo) => new(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");

    private async Task<(HttpStatusCode?, JsonElement?)> EnviarAsync(string servicio, HttpRequestMessage peticion, CancellationToken ct)
    {
        try
        {
            using var respuesta = await http.SendAsync(peticion, ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                // Solo el servicio y el estado: la petición lleva el documento y la clave.
                if (respuesta.StatusCode != HttpStatusCode.NotFound) logger.LogWarning("{Servicio} respondió con el estado {Estado}.", servicio, (int)respuesta.StatusCode);
                return (respuesta.StatusCode, null);
            }
            using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            return (respuesta.StatusCode, documento.RootElement.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("No se pudo consultar {Servicio}: {Tipo}", servicio, ex.GetType().Name);
            return (null, null);
        }
    }

    private static HashSet<string> Palabras(string texto)
    {
        var sinTildes = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return NoLetras().Split(sinTildes.ToUpperInvariant()).Where(p => p.Length > 0).ToHashSet();
    }

    /// <summary>
    /// Resumen de lo verificado: permite comprobarlo después sin guardar los datos en claro. Luhnify no conoce los
    /// nombres del pasaporte, así que en ese caso solo cuentan el número y el país.
    /// </summary>
    private static string Huella(string tipo, string numero, string nombres, string apellidos, string? pais)
    {
        var conNombres = tipo != TiposDocumento.Pasaporte;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tipo}|{numero}|{(conNombres ? string.Join(' ', Palabras(nombres).Order()) : "")}|{(conNombres ? string.Join(' ', Palabras(apellidos).Order()) : "")}|{pais}")));
    }

    private async Task<string?> GuardarAsync(SolicitudVerificacion s, string estado, string? fuente, CancellationToken ct)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var pais = s.Tipo == TiposDocumento.Pasaporte ? s.PaisEmisor?.Trim().ToUpperInvariant() : null;
        var guardado = new VerificacionGuardada(Huella(s.Tipo, s.Numero.Trim(), s.Nombres, s.Apellidos, pais), estado, fuente, DateTime.UtcNow);
        try
        {
            await cache.SetStringAsync($"verificacion:{token}", JsonSerializer.Serialize(guardado), TtlToken, ct);
            return token;
        }
        catch (Exception ex) when (EsFalloDeCache(ex)) { return null; }
    }

    /// <summary>Cada consulta gasta cuota de las APIs: se limita por dirección IP. Si la caché falla, no se bloquea.</summary>
    private async Task<bool> RegistrarIntentoAsync(string identificador, int limite, CancellationToken ct)
    {
        try
        {
            var clave = $"verificacion:intentos:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identificador)))[..16]}";
            var usados = int.TryParse(await cache.GetStringAsync(clave, ct), out var n) ? n : 0;
            if (usados >= limite) return false;
            await cache.SetStringAsync(clave, (usados + 1).ToString(CultureInfo.InvariantCulture), TtlContador, ct);
            return true;
        }
        catch (Exception ex) when (EsFalloDeCache(ex)) { return true; }
    }

    private static string? Texto(JsonElement objeto, string nombre) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static bool EsFalloDeCache(Exception ex) => ex is StackExchange.Redis.RedisException or TimeoutException;

    private sealed record VerificacionGuardada(string Huella, string Estado, string? Fuente, DateTime En);

    private sealed record DatosOficiales(string Nombres, string Apellidos, string? Nacionalidad);

    /// <summary>Lo que respondió la fuente. Oficial solo existe cuando confirmó el documento.</summary>
    private sealed record Consulta(string Estado, string? Fuente, string Mensaje, DatosOficiales? Oficial)
    {
        public static Consulta Fallo(string mensaje) => new(EstadosVerificacion.SinVerificar, null, mensaje, null);
    }

    [GeneratedRegex(@"^\d{8}$")] private static partial Regex Dni();
    [GeneratedRegex(@"^\d{6,12}$")] private static partial Regex Carne();
    [GeneratedRegex(@"^[A-Za-z0-9]{5,15}$")] private static partial Regex Pasaporte();
    [GeneratedRegex(@"[^A-Z]+")] private static partial Regex NoLetras();
}
