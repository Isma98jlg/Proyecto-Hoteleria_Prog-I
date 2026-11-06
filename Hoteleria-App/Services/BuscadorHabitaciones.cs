using System.Text.Json.Serialization;
using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services;

/// <summary>Datos públicos de una habitación. Nunca incluye información de huéspedes.</summary>
public sealed class HabitacionCatalogo
{
    [JsonPropertyName("objectID")] public string ObjectId { get; set; } = "";
    [JsonPropertyName("habitacionId")] public long HabitacionId { get; set; }
    [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
    [JsonPropertyName("categoria")] public string Categoria { get; set; } = "";
    [JsonPropertyName("piso")] public long Piso { get; set; }
    [JsonPropertyName("referenciaPiso")] public string ReferenciaPiso { get; set; } = "";
    /// <summary>Permite encontrarla escribiendo, por ejemplo, «piso 2».</summary>
    [JsonPropertyName("ubicacion")] public string Ubicacion { get; set; } = "";
    [JsonPropertyName("camas")] public int Camas { get; set; }
    [JsonPropertyName("precioNoche")] public decimal PrecioNoche { get; set; }
}

public static class UbicacionHabitacion
{
    public static string Formatear(long piso, string? referencia) =>
        string.IsNullOrWhiteSpace(referencia) ? $"Piso {piso}" : $"Piso {piso} · {referencia}";
}

public interface IBuscadorHabitaciones
{
    bool UsaAlgolia { get; }
    /// <param name="camas">Si se indica, solo habitaciones con exactamente ese número de camas.</param>
    /// <param name="categoria">Si se indica, solo habitaciones de esa categoría.</param>
    Task<List<HabitacionCatalogo>> BuscarAsync(string? texto, int? camas = null, string? categoria = null, CancellationToken ct = default);
    /// <summary>Reenvía el catálogo completo al índice. Sin Algolia, no hace nada.</summary>
    Task SincronizarAsync(CancellationToken ct = default);
}

/// <summary>
/// Busca en Algolia si hay credenciales (ALGOLIA_APP_ID y ALGOLIA_ADMIN_KEY) y en PostgreSQL
/// si no las hay o si Algolia falla. La disponibilidad final siempre se comprueba en PostgreSQL.
/// </summary>
public sealed class BuscadorHabitaciones(HoteleriaDbContext db, IConfiguration config, ILogger<BuscadorHabitaciones> logger) : IBuscadorHabitaciones
{
    private readonly string? _appId = config["ALGOLIA_APP_ID"];
    private readonly string? _clave = config["ALGOLIA_ADMIN_KEY"];
    private readonly string _indice = config["ALGOLIA_INDEX"] is { Length: > 0 } i ? i : "hoteleria_habitaciones";

    public bool UsaAlgolia => !string.IsNullOrWhiteSpace(_appId) && !string.IsNullOrWhiteSpace(_clave);

    private SearchClient CrearCliente() => new(new SearchConfig(_appId!, _clave!));

    public async Task<List<HabitacionCatalogo>> BuscarAsync(string? texto, int? camas = null, string? categoria = null, CancellationToken ct = default)
    {
        var filtros = new List<string>();
        if (camas is int cantidad) filtros.Add($"camas={cantidad}");
        if (!string.IsNullOrWhiteSpace(categoria)) filtros.Add($"categoria:\"{categoria.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");

        if (UsaAlgolia)
        {
            try
            {
                var respuesta = await CrearCliente().SearchSingleIndexAsync<HabitacionCatalogo>(
                    _indice,
                    new SearchParams(new SearchParamsObject { Query = texto ?? "", HitsPerPage = 100, Filters = string.Join(" AND ", filtros) }),
                    cancellationToken: ct);
                return respuesta.Hits;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Algolia no respondió; se usa la búsqueda en la base de datos.");
            }
        }

        return await BuscarEnBaseDeDatosAsync(texto, camas, categoria, ct);
    }

    public async Task SincronizarAsync(CancellationToken ct = default)
    {
        if (!UsaAlgolia) return;
        try
        {
            var catalogo = await LeerCatalogo().ToListAsync(ct);
            var documentos = catalogo.Select(Proyectar).ToList();
            var cliente = CrearCliente();
            await cliente.SetSettingsAsync(_indice, new IndexSettings
            {
                SearchableAttributes = ["categoria", "codigo", "ubicacion", "referenciaPiso", "camas", "piso"],
                AttributesForFaceting = ["categoria", "camas", "piso"]
            }, cancellationToken: ct);
            await cliente.ReplaceAllObjectsAsync(_indice, documentos, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo sincronizar el catálogo con Algolia.");
        }
    }

    private async Task<List<HabitacionCatalogo>> BuscarEnBaseDeDatosAsync(string? texto, int? camas, string? categoria, CancellationToken ct)
    {
        var consulta = LeerCatalogo();
        if (camas is int cantidad) consulta = consulta.Where(h => h.NroCamas == cantidad);
        if (!string.IsNullOrWhiteSpace(categoria)) consulta = consulta.Where(h => h.TipoVipNombre == categoria);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            consulta = consulta.Where(h =>
                EF.Functions.ILike(h.TipoVipNombre, patron) ||
                EF.Functions.ILike(h.Piso.RefPiso ?? "", patron) ||
                EF.Functions.ILike("Piso " + h.NroPiso.ToString(), patron) ||
                EF.Functions.ILike(h.HabitacionId.ToString(), patron));
        }
        return (await consulta.ToListAsync(ct)).Select(Proyectar).ToList();
    }

    private IQueryable<Habitacion> LeerCatalogo() => db.Habitaciones.AsNoTracking()
        .Include(h => h.Piso).Include(h => h.TipoVip)
        .OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId);

    private static HabitacionCatalogo Proyectar(Habitacion h) => new()
    {
        ObjectId = h.HabitacionId.ToString(),
        HabitacionId = h.HabitacionId,
        Codigo = CodigoHabitacion.Formatear(h.NroPiso, h.TipoVip.Sigla, h.HabitacionId),
        Categoria = h.TipoVipNombre,
        Piso = h.NroPiso,
        ReferenciaPiso = h.Piso.RefPiso ?? "",
        Ubicacion = UbicacionHabitacion.Formatear(h.NroPiso, h.Piso.RefPiso),
        Camas = h.NroCamas,
        PrecioNoche = h.NroCamas * h.TipoVip.PrecioCama
    };
}
