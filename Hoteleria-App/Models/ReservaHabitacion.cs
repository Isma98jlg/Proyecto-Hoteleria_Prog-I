namespace Hoteleria_App.Models;

public class ReservaHabitacion
{
    public long ReservaId { get; set; }
    public long HabitacionId { get; set; }
    public decimal PrecioNochePactado { get; set; }

    // Copia de las fechas y del estado de la reserva. Permite que PostgreSQL impida
    // solapes con una restricción de exclusión, incluso con peticiones simultáneas.
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public bool Activa { get; set; }

    public virtual Reserva Reserva { get; set; } = null!;
    public virtual Habitacion Habitacion { get; set; } = null!;
}
