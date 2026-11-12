using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages;

[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(IHabitacionService habitacionService, HoteleriaDbContext context, RelojHotel reloj) : PageModel
{
    /// <summary>Una llegada (check-in pendiente) o una salida (check-out pendiente) de hoy.</summary>
    public sealed record Movimiento(long ReservaId, string Huesped, IReadOnlyList<long> Habitaciones, int Noches);

    private const int MovimientosVisibles = 6;

    public DateTime Hoy { get; private set; }
    public List<Movimiento> Llegadas { get; private set; } = [];
    public List<Movimiento> Salidas { get; private set; } = [];
    public int TotalLlegadas { get; private set; }
    public int TotalSalidas { get; private set; }

    public int TotalHabitaciones { get; set; }
    public int HabitacionesLibres { get; set; }
    public int HabitacionesOcupadas { get; set; }
    public int HabitacionesMantenimiento { get; set; }
    public int TotalReservas { get; set; }
    public int ReservasConfirmadas { get; set; }
    public int TotalClientes { get; set; }
    public Dictionary<long, List<Habitacion>> HabitacionesPorPiso { get; set; } = new();
    public int HabitacionesReservadas { get; set; }
    public Dictionary<long, SituacionHabitacion> Situaciones { get; private set; } = new();
    public SituacionHabitacion Situacion(Habitacion h) => Situaciones[h.HabitacionId];
    public List<AlertaConsistencia> Alertas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var habitaciones = await habitacionService.GetAllAsync();
        TotalHabitaciones = habitaciones.Count;
        // Situación real de hoy: una habitación libre con llegada hoy ya no está disponible.
        Situaciones = await habitacionService.SituacionesHoyAsync(habitaciones);
        HabitacionesLibres = Situaciones.Values.Count(s => s.Clave == SituacionesHabitacion.Libre);
        HabitacionesReservadas = Situaciones.Values.Count(s => s.Clave == SituacionesHabitacion.Reservada);
        HabitacionesOcupadas = Situaciones.Values.Count(s => s.Clave == SituacionesHabitacion.Ocupada);
        HabitacionesMantenimiento = Situaciones.Values.Count(s => s.Clave == SituacionesHabitacion.Mantenimiento);
        Alertas = await habitacionService.RevisarConsistenciaAsync();

        var porEstado = await context.Reservas.AsNoTracking()
            .GroupBy(r => r.EstadoReserva)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Total);
        TotalReservas = porEstado.Values.Sum();
        ReservasConfirmadas = porEstado.GetValueOrDefault(EstadosReserva.Confirmada);
        TotalClientes = await context.Usuarios.CountAsync(u => u.Cargo.Name == CargosSistema.Huesped);

        HabitacionesPorPiso = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Movimientos de hoy: quién llega (confirmadas que empiezan hoy) y quién se va (en curso que terminan hoy).
        Hoy = reloj.Hoy;
        var llegadas = context.Reservas.AsNoTracking().Where(r => r.EstadoReserva == EstadosReserva.Confirmada && r.InicioReserva == Hoy);
        var salidas = context.Reservas.AsNoTracking().Where(r => r.EstadoReserva == EstadosReserva.EnCurso && r.FinReserva == Hoy);
        TotalLlegadas = await llegadas.CountAsync();
        TotalSalidas = await salidas.CountAsync();
        Llegadas = await Movimientos(llegadas);
        Salidas = await Movimientos(salidas);
    }

    private static async Task<List<Movimiento>> Movimientos(IQueryable<Reserva> reservas) =>
        (await reservas.OrderBy(r => r.ReservaId).Take(MovimientosVisibles)
            .Select(r => new { r.ReservaId, Huesped = r.Usuario.Nombres + " " + r.Usuario.Apellidos, Habitaciones = r.ReservaHabitaciones.Select(h => h.HabitacionId).ToList(), r.TotalNoches })
            .ToListAsync())
        .Select(r => new Movimiento(r.ReservaId, r.Huesped, r.Habitaciones.Order().ToList(), r.TotalNoches))
        .ToList();
}
