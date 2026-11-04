using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hoteleria_App.Security;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class EditarModel : PageModel
{
    private readonly IReservaService _reservaService;

    public EditarModel(IReservaService reservaService)
    {
        _reservaService = reservaService;
    }

    [BindProperty]
    public ReservaEditModel Reserva { get; set; } = new();

    public ReservaDetalleViewModel? Detalle { get; set; }
    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public List<string> Estados { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Detalle = await _reservaService.GetByIdAsync(id);
        if (Detalle is null) return NotFound();

        Usuarios = await _reservaService.GetUsuariosActivosAsync();
        Estados = await _reservaService.GetEstadosDisponiblesAsync();

        Reserva = new ReservaEditModel
        {
            EstadoReserva = Detalle.EstadoReserva,
            HabitacionIds = Detalle.Habitaciones.Select(h => h.HabitacionId).ToList(),
            PreciosNochePactado = Detalle.Habitaciones.Select(h => h.PrecioNochePactado).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        if (!new[] { "PENDIENTE", "CONFIRMADA", "FINALIZADA", "CANCELADA" }.Contains(Reserva.EstadoReserva))
            ModelState.AddModelError("Reserva.EstadoReserva", "Seleccione un estado válido.");
        if (!ModelState.IsValid)
        {
            Detalle = await _reservaService.GetByIdAsync(id);
            if (Detalle is null) return NotFound();
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = await _reservaService.GetEstadosDisponiblesAsync();
            return Page();
        }

        var ok = await _reservaService.EditarAsync(id, Reserva);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "No se pudo editar la reserva.");
            Detalle = await _reservaService.GetByIdAsync(id);
            if (Detalle is null) return NotFound();
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = await _reservaService.GetEstadosDisponiblesAsync();
            return Page();
        }

        TempData["MensajeExito"] = "Reserva actualizada correctamente.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var ok = await _reservaService.EliminarAsync(id);
        if (!ok)
        {
            TempData["MensajeError"] = "No se pudo eliminar la reserva.";
        }
        else
        {
            TempData["MensajeExito"] = "Reserva eliminada correctamente.";
        }
        return RedirectToPage("Index");
    }
}
