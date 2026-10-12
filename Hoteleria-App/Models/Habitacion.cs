namespace Hoteleria_App.Models;

public class Habitacion
{
    public long HabitacionId { get; set; }
    public long NroPiso { get; set; }
    public int NroCamas { get; set; }
    public string TipoVipNombre { get; set; } = null!;
    public string Estado { get; set; } = null!;

    public virtual Piso Piso { get; set; } = null!;
    public virtual TipoVip TipoVip { get; set; } = null!;
    public virtual ICollection<ReservaHabitacion> ReservaHabitaciones { get; set; } = new List<ReservaHabitacion>();
}