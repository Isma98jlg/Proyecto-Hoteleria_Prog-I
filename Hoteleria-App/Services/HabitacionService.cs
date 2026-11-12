using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services;

/// <summary>
/// Situación real de una habitación hoy. El estado guardado (Libre, Ocupado, Mantenimiento) solo cambia con el
/// check-in, el check-out o el personal; la situación además mira las reservas: una habitación libre con una
/// reserva que cubre hoy está «Reservada», aunque todavía no se haya hecho el check-in.
/// </summary>
public sealed record SituacionHabitacion(string Clave, string Etiqueta, string? Detalle, long? ReservaId, bool Inconsistente);

public static class SituacionesHabitacion
{
    public const string Libre = "LIBRE";
    public const string Reservada = "RESERVADA";
    public const string Ocupada = "OCUPADO";
    public const string Mantenimiento = "MANTENIMIENTO";

    /// <summary>Orden en filtros y leyendas.</summary>
    public static readonly IReadOnlyList<(string Clave, string Etiqueta)> Todas =
        [(Libre, "Libre"), (Reservada, "Reservada hoy"), (Ocupada, "Ocupada"), (Mantenimiento, "Mantenimiento")];
}

/// <summary>Algo que no cuadra entre reservas y habitaciones y que el personal debe revisar.</summary>
public sealed record AlertaConsistencia(string Texto, string Pagina, long Id);

public interface IHabitacionService
{
    Task<List<Habitacion>> GetAllAsync();
    Task<Habitacion?> GetByIdAsync(long id);
    Task<List<Habitacion>> GetByEstadoAsync(string estado);
    Task<List<Habitacion>> GetByPisoAsync(long nroPiso);
    Task<List<Habitacion>> GetByTipoVipAsync(string tipoVip);
    Task<Dictionary<long, SituacionHabitacion>> SituacionesHoyAsync(IEnumerable<Habitacion> habitaciones);
    Task<List<AlertaConsistencia>> RevisarConsistenciaAsync();
}

public class HabitacionService : IHabitacionService
{
    private readonly HoteleriaDbContext _context;
    private readonly RelojHotel _reloj;

    public HabitacionService(HoteleriaDbContext context, RelojHotel reloj)
    {
        _context = context;
        _reloj = reloj;
    }

    /// <summary>
    /// Situación de hoy de cada habitación, con una sola consulta de las reservas activas que la afectan.
    /// Prioridad: estancia en curso, ocupada sin estancia (inconsistente), reservada hoy, mantenimiento, libre.
    /// </summary>
    public async Task<Dictionary<long, SituacionHabitacion>> SituacionesHoyAsync(IEnumerable<Habitacion> habitaciones)
    {
        var hoy = _reloj.Hoy;
        var lineas = await _context.ReservaHabitaciones.AsNoTracking()
            .Where(rh => rh.Activa && (rh.Fin > hoy || rh.Reserva.EstadoReserva == EstadosReserva.EnCurso))
            .Select(rh => new { rh.HabitacionId, rh.ReservaId, rh.Inicio, rh.Fin, Estado = rh.Reserva.EstadoReserva })
            .ToListAsync();
        var porHabitacion = lineas.ToLookup(l => l.HabitacionId);

        var situaciones = new Dictionary<long, SituacionHabitacion>();
        foreach (var h in habitaciones)
        {
            var suyas = porHabitacion[h.HabitacionId].ToList();
            var enCurso = suyas.FirstOrDefault(l => l.Estado == EstadosReserva.EnCurso);
            var hoyReservada = suyas.Where(l => l.Estado != EstadosReserva.EnCurso && l.Inicio <= hoy && l.Fin > hoy).OrderBy(l => l.Inicio).FirstOrDefault();
            var proxima = suyas.Where(l => l.Estado != EstadosReserva.EnCurso && l.Inicio > hoy).OrderBy(l => l.Inicio).FirstOrDefault();

            situaciones[h.HabitacionId] =
                enCurso is not null
                    ? new(SituacionesHabitacion.Ocupada, "Ocupada",
                        enCurso.Fin <= hoy ? $"Salida pendiente desde el {enCurso.Fin:dd/MM}" : $"Hasta el {enCurso.Fin:dd/MM}",
                        enCurso.ReservaId, h.Estado != EstadosHabitacion.Ocupado)
                : h.Estado == EstadosHabitacion.Ocupado
                    ? new(SituacionesHabitacion.Ocupada, "Ocupada", "Sin estancia en curso: revise", null, true)
                : hoyReservada is not null
                    ? new(SituacionesHabitacion.Reservada, "Reservada",
                        hoyReservada.Inicio == hoy ? "Llega hoy · check-in pendiente" : $"Check-in pendiente desde el {hoyReservada.Inicio:dd/MM}",
                        hoyReservada.ReservaId, h.Estado == EstadosHabitacion.Mantenimiento)
                : h.Estado == EstadosHabitacion.Mantenimiento
                    ? new(SituacionesHabitacion.Mantenimiento, "Mantenimiento", proxima is null ? null : $"Próxima llegada el {proxima.Inicio:dd/MM}", proxima?.ReservaId, false)
                : new(SituacionesHabitacion.Libre, "Libre", proxima is null ? null : $"Próxima llegada el {proxima.Inicio:dd/MM}", proxima?.ReservaId, false);
        }
        return situaciones;
    }

