using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services.Ml;

/// <summary>
/// Entrena los dos modelos de ML.NET al arrancar y cada 6 horas, o cuando el administrador lo pide.
/// Primero el recomendador, porque su afinidad es una de las características del ranking.
/// En los logs solo quedan cantidades: nunca huéspedes ni habitaciones concretas.
/// </summary>
public sealed class EntrenamientoModelos(
    IServiceScopeFactory scopes,
    RecomendadorHabitaciones recomendador,
    OrdenadorHabitaciones ordenador,
    ILogger<EntrenamientoModelos> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim turno = new(1, 1);

    public string? UltimoError { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            // Deja terminar el arranque (migraciones y datos iniciales) antes del primer entrenamiento.
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            using var temporizador = new PeriodicTimer(Intervalo);
            do { await EntrenarAsync(ct); } while (await temporizador.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    public async Task EntrenarAsync(CancellationToken ct = default)
    {
        await turno.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HoteleriaDbContext>();
            var purgadas = await scope.ServiceProvider.GetRequiredService<HistorialBusquedas>().PurgarAsync(ct);

            var pares = await db.ReservaHabitaciones.AsNoTracking()
                .Where(rh => rh.Reserva.EstadoReserva != EstadosReserva.Cancelada)
                .Select(rh => new { rh.Reserva.UsuarioId, rh.HabitacionId })
                .ToListAsync(ct);
            recomendador.Entrenar(pares.Select(p => (p.UsuarioId, p.HabitacionId)).ToList());

            var resultados = await db.BusquedaResultados.AsNoTracking()
                .Where(r => r.Busqueda.ReservadaEn != null)
                .Select(r => new
                {
                    r.BusquedaId, r.Busqueda.UsuarioId, r.Busqueda.CreadaEn, r.Busqueda.Inicio, r.Busqueda.Noches,
                    r.HabitacionId, r.Elegida, r.Habitacion.NroCamas, r.Habitacion.NroPiso, r.Habitacion.TipoVipNombre, r.Habitacion.TipoVip.PrecioCama
                })
                .ToListAsync(ct);

            var filas = new List<FilaRanking>(resultados.Count);
            foreach (var busqueda in resultados.GroupBy(r => r.BusquedaId))
            {
                var primera = busqueda.First();
                var (afinidad, _) = recomendador.Puntuar(primera.UsuarioId, busqueda.Select(r => r.HabitacionId).ToList());
                filas.AddRange(busqueda.Select(r => OrdenadorHabitaciones.Caracteristicas(
                    r.BusquedaId.ToString(), r.Elegida, r.TipoVipNombre, r.NroCamas, r.NroPiso, r.NroCamas * r.PrecioCama,
                    r.Noches, r.Inicio, r.CreadaEn, afinidad.GetValueOrDefault(r.HabitacionId))));
            }
            ordenador.Entrenar(filas);

            UltimoError = null;
            logger.LogInformation("Modelos entrenados: {Reservas} habitaciones reservadas, {Busquedas} búsquedas con reserva, {Purgadas} búsquedas antiguas borradas.",
                pares.Count, ordenador.Estado.Busquedas, purgadas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sin modelo, el selector sigue ordenando como antes: un fallo aquí nunca bloquea las reservas.
            UltimoError = ex.GetType().Name;
            logger.LogWarning(ex, "No se pudieron entrenar los modelos de recomendación.");
        }
        finally
        {
            turno.Release();
        }
    }
}
