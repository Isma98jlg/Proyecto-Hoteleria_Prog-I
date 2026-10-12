using Hoteleria_App.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

public class IndexModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public IndexModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    public int TotalClientes { get; set; }
    public int ConReservas { get; set; }
    public int SinReservas { get; set; }

    public async Task OnGetAsync()
    {
        TotalClientes = await _context.Usuarios.CountAsync();
        ConReservas = await _context.Usuarios.CountAsync(u => _context.Reservas.Any(r => r.UsuarioId == u.UsuarioId));
        SinReservas = TotalClientes - ConReservas;
    }
}