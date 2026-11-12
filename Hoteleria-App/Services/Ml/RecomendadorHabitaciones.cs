using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace Hoteleria_App.Services.Ml;

public sealed record EstadoRecomendador(bool Activo, DateTime? EntrenadoEn, int Interacciones, int Huespedes, int Habitaciones, string Detalle);

/// <summary>
/// Recomendación con factorización de matrices (ML.NET). Aprende de la matriz huésped × habitación que forman las
/// reservas no canceladas: cada reserva cuenta como «le interesó» (no hay puntuaciones, por eso se usa la variante
/// de una sola clase). Al huésped sin historial se le ofrecen las habitaciones más reservadas.
/// El modelo vive en memoria y se cambia de golpe al reentrenar, así que se puede consultar mientras tanto.
/// </summary>
public sealed class RecomendadorHabitaciones
{
    public const int MinimoInteracciones = 10;
    public const int MinimoHuespedes = 3;

    private sealed class Interaccion
    {
        public string Usuario { get; set; } = "";
        public string Habitacion { get; set; } = "";
        public float Label { get; set; }
    }

    private sealed class Prediccion
    {
        public float Score { get; set; }
    }

    private sealed record Modelo(MLContext Contexto, ITransformer Transformador, HashSet<long> Huespedes, HashSet<long> Habitaciones);

    private volatile Modelo? modelo;
    private volatile IReadOnlyDictionary<long, float> popularidad = new Dictionary<long, float>();
    private volatile EstadoRecomendador estado = new(false, null, 0, 0, 0, "Aún no se ha entrenado.");

    public EstadoRecomendador Estado => estado;

    /// <param name="pares">Una fila por habitación reservada: quién y cuál.</param>
    public void Entrenar(IReadOnlyCollection<(long Usuario, long Habitacion)> pares)
    {
        var maximo = pares.Count == 0 ? 1 : pares.GroupBy(p => p.Habitacion).Max(g => g.Count());
        popularidad = pares.GroupBy(p => p.Habitacion).ToDictionary(g => g.Key, g => (float)g.Count() / maximo);

        var unicos = pares.Distinct().ToList();
        var huespedes = unicos.Select(p => p.Usuario).ToHashSet();
        var habitaciones = unicos.Select(p => p.Habitacion).ToHashSet();
        if (unicos.Count < MinimoInteracciones || huespedes.Count < MinimoHuespedes || habitaciones.Count < 2)
        {
            modelo = null;
            estado = new(false, DateTime.UtcNow, unicos.Count, huespedes.Count, habitaciones.Count,
                $"Faltan datos: hacen falta {MinimoInteracciones} reservas de {MinimoHuespedes} huéspedes. Mientras tanto se usan las más reservadas.");
            return;
        }

        var ml = new MLContext(seed: 7);
        var datos = ml.Data.LoadFromEnumerable(unicos.Select(p => new Interaccion { Usuario = p.Usuario.ToString(), Habitacion = p.Habitacion.ToString(), Label = 1f }));
        var proceso = ml.Transforms.Conversion.MapValueToKey("UsuarioClave", nameof(Interaccion.Usuario))
            .Append(ml.Transforms.Conversion.MapValueToKey("HabitacionClave", nameof(Interaccion.Habitacion)))
            .Append(ml.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UsuarioClave",
                MatrixRowIndexColumnName = "HabitacionClave",
                LabelColumnName = nameof(Interaccion.Label),
                // Una sola clase: solo sabemos lo que reservó, no lo que descartó.
                LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
                Alpha = 0.01,   // peso de las parejas no observadas
                Lambda = 0.025, // regularización
                C = 0.00001,    // valor supuesto para lo no reservado
                ApproximationRank = 8,
                NumberOfIterations = 60,
                Quiet = true
            }));

        modelo = new Modelo(ml, proceso.Fit(datos), huespedes, habitaciones);
        estado = new(true, DateTime.UtcNow, unicos.Count, huespedes.Count, habitaciones.Count,
            "Factorización de matrices de una clase sobre las reservas no canceladas.");
    }

    /// <summary>
    /// Afinidad de 0 a 1 de un huésped con cada habitación. Personal si el modelo lo conoce; si no, la popularidad.
    /// </summary>
    public (IReadOnlyDictionary<long, float> Afinidad, bool Personal) Puntuar(long usuarioId, IReadOnlyCollection<long> habitacionIds)
    {
        var actual = modelo;
        if (actual is null || !actual.Huespedes.Contains(usuarioId))
        {
            var populares = popularidad;
            return (habitacionIds.ToDictionary(id => id, id => populares.GetValueOrDefault(id)), false);
        }

        var conocidas = habitacionIds.Where(actual.Habitaciones.Contains).ToList();
        var filas = actual.Contexto.Data.LoadFromEnumerable(conocidas.Select(id => new Interaccion { Usuario = usuarioId.ToString(), Habitacion = id.ToString() }));
        var puntajes = actual.Contexto.Data.CreateEnumerable<Prediccion>(actual.Transformador.Transform(filas), reuseRowObject: false)
            .Select(p => float.IsFinite(p.Score) ? Math.Clamp(p.Score, 0f, 1f) : 0f)
            .ToList();
        var afinidad = habitacionIds.ToDictionary(id => id, _ => 0f);
        for (var i = 0; i < conocidas.Count; i++) afinidad[conocidas[i]] = puntajes[i];
        return (afinidad, true);
    }
}
