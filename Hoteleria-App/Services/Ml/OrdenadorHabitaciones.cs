using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;

namespace Hoteleria_App.Services.Ml;

public sealed record EstadoRanking(bool Activo, DateTime? EntrenadoEn, int Busquedas, int Filas, double? NdcgModelo, double? NdcgOrdenActual, string Detalle);

/// <summary>
/// Una habitación candidata dentro de una búsqueda, con las características que mira el ranking.
/// Se arma igual al entrenar y al ordenar en vivo (<see cref="OrdenadorHabitaciones.Caracteristicas"/>).
/// </summary>
public sealed class FilaRanking
{
    public string Busqueda { get; set; } = "";
    public float Label { get; set; }
    public string Categoria { get; set; } = "";
    public float Camas { get; set; }
    public float Precio { get; set; }
    public float Piso { get; set; }
    public float Noches { get; set; }
    public float Antelacion { get; set; }
    public float FinDeSemana { get; set; }
    public float Afinidad { get; set; }
}

/// <summary>
/// Ranking con LightGBM (ML.NET). Aprende de la tabla «Busqueda Resultados»: en cada búsqueda que terminó en
/// reserva, qué habitaciones se mostraron y cuál se eligió. Ordena las habitaciones libres de mayor a menor
/// probabilidad de ser elegidas. Algolia sigue filtrando y PostgreSQL decide qué está libre: esto solo ordena.
/// </summary>
public sealed class OrdenadorHabitaciones
{
    public const int MinimoBusquedas = 20;
    private const int Corte = 3; // NDCG@3: ¿la elegida sale entre las tres primeras?

    private static readonly string[] Numericas =
        [nameof(FilaRanking.Camas), nameof(FilaRanking.Precio), nameof(FilaRanking.Piso), nameof(FilaRanking.Noches),
         nameof(FilaRanking.Antelacion), nameof(FilaRanking.FinDeSemana), nameof(FilaRanking.Afinidad)];

    private sealed class Prediccion
    {
        public float Score { get; set; }
    }

    private sealed record Modelo(MLContext Contexto, ITransformer Transformador);

    private volatile Modelo? modelo;
    private volatile EstadoRanking estado = new(false, null, 0, 0, null, null, "Aún no se ha entrenado.");

    public EstadoRanking Estado => estado;
    public bool Activo => modelo is not null;

    public static FilaRanking Caracteristicas(string busqueda, bool elegida, string categoria, int camas, long piso, decimal precioNoche,
        int noches, DateTime inicio, DateTime buscadaEl, float afinidad) => new()
        {
            Busqueda = busqueda,
            Label = elegida ? 1f : 0f,
            Categoria = categoria,
            Camas = camas,
            Precio = (float)precioNoche,
            Piso = piso,
            Noches = noches,
            Antelacion = Math.Max(0, (inicio.Date - buscadaEl.Date).Days),
            FinDeSemana = inicio.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 1f : 0f,
            Afinidad = afinidad
        };

    public void Entrenar(IReadOnlyList<FilaRanking> filas)
    {
        var grupos = filas.GroupBy(f => f.Busqueda).Where(g => g.Any(f => f.Label > 0)).ToList();
        if (grupos.Count < MinimoBusquedas)
        {
            modelo = null;
            estado = new(false, DateTime.UtcNow, grupos.Count, filas.Count, null, null,
                $"Faltan datos: hacen falta {MinimoBusquedas} búsquedas que terminaron en reserva. Mientras tanto se ordena por afinidad.");
            return;
        }

        // Se evalúa con un 20 % de búsquedas que el modelo no vio, y se compara con el orden de antes (por piso).
        var azar = new Random(7);
        var prueba = grupos.Where(_ => azar.NextDouble() < 0.2).Select(g => g.Key).ToHashSet();
        double? ndcgModelo = null, ndcgActual = null;
        if (prueba.Count >= 3 && grupos.Count - prueba.Count >= MinimoBusquedas / 2)
        {
            var (ml, transformador) = Ajustar(grupos.Where(g => !prueba.Contains(g.Key)).SelectMany(g => g).ToList());
            var deprueba = grupos.Where(g => prueba.Contains(g.Key)).ToList();
            ndcgModelo = deprueba.Average(g => Ndcg(Puntuar(ml, transformador, g.ToList()).Zip(g, (s, f) => (s, f.Label))));
            ndcgActual = deprueba.Average(g => Ndcg(g.Select(f => (-f.Piso, f.Label))));
        }

        var (contexto, final) = Ajustar(grupos.SelectMany(g => g).ToList());
        modelo = new Modelo(contexto, final);
        estado = new(true, DateTime.UtcNow, grupos.Count, grupos.Sum(g => g.Count()), ndcgModelo, ndcgActual,
            "LightGBM (LambdaRank) sobre las búsquedas que terminaron en reserva.");
    }

    /// <summary>Puntaje de cada candidata, en el mismo orden. Null si todavía no hay modelo.</summary>
    public float[]? Puntuar(IReadOnlyList<FilaRanking> candidatas)
    {
        var actual = modelo;
        return actual is null || candidatas.Count == 0 ? null : Puntuar(actual.Contexto, actual.Transformador, candidatas);
    }

    private static (MLContext, ITransformer) Ajustar(IReadOnlyList<FilaRanking> filas)
    {
        var ml = new MLContext(seed: 7);
        var proceso = ml.Transforms.Conversion.MapValueToKey("Grupo", nameof(FilaRanking.Busqueda))
            .Append(ml.Transforms.Categorical.OneHotEncoding("CategoriaVector", nameof(FilaRanking.Categoria)))
            .Append(ml.Transforms.Concatenate("Features", [.. Numericas, "CategoriaVector"]))
            .Append(ml.Ranking.Trainers.LightGbm(new LightGbmRankingTrainer.Options
            {
                LabelColumnName = nameof(FilaRanking.Label),
                FeatureColumnName = "Features",
                RowGroupColumnName = "Grupo",
                NumberOfLeaves = 8,
                MinimumExampleCountPerLeaf = 5,
                LearningRate = 0.1,
                NumberOfIterations = 100
            }));
        return (ml, proceso.Fit(ml.Data.LoadFromEnumerable(filas)));
    }

    private static float[] Puntuar(MLContext ml, ITransformer transformador, IReadOnlyList<FilaRanking> filas) =>
        ml.Data.CreateEnumerable<Prediccion>(transformador.Transform(ml.Data.LoadFromEnumerable(filas)), reuseRowObject: false)
            .Select(p => float.IsFinite(p.Score) ? p.Score : float.MinValue)
            .ToArray();

    /// <summary>NDCG@3: 1 si las elegidas salen arriba del todo; baja cuanto más abajo aparecen.</summary>
    private static double Ndcg(IEnumerable<(float Puntaje, float Label)> filas)
    {
        var lista = filas.ToList();
        static double Dcg(IEnumerable<float> etiquetas) => etiquetas.Take(Corte).Select((l, i) => l / Math.Log2(i + 2)).Sum();
        var ideal = Dcg(lista.Select(f => f.Label).OrderByDescending(l => l));
        return ideal == 0 ? 0 : Dcg(lista.OrderByDescending(f => f.Puntaje).Select(f => f.Label)) / ideal;
    }
}
