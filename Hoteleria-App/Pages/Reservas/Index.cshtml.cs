using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class IndexModel(IReservaService reservaService, RelojHotel reloj) : PageModel
{
    public List<ReservaViewModel> Reservas { get; set; } = new();
    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public IReadOnlyList<string> Estados => EstadosReserva.Todos;
    public List<HabitacionDisponibleViewModel> Habitaciones { get; set; } = new();
    public DateTime Hoy => reloj.Hoy;

    [BindProperty]
    public ReservaCreateModel Reserva { get; set; } = new();

    public bool AbrirModalReserva { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? UsuarioId { get; set; }

    public async Task OnGetAsync()
    {
        await CargarPaginaAsync();
        Reserva.InicioReserva = reloj.Hoy.AddDays(1);
        Reserva.TotalNoches = 1;
        Reserva.EstadoReserva = EstadosReserva.Pendiente;
        await CargarHabitacionesAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (ModelState.IsValid)
        {
            var resultado = await reservaService.CrearAsync(Reserva);
            if (resultado.Ok)
            {
                TempData["MensajeExito"] = "Reserva creada correctamente.";
                return RedirectToPage("Index", new { Buscar, Estado, UsuarioId });
            }
            ModelState.AddModelError(string.Empty, resultado.Error!);
        }

        AbrirModalReserva = true;
        await CargarPaginaAsync();
        await CargarHabitacionesAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1)
    {
        if (noches < 1 || noches > 365) return BadRequest();
        var entrada = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified);
        return new JsonResult(await reservaService.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(noches)));
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var resultado = await reservaService.EliminarAsync(id);
        TempData[resultado.Ok ? "MensajeExito" : "MensajeError"] = resultado.Ok ? "Reserva eliminada correctamente." : resultado.Error;
        return RedirectToPage("Index");
    }

    private async Task CargarPaginaAsync()
    {
        Reservas = await reservaService.GetAllAsync(Buscar, Estado, UsuarioId);
        Usuarios = await reservaService.GetHuespedesAsync();
    }

    private async Task CargarHabitacionesAsync()
    {
        var inicio = Reserva.InicioReserva == default ? reloj.Hoy.AddDays(1) : DateTime.SpecifyKind(Reserva.InicioReserva.Date, DateTimeKind.Unspecified);
        var noches = Math.Clamp(Reserva.TotalNoches, 1, 365);
        Habitaciones = await reservaService.GetHabitacionesDisponiblesAsync(inicio, inicio.AddDays(noches));
    }
}
