using Hoteleria_App.Services.Ml;

namespace Hoteleria_App.Services;

/// <summary>
/// Una habitación libre. <c>Coincide</c> indica si cumple los filtros: las demás sirven para mantener lo ya marcado.
/// <c>Destacado</c> es la insignia de ML.NET («Para ti» o «Más reservada»), solo en el portal.
/// </summary>
public sealed record HabitacionSeleccionable(long HabitacionId, string Codigo, string Categoria, string Ubicacion, int Camas, decimal PrecioNoche, bool Coincide, string? Destacado = null);

/// <summary><c>BusquedaId</c> y <c>Orden</c> solo llegan en el portal, cuando la búsqueda se personaliza.</summary>
public sealed record ResultadoSelector(string Motor, IReadOnlyList<string> Categorias, IReadOnlyList<HabitacionSeleccionable> Habitaciones, long? BusquedaId = null, string? Orden = null);

/// <summary>Datos de la vista compartida _SelectorHabitaciones.</summary>
public sealed record SelectorHabitacionesVista(string Campo, string Inicio, string Noches, IEnumerable<long> Seleccionadas);

/// <summary>
/// Selector de habitaciones. Cada pieza tiene un papel:
/// Algolia (o PostgreSQL) filtra lo que encaja con lo pedido, PostgreSQL decide qué está libre y, para el
/// huésped del portal, ML.NET ordena y destaca: el ranking pone primero lo que más probablemente elegirá y el
/// recomendador marca lo que encaja con su historial. Sin modelos, el orden es el de siempre. Cada persona ocupa una cama.
/// </summary>
public sealed class SelectorHabitaciones(
    IReservaService reservas,
    IBuscadorHabitaciones buscador,
    RelojHotel reloj,
    RecomendadorHabitaciones recomendador,
    OrdenadorHabitaciones ordenador,
    HistorialBusquedas historial)
{
    private const int Destacadas = 2;

    /// <param name="huesped">Solo en el portal: personaliza el orden y guarda la búsqueda para el ranking.</param>
    public async Task<ResultadoSelector?> BuscarAsync(DateTime inicio, int noches, int? personas, string? categoria, string? texto,
        CancellationToken ct = default, long? huesped = null)
    {
        if (noches is < 1 or > 365 || personas is < 1) return null;

        var motor = buscador.UsaAlgolia ? "Algolia" : "base de datos";
        var entrada = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified);
        if (entrada < reloj.Hoy) return new(motor, [], []);

        var libres = await reservas.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(noches));
        var catalogo = await buscador.BuscarAsync(texto, personas, categoria, ct);
        var categorias = libres.Select(h => h.TipoVip).Distinct().Order().ToList();

        // El orden del buscador refleja la relevancia; primero lo que coincide.
        var posiciones = catalogo.Select((h, i) => (h.HabitacionId, i)).ToDictionary(p => p.HabitacionId, p => p.i);
        int Relevancia(HabitacionDisponibleViewModel h) => posiciones.GetValueOrDefault(h.HabitacionId, int.MaxValue);

        if (huesped is not { } usuarioId)
        {
            var habitaciones = libres.OrderBy(Relevancia).ThenBy(h => h.NroPiso)
                .Select(h => Seleccionable(h, posiciones.ContainsKey(h.HabitacionId), null)).ToList();
            return new(motor, categorias, habitaciones);
        }

        // Portal: afinidad del recomendador y puntaje del ranking para cada habitación libre.
        var (afinidad, personal) = recomendador.Puntuar(usuarioId, libres.Select(h => h.HabitacionId).ToList());
        var filas = libres.Select(h => OrdenadorHabitaciones.Caracteristicas("actual", false, h.TipoVip, h.NroCamas, h.NroPiso, h.PrecioNoche,
            noches, entrada, reloj.Hoy, afinidad.GetValueOrDefault(h.HabitacionId))).ToList();
        var ranking = ordenador.Puntuar(filas);
        var puntaje = libres.Select((h, i) => (h.HabitacionId, Valor: ranking?[i] ?? afinidad.GetValueOrDefault(h.HabitacionId)))
            .ToDictionary(p => p.HabitacionId, p => p.Valor);

        // Con texto escrito manda la relevancia de Algolia y el modelo desempata; sin texto, ordena el modelo.
        var conTexto = !string.IsNullOrWhiteSpace(texto);
        var ordenadas = libres.OrderBy(h => posiciones.ContainsKey(h.HabitacionId) ? 0 : 1);
        ordenadas = conTexto
            ? ordenadas.ThenBy(Relevancia).ThenByDescending(h => puntaje[h.HabitacionId])
            : ordenadas.ThenByDescending(h => puntaje[h.HabitacionId]).ThenBy(h => h.NroPiso);

        var coincidentes = libres.Where(h => posiciones.ContainsKey(h.HabitacionId)).ToList();
        var etiqueta = personal ? "Para ti" : "Más reservada";
        var destacadas = coincidentes.Where(h => afinidad.GetValueOrDefault(h.HabitacionId) > 0.05f)
            .OrderByDescending(h => afinidad[h.HabitacionId]).Take(personal ? Destacadas : 1)
            .Select(h => h.HabitacionId).ToHashSet();

        var lista = ordenadas.Select(h => Seleccionable(h, posiciones.ContainsKey(h.HabitacionId), destacadas.Contains(h.HabitacionId) ? etiqueta : null)).ToList();
        var busquedaId = await historial.RegistrarAsync(usuarioId, entrada, noches, personas, categoria, conTexto,
            lista.Where(h => h.Coincide).Select(h => h.HabitacionId).ToList(), ct);
        var orden = ordenador.Activo ? "personalizado (ranking ML.NET)" : recomendador.Estado.Activo ? "por afinidad (ML.NET)" : null;
        return new(motor, categorias, lista, busquedaId, orden);
    }

    private static HabitacionSeleccionable Seleccionable(HabitacionDisponibleViewModel h, bool coincide, string? destacado) =>
        new(h.HabitacionId, h.Codigo, h.TipoVip, h.Ubicacion, h.NroCamas, h.PrecioNoche, coincide, destacado);
}
