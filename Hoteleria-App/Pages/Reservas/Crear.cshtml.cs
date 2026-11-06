using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class CrearModel(IReservaService reservaService, RelojHotel reloj) : PageModel
{
    [BindProperty]
    public ReservaCreateModel Reserva { get; set; } = new();

    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public List<string> Estados { get; } = [EstadosReserva.Pendiente, EstadosReserva.Confirmada];
    public List<HabitacionDisponibleViewModel> Habitaciones { get; set; } = new();
    public DateTime Hoy => reloj.Hoy;

    /// <summary>Los parámetros permiten abrir el formulario con la propuesta del copiloto.</summary>
    public async Task OnGetAsync(DateTime? inicio, int? noches, long? habitacion)
    {
        Reserva.InicioReserva = inicio?.Date ?? reloj.Hoy.AddDays(1);
        Reserva.TotalNoches = Math.Clamp(noches ?? 1, 1, 365);
        Reserva.EstadoReserva = EstadosReserva.Pendiente;
        if (habitacion is long id) Reserva.HabitacionIds.Add(id);
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

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1)
    {
        if (noches < 1 || noches > 365) return BadRequest();
        var entrada = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified);
        return new JsonResult(await reservaService.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(noches)));
    }

    private async Task CargarAsync()
    {
        Usuarios = await reservaService.GetHuespedesAsync();
        var inicio = Reserva.InicioReserva == default ? reloj.Hoy.AddDays(1) : DateTime.SpecifyKind(Reserva.InicioReserva.Date, DateTimeKind.Unspecified);
        var noches = Math.Clamp(Reserva.TotalNoches, 1, 365);
        Habitaciones = await reservaService.GetHabitacionesDisponiblesAsync(inicio, inicio.AddDays(noches));
    }
}
