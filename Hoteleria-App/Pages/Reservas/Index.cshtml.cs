using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hoteleria_App.Data;
using Hoteleria_App.Security;

namespace Hoteleria_App.Pages.Reservas;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class IndexModel : PageModel
{
    private readonly IReservaService _reservaService;
    private readonly HoteleriaDbContext _db;

    public IndexModel(IReservaService reservaService, HoteleriaDbContext db)
    {
        _reservaService = reservaService;
        _db = db;
    }

    public List<ReservaViewModel> Reservas { get; set; } = new();
    public List<UsuarioComboViewModel> Usuarios { get; set; } = new();
    public List<string> Estados { get; set; } = new();
    public List<HabitacionDisponibleViewModel> Habitaciones { get; set; } = new();

    [BindProperty]
    public ReservaCreateModel Reserva { get; set; } = new();

    public bool AbrirModalReserva { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UsuarioId { get; set; }

    public async Task OnGetAsync()
    {
        Reservas = await _reservaService.GetAllAsync(Buscar, Estado, UsuarioId);
        Usuarios = await _reservaService.GetUsuariosActivosAsync();
        Estados = await _reservaService.GetEstadosDisponiblesAsync();
        InicializarFormulario();
        await CargarHabitacionesAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (Reserva.InicioReserva.Date < DateTime.Today)
            ModelState.AddModelError("Reserva.InicioReserva", "La fecha de entrada no puede ser anterior a hoy.");
        if (Reserva.HabitacionIds is null || Reserva.HabitacionIds.Count == 0)
            ModelState.AddModelError("Reserva.HabitacionIds", "Seleccione al menos una habitación.");
        if (!new[] { "PENDIENTE", "CONFIRMADA" }.Contains(Reserva.EstadoReserva))
            ModelState.AddModelError("Reserva.EstadoReserva", "Seleccione un estado inicial válido.");

        if (!ModelState.IsValid || !await _reservaService.CrearAsync(Reserva))
        {
            if (ModelState.IsValid)
                ModelState.AddModelError(string.Empty, "No se pudo crear la reserva. Verifique el usuario y que las habitaciones sigan disponibles.");
            AbrirModalReserva = true;
            await CargarPaginaAsync();
            await CargarHabitacionesAsync();
            return Page();
        }

        TempData["MensajeExito"] = "Reserva creada correctamente.";
        return RedirectToPage("Index", new { Buscar, Estado, UsuarioId });
    }

    public async Task<IActionResult> OnGetDisponiblesAsync(DateTime inicio, int noches = 1)
    {
        if (noches < 1 || noches > 365) return BadRequest();
        var habitaciones = await _reservaService.GetHabitacionesDisponiblesAsync(inicio.Date, inicio.Date.AddDays(noches));
        return new JsonResult(habitaciones);
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

    private async Task CargarPaginaAsync()
    {
        Reservas = await _reservaService.GetAllAsync(Buscar, Estado, UsuarioId);
        Usuarios = await _reservaService.GetUsuariosActivosAsync();
        Estados = await _reservaService.GetEstadosDisponiblesAsync();
    }

    private void InicializarFormulario()
    {
        Reserva.InicioReserva = DateTime.Today.AddDays(1);
        Reserva.TotalNoches = 1;
        Reserva.EstadoReserva = "PENDIENTE";
    }

    private async Task CargarHabitacionesAsync()
    {
        var inicio = Reserva.InicioReserva == default ? DateTime.Today.AddDays(1) : Reserva.InicioReserva.Date;
        var noches = Math.Clamp(Reserva.TotalNoches, 1, 365);
        Habitaciones = await _reservaService.GetHabitacionesDisponiblesAsync(inicio, inicio.AddDays(noches));
    }
}
