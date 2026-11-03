namespace Hoteleria_App.Models;

/// <summary>Persona registrada en el hotel; puede ser huésped y/o empleado.</summary>
public class Usuario
{
    public long UsuarioId { get; set; }
    public string TipoDocumento { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string Nacionalidad { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    public string EMail { get; set; } = null!;
    public long? CargoId { get; set; }

    public virtual Employee? Employee { get; set; }
    public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
