using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Portal personal: cada persona ve y cancela únicamente sus propias reservas.</summary>
[Authorize]
public class IndexModel(IReservaService reservas) : PageModel
{
    public List<ReservaViewModel> Reservas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Reservas = await reservas.GetAllAsync(usuarioId: User.Id());
    }

    public async Task<IActionResult> OnPostCancelarAsync(long id)
    {
        var resultado = await reservas.CancelarPropiaAsync(id, User.Id() ?? 0);
        TempData[resultado.Ok ? "MensajeExito" : "MensajeError"] = resultado.Ok ? "Reserva cancelada." : resultado.Error;
        return RedirectToPage();
    }
}
