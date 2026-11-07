using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Competencia;

/// <summary>Compara las tarifas propias con las del mercado. Solo informa: decide el administrador.</summary>
[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(CompetenciaService competencia, HoteleriaDbContext db, RelojHotel reloj) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Ciudad { get; set; } = "Arequipa";
    [BindProperty(SupportsGet = true)] public DateTime? Entrada { get; set; }
    [BindProperty(SupportsGet = true)] public int Noches { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Adultos { get; set; } = 2;

    public bool Disponible => competencia.Disponible;
    public int LimiteMensual => competencia.LimiteMensual;
    public int BusquedasDelMes { get; private set; }
    public DateTime Hoy => reloj.Hoy;
    public ConsultaGuardada? Guardada { get; private set; }
    public List<Competidor> Competidores { get; private set; } = [];
    public List<ComparacionCategoria> Comparacion { get; private set; } = [];
    public List<PuntoHistorial> Historial { get; private set; } = [];
    public List<HotelMercado> Ordenados { get; private set; } = [];
    public decimal? MediaCompetidores { get; private set; }
    public decimal? MedianaMercado { get; private set; }
    public int CompetidoresConPrecio { get; private set; }

    public bool EsSeguido(string token) => Competidores.Any(c => c.PropertyToken == token);

    public async Task OnGetAsync()
    {
        Entrada ??= reloj.Hoy.AddDays(14);
        await CargarAsync();
    }

    /// <summary>Lo único que puede gastar una búsqueda de SerpApi.</summary>
    public async Task<IActionResult> OnPostConsultarAsync()
    {
        var resultado = await competencia.ConsultarAsync(Ciudad, Entrada ?? reloj.Hoy.AddDays(14), Noches, Adultos, HttpContext.RequestAborted);
        if (resultado.Error is not null) TempData["MensajeError"] = resultado.Error;
        else TempData["MensajeExito"] = resultado.Reutilizada
            ? "Se reutilizó una consulta reciente, sin gastar búsquedas."
            : "Precios actualizados.";
        return Volver();
    }

    /// <summary>El nombre sale de la consulta guardada, no de lo que envíe el navegador.</summary>
    public async Task<IActionResult> OnPostSeguirAsync(string? token)
    {
        var guardada = await competencia.UltimaAsync(Ciudad, Entrada ?? reloj.Hoy, Noches, Adultos, HttpContext.RequestAborted);
        var hotel = guardada?.Hoteles.FirstOrDefault(h => h.Token == token);
        if (hotel is null)
        {
            TempData["MensajeError"] = "Ese hotel ya no aparece en la consulta. Actualice los precios e inténtelo de nuevo.";
            return Volver();
        }

        if (!await db.Competidores.AnyAsync(c => c.PropertyToken == hotel.Token))
        {
            db.Competidores.Add(new Competidor { PropertyToken = hotel.Token, Nombre = hotel.Nombre, Ciudad = Ciudad.Trim(), CreadoEn = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        return Volver();
    }

    public async Task<IActionResult> OnPostDejarAsync(string? token)
    {
        await db.Competidores.Where(c => c.PropertyToken == token).ExecuteDeleteAsync();
        return Volver();
    }

    private RedirectToPageResult Volver() =>
        RedirectToPage(new { Ciudad, Entrada = (Entrada ?? reloj.Hoy.AddDays(14)).ToString("yyyy-MM-dd"), Noches, Adultos });

    private async Task CargarAsync()
    {
        var ct = HttpContext.RequestAborted;
        BusquedasDelMes = await competencia.BusquedasDelMesAsync(ct);
        var nombre = (Ciudad ?? "").Trim();
        Competidores = await db.Competidores.AsNoTracking().Where(c => c.Ciudad == nombre).OrderBy(c => c.Nombre).ToListAsync(ct);
        var seguidos = Competidores.Select(c => c.PropertyToken).ToHashSet();
        Historial = await competencia.HistorialAsync(nombre, seguidos, ct: ct);

        if (competencia.Validar(nombre, Entrada!.Value.Date < reloj.Hoy ? reloj.Hoy : Entrada.Value, Noches, Adultos) is not null) return;
        Guardada = await competencia.UltimaAsync(nombre, Entrada.Value, Noches, Adultos, ct);
        if (Guardada is null) return;

        var hoteles = Guardada.Hoteles;
        Ordenados = hoteles.OrderByDescending(h => seguidos.Contains(h.Token)).ThenBy(h => h.PrecioNoche ?? decimal.MaxValue).ToList();
        Comparacion = await competencia.CompararAsync(hoteles, seguidos, Adultos, ct);
        var precios = hoteles.Where(h => h.PrecioNoche is not null && seguidos.Contains(h.Token)).Select(h => h.PrecioNoche!.Value).ToList();
        CompetidoresConPrecio = precios.Count;
        MediaCompetidores = precios.Count > 0 ? Math.Round(precios.Average(), 2) : null;
        MedianaMercado = CompetenciaService.Mediana(hoteles.Where(h => h.PrecioNoche is not null).Select(h => h.PrecioNoche!.Value).ToList());
    }
}
