using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages;

public class IndexModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public IndexModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    public int TotalHabitaciones { get; set; }
    public int HabitacionesLibres { get; set; }
    public int HabitacionesOcupadas { get; set; }
    public int HabitacionesMantenimiento { get; set; }
    public int TotalReservas { get; set; }
    public int ReservasPendientes { get; set; }
    public int ReservasConfirmadas { get; set; }
    public int TotalClientes { get; set; }
    public Dictionary<long, List<Habitacion>> HabitacionesPorPiso { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Habitaciones
        var habitaciones = await _context.Habitaciones.ToListAsync();
        TotalHabitaciones = habitaciones.Count;
        HabitacionesLibres = habitaciones.Count(h => h.Estado == "LIBRE");
        HabitacionesOcupadas = habitaciones.Count(h => h.Estado == "OCUPADO");
        HabitacionesMantenimiento = habitaciones.Count(h => h.Estado == "MANTENIMIENTO");

        // Reservas
        var reservas = await _context.Reservas.ToListAsync();
        TotalReservas = reservas.Count;
        ReservasPendientes = reservas.Count(r => r.EstadoReserva == "PENDIENTE");
        ReservasConfirmadas = reservas.Count(r => r.EstadoReserva == "CONFIRMADA");

        // Clientes
        TotalClientes = await _context.Usuarios.CountAsync();

        // Por piso
        HabitacionesPorPiso = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}