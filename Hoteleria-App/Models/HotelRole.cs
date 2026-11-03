using Microsoft.AspNetCore.Identity;

namespace Hoteleria_App.Models;

/// <summary>Rol de Identity y cargo hotelero con su matriz de permisos.</summary>
public class HotelRole : IdentityRole<long>
{
    public bool Ver { get; set; }
    public bool CrudRequerimientos { get; set; }
    public bool CrudPisos { get; set; }
    public bool CrudUsuarios { get; set; }
    public bool CrudHabitaciones { get; set; }
    public bool CrudTipoVip { get; set; }
}
