using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class CrearModel(IReservaService reservaService, SelectorHabitaciones selector, RelojHotel reloj) : PageModel
{
    [BindProperty]
    public ReservaCreateModel Reserva { get; set; } = new();

    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public DateTime Hoy => reloj.Hoy;

    public async Task OnGetAsync()
    {
        Reserva.InicioReserva = reloj.Hoy.AddDays(1);
        Reserva.TotalNoches = 1;
        await CargarAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var resultado = await reservaService.CrearAsync(Reserva);
            if (resultado.Ok)
            {
                TempData["MensajeExito"] = "Reserva creada correctamente.";
                return RedirectToPage("Index");
            }
            ModelState.AddModelError(string.Empty, resultado.Error!);
        }

        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1, int? personas = null, string? categoria = null, string? q = null) =>
        await selector.BuscarAsync(inicio, noches, personas, categoria, q, HttpContext.RequestAborted) is { } resultado ? new JsonResult(resultado) : BadRequest();

    private async Task CargarAsync() => Usuarios = await reservaService.GetHuespedesAsync();
}
