namespace Hoteleria_App.Models;

public class TipoVip
{
    public string TipoVipNombre { get; set; } = null!;
    public char Sigla { get; set; }
    public decimal PrecioCama { get; set; }

    public virtual ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
}