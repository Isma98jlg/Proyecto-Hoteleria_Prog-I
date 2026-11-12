using System.Data.Common;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services.Ml;

/// <summary>
/// Guarda las búsquedas del portal y marca la que terminó en reserva. Es lo que aprende el ranking.
/// Registrar nunca debe romper la búsqueda: si falla, se avisa en el log y el huésped sigue igual.
/// </summary>
public sealed class HistorialBusquedas(HoteleriaDbContext db, ILogger<HistorialBusquedas> logger)
{
    /// <summary>Las búsquedas sin reserva no enseñan nada al ranking: se guardan poco tiempo.</summary>
    public static readonly TimeSpan ConservarSinReserva = TimeSpan.FromDays(30);
    public static readonly TimeSpan ConservarConReserva = TimeSpan.FromDays(180);

    public async Task<long?> RegistrarAsync(long usuarioId, DateTime inicio, int noches, int? personas, string? categoria, bool conTexto,
        IReadOnlyList<long> mostradas, CancellationToken ct = default)
    {
        if (mostradas.Count == 0) return null;
        var busqueda = new Busqueda
        {
            UsuarioId = usuarioId,
            CreadaEn = DateTime.UtcNow,
            Inicio = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified),
            Noches = noches,
            Personas = personas,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()[..Math.Min(30, categoria.Trim().Length)],
            ConTexto = conTexto,
            Resultados = mostradas.Select((id, i) => new BusquedaResultado { HabitacionId = id, Posicion = i + 1 }).ToList()
        };
        try
        {
            db.Busquedas.Add(busqueda);
            await db.SaveChangesAsync(ct);
            return busqueda.BusquedaId;
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("No se pudo registrar la búsqueda: {Tipo}", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Marca como elegidas las habitaciones reservadas. Solo vale para una búsqueda propia y sin reservar.</summary>
    public async Task MarcarReservaAsync(long? busquedaId, long usuarioId, IReadOnlyCollection<long> habitacionIds, CancellationToken ct = default)
    {
        if (busquedaId is not { } id || habitacionIds.Count == 0) return;
        try
        {
            var propia = await db.Busquedas
                .Where(b => b.BusquedaId == id && b.UsuarioId == usuarioId && b.ReservadaEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservadaEn, DateTime.UtcNow), ct);
            if (propia == 1)
                await db.BusquedaResultados.Where(r => r.BusquedaId == id && habitacionIds.Contains(r.HabitacionId))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Elegida, true), ct);
        }
        catch (DbException ex)
        {
            logger.LogWarning("No se pudo marcar la búsqueda como reservada: {Tipo}", ex.GetType().Name);
        }
    }

    /// <summary>Borra lo que ya no hace falta conservar.</summary>
    public async Task<int> PurgarAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var sinReserva = ahora - ConservarSinReserva;
        var conReserva = ahora - ConservarConReserva;
        return await db.Busquedas.Where(b => (b.ReservadaEn == null && b.CreadaEn < sinReserva) || b.CreadaEn < conReserva).ExecuteDeleteAsync(ct);
    }
}
