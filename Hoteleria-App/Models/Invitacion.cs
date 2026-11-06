namespace Hoteleria_App.Models;

/// <summary>
/// Enlace de un solo uso para crear una cuenta: alta de personal con el cargo elegido
/// por el administrador, o activación de la cuenta de un huésped ya registrado.
/// Solo se guarda el hash del token.
/// </summary>
public class Invitacion
{
    public long InvitacionId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string Email { get; set; } = null!;
    public long CargoId { get; set; }
    public long? UsuarioId { get; set; }
    public long CreadaPorId { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? UsadaEn { get; set; }

    public virtual HotelRole Cargo { get; set; } = null!;
}
