using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>
/// Búsqueda y reserva para huéspedes. Algolia ordena el catálogo por relevancia y PostgreSQL
/// decide la disponibilidad. El titular es siempre quien inició sesión.
/// </summary>
[Authorize(Roles = CargosSistema.Huesped)]
public class ReservarModel(IReservaService reservas, IBuscadorHabitaciones buscador, RelojHotel reloj) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Inicio { get; set; }
    [BindProperty(SupportsGet = true)] public int Noches { get; set; } = 1;
    [BindProperty] public List<long> HabitacionIds { get; set; } = [];

    public List<HabitacionDisponibleViewModel> Resultados { get; private set; } = [];
    public bool UsaAlgolia => buscador.UsaAlgolia;
    public DateTime Hoy => reloj.Hoy;

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var resultado = await reservas.CrearAsync(new ReservaCreateModel
        {
            UsuarioId = User.Id() ?? 0,
            InicioReserva = Inicio ?? default,
            TotalNoches = Noches,
            EstadoReserva = EstadosReserva.Pendiente,
            HabitacionIds = HabitacionIds
        });
        if (resultado.Ok)
        {
            TempData["MensajeExito"] = "Reserva registrada. El hotel la confirmará en breve.";
            return RedirectToPage("Index");
        }

        ModelState.AddModelError(string.Empty, resultado.Error!);
        await CargarAsync();
        return Page();
    }

    private async Task CargarAsync()
    {
        Inicio ??= reloj.Hoy.AddDays(1);
        Noches = Math.Clamp(Noches, 1, 365);
        var entrada = DateTime.SpecifyKind(Inicio.Value.Date, DateTimeKind.Unspecified);
        if (entrada < reloj.Hoy) return;

        var disponibles = (await reservas.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(Noches)))
            .ToDictionary(h => h.HabitacionId);
        var catalogo = await buscador.BuscarAsync(Q);
        Resultados = catalogo
            .Where(c => disponibles.ContainsKey(c.HabitacionId))
            .Select(c => disponibles[c.HabitacionId])
            .ToList();
    }
}
