namespace Hoteleria_App.Models;

public class Piso
{
    public long NroPiso { get; set; }
    public string? RefPiso { get; set; }

    public virtual ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
}