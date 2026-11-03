using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize]
public class DetallesModel : PageModel
{
    private readonly IReservaService _reservaService;

    public DetallesModel(IReservaService reservaService)
    {
        _reservaService = reservaService;
    }

    public ReservaDetalleViewModel? Reserva { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Reserva = await _reservaService.GetByIdAsync(id);
        if (Reserva is null)
        {
            return NotFound();
        }
        return Page();
    }
}
