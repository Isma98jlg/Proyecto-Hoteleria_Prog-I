using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages;

public class IndexModel : PageModel
{
    private readonly IHabitacionService _habitacionService;
    private readonly HoteleriaDbContext _context;

    public IndexModel(IHabitacionService habitacionService, HoteleriaDbContext context)
    {
        _habitacionService = habitacionService;
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
        var habitaciones = await _habitacionService.GetAllAsync();
        TotalHabitaciones = habitaciones.Count;
        HabitacionesLibres = habitaciones.Count(h => h.Estado == "LIBRE");
        HabitacionesOcupadas = habitaciones.Count(h => h.Estado == "OCUPADO");
        HabitacionesMantenimiento = habitaciones.Count(h => h.Estado == "MANTENIMIENTO");

        var reservas = await _context.Reservas.ToListAsync();
        TotalReservas = reservas.Count;
        ReservasPendientes = reservas.Count(r => r.EstadoReserva == "PENDIENTE");
        ReservasConfirmadas = reservas.Count(r => r.EstadoReserva == "CONFIRMADA");
        TotalClientes = await _context.Usuarios.CountAsync();

        HabitacionesPorPiso = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
