using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
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
    public List<HabitacionDisponibleViewModel> Habitaciones { get; set; } = new();

    public async Task OnGetAsync()
    {
        Usuarios = await _reservaService.GetUsuariosActivosAsync();
        Estados = new List<string> { "PENDIENTE", "CONFIRMADA" };

        // Valores por defecto
        Reserva.InicioReserva = DateTime.Today.AddDays(1);
        Reserva.TotalNoches = 1;
        Reserva.EstadoReserva = "PENDIENTE";
        await CargarHabitacionesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Reserva.InicioReserva.Date < DateTime.Today)
            ModelState.AddModelError("Reserva.InicioReserva", "La fecha de entrada no puede ser anterior a hoy.");
        if (Reserva.HabitacionIds is null || Reserva.HabitacionIds.Count == 0)
            ModelState.AddModelError("Reserva.HabitacionIds", "Seleccione al menos una habitación.");
        if (!new[] { "PENDIENTE", "CONFIRMADA" }.Contains(Reserva.EstadoReserva))
            ModelState.AddModelError("Reserva.EstadoReserva", "Seleccione un estado inicial válido.");
        if (!ModelState.IsValid)
        {
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = new List<string> { "PENDIENTE", "CONFIRMADA" };
            await CargarHabitacionesAsync();
            return Page();
        }

        var ok = await _reservaService.CrearAsync(Reserva);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "No se pudo crear la reserva. Verifique que las habitaciones seleccionadas estén disponibles en esas fechas.");
            Usuarios = await _reservaService.GetUsuariosActivosAsync();
            Estados = new List<string> { "PENDIENTE", "CONFIRMADA" };
            await CargarHabitacionesAsync();
            return Page();
        }

        TempData["MensajeExito"] = "Reserva creada correctamente.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1)
    {
        if (noches < 1 || noches > 365) return BadRequest();
        var habitaciones = await _reservaService.GetHabitacionesDisponiblesAsync(inicio.Date, inicio.Date.AddDays(noches));
        return new JsonResult(habitaciones);
    }

    private async Task CargarHabitacionesAsync()
    {
        var inicio = Reserva.InicioReserva == default ? DateTime.Today.AddDays(1) : Reserva.InicioReserva.Date;
        var noches = Math.Clamp(Reserva.TotalNoches, 1, 365);
        Habitaciones = await _reservaService.GetHabitacionesDisponiblesAsync(inicio, inicio.AddDays(noches));
    }
}
