using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Hoteleria_App.Data;
using Hoteleria_App.Security;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Habitaciones;

/// <summary>
/// PageModel de "Gestión de Habitaciones y Pisos" (módulo canónico 2 · Habitaciones y Pisos).
///
/// Reproduce el diseño Stitch "gesti_n_de_habitaciones_y_pisos_hms_unificado" enlazando los
/// datos reales del modelo (Habitacion, Piso, TipoVip) servidos por <see cref="IHabitacionService"/>.
///
/// Las categorías iniciales son ESTANDAR y VIP, y se pueden crear más. La cinta de KPIs y el
/// filtro de categoría agrupan así:
///   · Estándar  → TipoVipNombre == "ESTANDAR"
///   · VIP/Premium → cualquier otra categoría
/// Las tarjetas individuales muestran SIEMPRE el TipoVip y la tarifa reales de cada
/// habitación (sin cifras hardcodeadas).
/// </summary>
[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class IndexModel : PageModel
{
    private readonly IHabitacionService _habitacionService;
    private readonly HoteleriaDbContext _db;
    private readonly IAuthorizationService _authorization;

    public IndexModel(IHabitacionService habitacionService, HoteleriaDbContext db, IAuthorizationService authorization)
    {
        _habitacionService = habitacionService;
        _db = db;
        _authorization = authorization;
    }

    public bool PuedeAdministrarHabitaciones { get; private set; }
    public bool PuedeAdministrarPisos { get; private set; }

    // ---------------------------------------------------------------
    // Datos calculados para el renderizado
    // ---------------------------------------------------------------
    public List<Habitacion> Todas { get; private set; } = new();
    public List<Habitacion> Habitaciones { get; private set; } = new();
    public List<PisoResumen> Pisos { get; private set; } = new();
    public List<int> CamasDisponibles { get; private set; } = new();

    // ---------------------------------------------------------------
    // Filtros (GET) — espejo exacto de los controles del diseño
    // ---------------------------------------------------------------
    [BindProperty(SupportsGet = true)] public long? Piso { get; set; }
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Categoria { get; set; }
    [BindProperty(SupportsGet = true)] public int? Camas { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }

    // ---------------------------------------------------------------
    // KPIs operativos (cinta colapsable)
    // ---------------------------------------------------------------
    public int TotalHabitaciones => Todas.Count;
    public int Ocupadas => Todas.Count(h => h.Estado == "OCUPADO");
    public int Libres => Todas.Count(h => h.Estado == "LIBRE");
    public int EnMantenimiento => Todas.Count(h => h.Estado == "MANTENIMIENTO");
    public int CategoriaEstandar => Todas.Count(h => h.TipoVipNombre == "ESTANDAR");
    public int CategoriaVip => Todas.Count(h => h.TipoVipNombre != "ESTANDAR");

    public decimal PrecioCamaEstandar =>
        Todas.Where(h => h.TipoVipNombre == "ESTANDAR")
             .Select(h => h.TipoVip?.PrecioCama ?? 0m)
             .FirstOrDefault();

    public decimal PrecioCamaVip =>
        Todas.Where(h => h.TipoVipNombre != "ESTANDAR")
             .Select(h => h.TipoVip?.PrecioCama ?? 0m)
             .DefaultIfEmpty(0m)
             .Max();

    // ---------------------------------------------------------------
    // Helpers de presentación reutilizados en la vista
    // ---------------------------------------------------------------
    public string Codigo(Habitacion h) => CodigoHabitacion.Formatear(h.NroPiso, h.TipoVip?.Sigla ?? 'E', h.HabitacionId);

    public bool EsVip(Habitacion h) =>
        !string.IsNullOrEmpty(h.TipoVipNombre) && h.TipoVipNombre != "ESTANDAR";

    public string EstadoEtiqueta(string estado) => estado switch
    {
        "LIBRE" => "Libre",
        "OCUPADO" => "Ocupada",
        "MANTENIMIENTO" => "Mant.",
        _ => estado
    };

    public decimal PrecioCama(Habitacion h) => h.TipoVip?.PrecioCama ?? 0m;
    public decimal PrecioTotal(Habitacion h) => h.NroCamas * PrecioCama(h);

    public string Dinero(decimal monto) =>
        monto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public decimal Porcentaje(int cantidad) =>
        TotalHabitaciones == 0 ? 0m : Math.Round(cantidad * 100m / TotalHabitaciones, 1);

    public string Pct(int cantidad) =>
        Porcentaje(cantidad).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------
    // Handlers
    // ---------------------------------------------------------------
    public async Task OnGetAsync()
    {
        PuedeAdministrarHabitaciones = (await _authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudHabitaciones)).Succeeded;
        PuedeAdministrarPisos = (await _authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudPisos)).Succeeded;
        Todas = (await _habitacionService.GetAllAsync())
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToList();

        CamasDisponibles = Todas.Select(h => h.NroCamas).Distinct().OrderBy(c => c).ToList();

        IEnumerable<Habitacion> query = Todas;

        if (Piso.HasValue)
            query = query.Where(h => h.NroPiso == Piso.Value);

        if (!string.IsNullOrWhiteSpace(Estado))
        {
            var e = Estado.Trim().ToUpperInvariant();
            query = query.Where(h => string.Equals(h.Estado, e, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(Categoria))
        {
            if (Categoria.Equals("VIP", StringComparison.OrdinalIgnoreCase))
                query = query.Where(h => h.TipoVipNombre != "ESTANDAR");
            else
                query = query.Where(h => h.TipoVipNombre == Categoria);
        }

        if (Camas.HasValue)
            query = query.Where(h => h.NroCamas == Camas.Value);

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var term = Buscar.Trim();
            query = query.Where(h =>
                Codigo(h).Contains(term, StringComparison.OrdinalIgnoreCase)
                || (h.TipoVipNombre?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (h.Piso?.RefPiso?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Habitaciones = query.OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId).ToList();

        // Estructura de pisos (Entidad PISOS) derivada de las habitaciones reales.
        var habitacionesPorPiso = Todas.GroupBy(h => h.NroPiso).ToDictionary(g => g.Key, g => g.ToList());
        Pisos = (await _db.Pisos.AsNoTracking().OrderBy(p => p.NroPiso).ToListAsync())
            .Select(p =>
            {
                var habitaciones = habitacionesPorPiso.GetValueOrDefault(p.NroPiso) ?? new List<Habitacion>();
                return new PisoResumen
                {
                    NroPiso = p.NroPiso,
                    RefPiso = p.RefPiso,
                    Cantidad = habitaciones.Count,
                    Ocupadas = habitaciones.Count(h => h.Estado == "OCUPADO"),
                    EsVipPiso = habitaciones.Any(h => h.TipoVipNombre != "ESTANDAR"),
                };
            })
            .ToList();
    }

    /// <summary>
    /// Exportación CSV real del catálogo (sustituye al alert() de maqueta "exportData()").
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync()
    {
        var todas = (await _habitacionService.GetAllAsync())
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Codigo,Piso,Referencia,Categoria,Camas,Estado,PrecioCama,PrecioTotal");
        foreach (var h in todas)
        {
            sb.AppendLine(string.Join(",",
                Codigo(h),
                h.NroPiso,
                (h.Piso?.RefPiso ?? string.Empty).Replace(',', ' '),
                h.TipoVipNombre,
                h.NroCamas,
                h.Estado,
                PrecioCama(h).ToString("0.00"),
                PrecioTotal(h).ToString("0.00")));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return new FileContentResult(bytes, "text/csv")
        {
            FileDownloadName = "habitaciones-y-pisos.csv"
        };
    }

    // ---------------------------------------------------------------
    // CRUD de la entidad PISOS (front-end)
    // ---------------------------------------------------------------
    [BindProperty] public long? PisoNro { get; set; }
    [BindProperty] public string? PisoRef { get; set; }

    /// <summary>Crea un piso o actualiza su referencia.</summary>
    public async Task<IActionResult> OnPostGuardarPisoAsync()
    {
        if (!(await _authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudPisos)).Succeeded)
            return Forbid();
        if (PisoNro is null || PisoNro < 0 || PisoNro > 99 || string.IsNullOrWhiteSpace(PisoRef) || PisoRef.Trim().Length > 20)
        {
            TempData["MensajeError"] = "Indica un número de piso entre 0 y 99 y una referencia de hasta 20 caracteres.";
            return RedirectToPage();
        }

        var piso = await _db.Pisos.SingleOrDefaultAsync(p => p.NroPiso == PisoNro.Value);
        if (piso is null)
            _db.Pisos.Add(new Piso { NroPiso = PisoNro.Value, RefPiso = PisoRef.Trim() });
        else
            piso.RefPiso = PisoRef.Trim();
        await _db.SaveChangesAsync();
        TempData["MensajeExito"] = piso is null ? "Piso registrado correctamente." : "Referencia del piso actualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarPisoAsync(long nroPiso)
    {
        if (!(await _authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudPisos)).Succeeded)
            return Forbid();

        var piso = await _db.Pisos.SingleOrDefaultAsync(p => p.NroPiso == nroPiso);
        if (piso is null) return NotFound();
        if (await _db.Habitaciones.AnyAsync(h => h.NroPiso == nroPiso))
        {
            TempData["MensajeError"] = "No se puede eliminar un piso con habitaciones.";
            return RedirectToPage();
        }

        _db.Pisos.Remove(piso);
        await _db.SaveChangesAsync();
        TempData["MensajeExito"] = "Piso eliminado.";
        return RedirectToPage();
    }
}

/// <summary>
/// Resumen agregado de un nivel del edificio (para la tabla "Estructura de Pisos").
/// </summary>
public class PisoResumen
{
    public long NroPiso { get; set; }
    public string? RefPiso { get; set; }
    public int Cantidad { get; set; }
    public int Ocupadas { get; set; }
    public bool EsVipPiso { get; set; }

    public decimal OcupacionPct =>
        Cantidad == 0 ? 0m : Math.Round(Ocupadas * 100m / Cantidad, 1);
}