    /// <summary>
    /// Revisión de consistencia entre reservas y habitaciones. No corrige nada sola: el estado de una habitación
    /// solo lo cambian el check-in y el check-out, así que se avisa al personal con el enlace para resolverlo.
    /// </summary>
    public async Task<List<AlertaConsistencia>> RevisarConsistenciaAsync()
    {
        var hoy = _reloj.Hoy;
        var alertas = new List<AlertaConsistencia>();
        var habitaciones = await _context.Habitaciones.AsNoTracking().ToListAsync();
        foreach (var (id, s) in await SituacionesHoyAsync(habitaciones))
        {
            if (!s.Inconsistente) continue;
            var guardado = habitaciones.First(h => h.HabitacionId == id).Estado;
            alertas.Add(s.Clave switch
            {
                SituacionesHabitacion.Ocupada when s.ReservaId is null =>
                    new($"La habitación {id} figura ocupada, pero no tiene una estancia en curso.", "/Habitaciones/Detalles", id),
                SituacionesHabitacion.Ocupada =>
                    new($"La reserva #{s.ReservaId} está en curso, pero la habitación {id} figura {(guardado == EstadosHabitacion.Mantenimiento ? "en mantenimiento" : "libre")}.", "/Reservas/Detalles", s.ReservaId!.Value),
                _ => new($"La habitación {id} está en mantenimiento y tiene una llegada hoy (reserva #{s.ReservaId}).", "/Reservas/Detalles", s.ReservaId!.Value)
            });
        }

        // Estancias que debieron terminar y llegadas que nunca se registraron.
        var vencidas = await _context.Reservas.AsNoTracking()
            .Where(r => (r.EstadoReserva == EstadosReserva.EnCurso && r.FinReserva < hoy)
                     || ((r.EstadoReserva == EstadosReserva.Confirmada || r.EstadoReserva == EstadosReserva.Pendiente) && r.FinReserva <= hoy))
            .OrderBy(r => r.FinReserva)
            .Select(r => new { r.ReservaId, r.EstadoReserva, r.InicioReserva, r.FinReserva })
            .ToListAsync();
        alertas.AddRange(vencidas.Select(r => r.EstadoReserva == EstadosReserva.EnCurso
            ? new AlertaConsistencia($"Reserva #{r.ReservaId}: el check-out venció el {r.FinReserva:dd/MM/yyyy}.", "/Reservas/Detalles", r.ReservaId)
            : new AlertaConsistencia($"Reserva #{r.ReservaId}: terminó el {r.FinReserva:dd/MM/yyyy} sin check-in. Cancélela si el huésped no llegó.", "/Reservas/Detalles", r.ReservaId)));

        // Las líneas de habitación deben copiar las fechas y la vigencia de su reserva.
        var desalineadas = await _context.ReservaHabitaciones.AsNoTracking()
            .Where(rh => rh.Inicio != rh.Reserva.InicioReserva || rh.Fin != rh.Reserva.FinReserva
                || rh.Activa != (rh.Reserva.EstadoReserva == EstadosReserva.Pendiente || rh.Reserva.EstadoReserva == EstadosReserva.Confirmada || rh.Reserva.EstadoReserva == EstadosReserva.EnCurso))
            .Select(rh => new { rh.ReservaId, rh.HabitacionId })
            .ToListAsync();
        alertas.AddRange(desalineadas.Select(d => new AlertaConsistencia(
            $"Reserva #{d.ReservaId}: la habitación {d.HabitacionId} no coincide con las fechas o el estado de la reserva.", "/Reservas/Detalles", d.ReservaId)));
        return alertas;
    }

    public async Task<List<Habitacion>> GetAllAsync()
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<Habitacion?> GetByIdAsync(long id)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Include(h => h.ReservaHabitaciones).ThenInclude(rh => rh.Reserva).ThenInclude(r => r.Usuario)
            .FirstOrDefaultAsync(h => h.HabitacionId == id);
    }

    public async Task<List<Habitacion>> GetByEstadoAsync(string estado)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.Estado == estado)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<List<Habitacion>> GetByPisoAsync(long nroPiso)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.NroPiso == nroPiso)
            .OrderBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<List<Habitacion>> GetByTipoVipAsync(string tipoVip)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.TipoVipNombre == tipoVip)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }
}
