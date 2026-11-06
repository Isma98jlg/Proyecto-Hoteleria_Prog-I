using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class IndexModel(IReservaService reservaService, SelectorHabitaciones selector, RelojHotel reloj) : PageModel
{
    public List<ReservaViewModel> Reservas { get; set; } = new();
    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public IReadOnlyList<string> Estados => EstadosReserva.Todos;
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
        return Page();
    }

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1, int? personas = null, string? categoria = null, string? q = null) =>
        await selector.BuscarAsync(inicio, noches, personas, categoria, q, HttpContext.RequestAborted) is { } resultado ? new JsonResult(resultado) : BadRequest();

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
}
