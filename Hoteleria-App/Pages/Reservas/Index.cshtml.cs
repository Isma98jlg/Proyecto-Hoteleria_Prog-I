using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hoteleria_App.Data;

namespace Hoteleria_App.Pages.Reservas;

[Authorize]
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
