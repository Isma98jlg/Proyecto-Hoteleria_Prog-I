using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages;

[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(IHabitacionService habitacionService, HoteleriaDbContext context) : PageModel
{
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
        var habitaciones = await habitacionService.GetAllAsync();
        TotalHabitaciones = habitaciones.Count;
        HabitacionesLibres = habitaciones.Count(h => h.Estado == EstadosHabitacion.Libre);
        HabitacionesOcupadas = habitaciones.Count(h => h.Estado == EstadosHabitacion.Ocupado);
        HabitacionesMantenimiento = habitaciones.Count(h => h.Estado == EstadosHabitacion.Mantenimiento);

        var porEstado = await context.Reservas.AsNoTracking()
            .GroupBy(r => r.EstadoReserva)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Total);
        TotalReservas = porEstado.Values.Sum();
        ReservasPendientes = porEstado.GetValueOrDefault(EstadosReserva.Pendiente);
        ReservasConfirmadas = porEstado.GetValueOrDefault(EstadosReserva.Confirmada);
        TotalClientes = await context.Usuarios.CountAsync(u => u.Cargo.Name == CargosSistema.Huesped);

        HabitacionesPorPiso = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
