namespace Hoteleria_App.Models;

public class Reserva
{
    public long ReservaId { get; set; }
    public long UsuarioId { get; set; }
    public string EstadoReserva { get; set; } = null!;
    public DateTime FechaReservaCreacion { get; set; }
    public DateTime InicioReserva { get; set; }
    public int TotalNoches { get; set; }
    public DateTime FinReserva { get; set; } // Columna generada: inicio_reserva + total_noches

    public virtual Usuario Usuario { get; set; } = null!;
    public virtual ICollection<ReservaHabitacion> ReservaHabitaciones { get; set; } = new List<ReservaHabitacion>();
}

/// <summary>Estados de una reserva y las transiciones permitidas entre ellos.</summary>
public static class EstadosReserva
{
    public const string Pendiente = "PENDIENTE";
    public const string Confirmada = "CONFIRMADA";
    public const string EnCurso = "EN_CURSO";
    public const string Finalizada = "FINALIZADA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlyList<string> Todos = [Pendiente, Confirmada, EnCurso, Finalizada, Cancelada];

    /// <summary>Estados que ocupan la habitación en sus fechas.</summary>
    public static bool Activo(string estado) => estado is Pendiente or Confirmada or EnCurso;

    /// <summary>Solo antes del check-in se pueden cambiar fechas y habitaciones.</summary>
    public static bool Editable(string estado) => estado is Pendiente or Confirmada;

    public static IReadOnlyList<string> Siguientes(string estado) => estado switch
    {
        Pendiente => [Confirmada, Cancelada],
        Confirmada => [EnCurso, Cancelada],
        EnCurso => [Finalizada],
        _ => []
    };

    public static bool PuedeCambiar(string desde, string hacia) => Siguientes(desde).Contains(hacia);

    public static string Etiqueta(string estado) => estado switch
    {
        Pendiente => "Pendiente",
        Confirmada => "Confirmada",
        EnCurso => "En curso",
        Finalizada => "Finalizada",
        Cancelada => "Cancelada",
        _ => estado
    };
}
