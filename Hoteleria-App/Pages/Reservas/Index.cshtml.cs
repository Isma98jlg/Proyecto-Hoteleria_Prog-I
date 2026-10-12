using Hoteleria_App.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Reservas;

public class IndexModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public IndexModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    public int ReservasPendientes { get; set; }
    public int ReservasConfirmadas { get; set; }
    public int ReservasFinalizadas { get; set; }

    public async Task OnGetAsync()
    {
        ReservasPendientes = await _context.Reservas.CountAsync(r => r.EstadoReserva == "PENDIENTE");
        ReservasConfirmadas = await _context.Reservas.CountAsync(r => r.EstadoReserva == "CONFIRMADA");
        ReservasFinalizadas = await _context.Reservas.CountAsync(r => r.EstadoReserva == "FINALIZADA");
    }
}