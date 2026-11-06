namespace Hoteleria_App.Models;

/// <summary>Persona registrada en el hotel: huésped o personal, según su cargo.</summary>
public class Usuario
{
    public long UsuarioId { get; set; }
    public string TipoDocumento { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string Nacionalidad { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    /// <summary>Correo de acceso, siempre del dominio único del sistema.</summary>
    public string EMail { get; set; } = null!;
    /// <summary>Correo real de cualquier proveedor, para futuros envíos.</summary>
    public string? CorreoContacto { get; set; }
    public long CargoId { get; set; }

    public virtual HotelRole Cargo { get; set; } = null!;
    public virtual Employee? Employee { get; set; }
    public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
