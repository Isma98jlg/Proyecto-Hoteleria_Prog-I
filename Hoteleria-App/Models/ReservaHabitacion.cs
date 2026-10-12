namespace Hoteleria_App.Models;

public class ReservaHabitacion
{
    public long ReservaId { get; set; }
    public long HabitacionId { get; set; }
    public decimal PrecioNochePactado { get; set; }

    public virtual Reserva Reserva { get; set; } = null!;
    public virtual Habitacion Habitacion { get; set; } = null!;
}