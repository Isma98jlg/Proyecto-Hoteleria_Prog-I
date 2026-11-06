using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Detalle de una reserva propia, con los datos de las habitaciones elegidas.</summary>
[Authorize]
public class DetalleModel(IReservaService reservas) : PageModel
{
    public ReservaDetalleViewModel Reserva { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var reserva = await reservas.GetByIdAsync(id);
        if (reserva is null || reserva.UsuarioId != User.Id()) return NotFound();
        Reserva = reserva;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelarAsync(long id)
    {
        var resultado = await reservas.CancelarPropiaAsync(id, User.Id() ?? 0);
        TempData[resultado.Ok ? "MensajeExito" : "MensajeError"] = resultado.Ok ? "Reserva cancelada." : resultado.Error;
        return RedirectToPage("Index");
    }
}
