using Microsoft.AspNetCore.Identity;

namespace Hoteleria_App.Models;

/// <summary>Cuenta Identity cuya PK comparte el usuario hotelero correspondiente.</summary>
public class HotelUser : IdentityUser<long>
{
    public virtual Usuario Usuario { get; set; } = null!;
}
