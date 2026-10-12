namespace Hoteleria_App.Models;

public class Reserva
{
    public long ReservaId { get; set; }
    public long UsuarioId { get; set; }
    public string EstadoReserva { get; set; } = null!;
    public DateTime FechaReservaCreacion { get; set; }
    public DateTime InicioReserva { get; set; }
    public int TotalNoches { get; set; }
    public DateTime FinReserva { get; set; } // Computed column in DB: DATEADD(day, TotalNoches, InicioReserva)

    public virtual Usuario Usuario { get; set; } = null!;
    public virtual ICollection<ReservaHabitacion> ReservaHabitaciones { get; set; } = new List<ReservaHabitacion>();
}