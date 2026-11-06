namespace Hoteleria_App.Services;

/// <summary>Una habitación libre. <c>Coincide</c> indica si cumple los filtros: las demás sirven para mantener lo ya marcado.</summary>
public sealed record HabitacionSeleccionable(long HabitacionId, string Codigo, string Categoria, string Ubicacion, int Camas, decimal PrecioNoche, bool Coincide);

public sealed record ResultadoSelector(string Motor, IReadOnlyList<string> Categorias, IReadOnlyList<HabitacionSeleccionable> Habitaciones);

/// <summary>Datos de la vista compartida _SelectorHabitaciones.</summary>
public sealed record SelectorHabitacionesVista(string Campo, string Inicio, string Noches, IEnumerable<long> Seleccionadas);

/// <summary>
/// Elegir habitación al reservar. PostgreSQL decide cuáles están libres, y Algolia (o PostgreSQL, si
/// no está disponible) aplica los filtros. Cada persona ocupa una cama.
/// </summary>
public sealed class SelectorHabitaciones(IReservaService reservas, IBuscadorHabitaciones buscador, RelojHotel reloj)
{
    public async Task<ResultadoSelector?> BuscarAsync(DateTime inicio, int noches, int? personas, string? categoria, string? texto, CancellationToken ct = default)
    {
        if (noches is < 1 or > 365 || personas is < 1) return null;

        var motor = buscador.UsaAlgolia ? "Algolia" : "base de datos";
        var entrada = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified);
        if (entrada < reloj.Hoy) return new(motor, [], []);

        var libres = await reservas.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(noches));
        var catalogo = await buscador.BuscarAsync(texto, personas, categoria, ct);

        // El orden del buscador refleja la relevancia; primero lo que coincide.
        var posiciones = catalogo.Select((h, i) => (h.HabitacionId, i)).ToDictionary(p => p.HabitacionId, p => p.i);
        var habitaciones = libres
            .OrderBy(h => posiciones.GetValueOrDefault(h.HabitacionId, int.MaxValue))
            .ThenBy(h => h.NroPiso)
            .Select(h => new HabitacionSeleccionable(h.HabitacionId, h.Codigo, h.TipoVip, h.Ubicacion, h.NroCamas, h.PrecioNoche, posiciones.ContainsKey(h.HabitacionId)))
            .ToList();

        return new(motor, libres.Select(h => h.TipoVip).Distinct().Order().ToList(), habitaciones);
    }
}
