using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Reserva para huéspedes. El titular es siempre quien inició sesión.</summary>
[Authorize(Roles = CargosSistema.Huesped)]
public class ReservarModel(IReservaService reservas, SelectorHabitaciones selector, RelojHotel reloj) : PageModel
{
    [BindProperty] public DateTime? Inicio { get; set; }
    [BindProperty] public int Noches { get; set; } = 1;
    [BindProperty] public List<long> HabitacionIds { get; set; } = [];

    public DateTime Hoy => reloj.Hoy;

    public void OnGet() => Inicio = reloj.Hoy.AddDays(1);

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1, int? personas = null, string? categoria = null, string? q = null) =>
        await selector.BuscarAsync(inicio, noches, personas, categoria, q, HttpContext.RequestAborted) is { } resultado ? new JsonResult(resultado) : BadRequest();

    public async Task<IActionResult> OnPostAsync()
    {
        var resultado = await reservas.CrearAsync(new ReservaCreateModel
        {
            UsuarioId = User.Id() ?? 0,
            InicioReserva = Inicio ?? default,
            TotalNoches = Noches,
            HabitacionIds = HabitacionIds
        });
        if (resultado.Ok)
        {
            TempData["MensajeExito"] = "Su reserva está confirmada.";
            return RedirectToPage("Index");
        }

        ModelState.AddModelError(string.Empty, resultado.Error!);
        Inicio ??= reloj.Hoy.AddDays(1);
        return Page();
    }
}
