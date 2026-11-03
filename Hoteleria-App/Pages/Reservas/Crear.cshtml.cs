using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize]
public class CrearModel : PageModel
{
    private readonly IReservaService _reservaService;

    public CrearModel(IReservaService reservaService)
    {
        _reservaService = reservaService;
    }

    [BindProperty]
    public ReservaCreateModel Reserva { get; set; } = new();

    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public List<string> Estados { get; set; } = new();

    public async Task OnGetAsync()
    {
        Usuarios = await _reservaService.GetUsuariosActivosAsync();
        Estados = await _reservaService.GetEstadosDisponiblesAsync();

        // Valores por defecto
        Reserva.InicioReserva = DateTime.Today.AddDays(1);
        Reserva.TotalNoches = 1;
        Reserva.EstadoReserva = "PENDIENTE";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = await _reservaService.GetEstadosDisponiblesAsync();
            return Page();
        }

        var ok = await _reservaService.CrearAsync(Reserva);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "No se pudo crear la reserva. Verifique que las habitaciones seleccionadas estén disponibles en esas fechas.");
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = await _reservaService.GetEstadosDisponiblesAsync();
            return Page();
        }

        TempData["MensajeExito"] = "Reserva creada correctamente.";
        return RedirectToPage("Index");
    }
}
