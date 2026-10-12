using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages;

public class IndexModel : PageModel
{
    private readonly IHabitacionService _habitacionService;
    private readonly HoteleriaDbContext? _context; // null cuando DB_ENABLED=false

    public IndexModel(IHabitacionService habitacionService, HoteleriaDbContext? context = null)
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
        // Habitaciones (siempre disponible via service)
        var habitaciones = await _habitacionService.GetAllAsync();
        TotalHabitaciones = habitaciones.Count;
        HabitacionesLibres = habitaciones.Count(h => h.Estado == "LIBRE");
        HabitacionesOcupadas = habitaciones.Count(h => h.Estado == "OCUPADO");
        HabitacionesMantenimiento = habitaciones.Count(h => h.Estado == "MANTENIMIENTO");

        // Reservas y Clientes (solo si hay BD real)
        if (_context != null)
        {
            var reservas = await _context.Reservas.ToListAsync();
            TotalReservas = reservas.Count;
            ReservasPendientes = reservas.Count(r => r.EstadoReserva == "PENDIENTE");
            ReservasConfirmadas = reservas.Count(r => r.EstadoReserva == "CONFIRMADA");
            TotalClientes = await _context.Usuarios.CountAsync();
        }
        else
        {
            // Mock data para modo sin BD
            TotalReservas = 0;
            ReservasPendientes = 0;
            ReservasConfirmadas = 0;
            TotalClientes = 1; // admin
        }

        // Por piso
        HabitacionesPorPiso = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}