using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class EditarModel(IReservaService reservaService) : PageModel
{
    [BindProperty]
    public ReservaEditModel Reserva { get; set; } = new();

    public ReservaDetalleViewModel? Detalle { get; set; }
    public List<string> Estados { get; set; } = new();
    public List<HabitacionDisponibleViewModel> OtrasHabitaciones { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!await CargarAsync(id)) return NotFound();
        Reserva = new ReservaEditModel
        {
            EstadoReserva = Detalle!.EstadoReserva,
            InicioReserva = Detalle.InicioReserva,
            TotalNoches = Detalle.TotalNoches
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        if (ModelState.IsValid)
        {
            var resultado = await reservaService.EditarAsync(id, Reserva);
            if (resultado.Ok)
            {
                TempData["MensajeExito"] = "Reserva actualizada correctamente.";
                return RedirectToPage("Detalles", new { id });
            }
            ModelState.AddModelError(string.Empty, resultado.Error!);
        }

        return await CargarAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var resultado = await reservaService.EliminarAsync(id);
        TempData[resultado.Ok ? "MensajeExito" : "MensajeError"] = resultado.Ok ? "Reserva eliminada correctamente." : resultado.Error;
        return RedirectToPage("Index");
    }

    private async Task<bool> CargarAsync(long id)
    {
        Detalle = await reservaService.GetByIdAsync(id);
        if (Detalle is null) return false;

        Estados = [Detalle.EstadoReserva, .. EstadosReserva.Siguientes(Detalle.EstadoReserva)];
        if (EstadosReserva.Editable(Detalle.EstadoReserva))
        {
            var asignadas = Detalle.Habitaciones.Select(h => h.HabitacionId).ToHashSet();
            OtrasHabitaciones = (await reservaService.GetHabitacionesDisponiblesAsync(Detalle.InicioReserva, Detalle.FinReserva, id))
                .Where(h => !asignadas.Contains(h.HabitacionId))
                .ToList();
        }
        return true;
    }
}
