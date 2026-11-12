using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Habitaciones;

/// <summary>
/// Dirección antigua: la vista principal de habitaciones ya filtra por piso y muestra la situación real
/// de cada habitación, así que esta solo redirige (con el filtro, si llega).
/// </summary>
[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class PorPisoModel : PageModel
{
    public IActionResult OnGet(string? piso) =>
        RedirectToPage("/Habitaciones/Index", string.IsNullOrWhiteSpace(piso) ? null : new { Piso = piso });
}
