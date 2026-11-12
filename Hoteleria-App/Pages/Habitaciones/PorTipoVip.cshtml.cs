using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Habitaciones;

/// <summary>
/// Dirección antigua: la vista principal de habitaciones ya filtra por categoria y muestra la situación real
/// de cada habitación, así que esta solo redirige (con el filtro, si llega).
/// </summary>
[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class PorTipoVipModel : PageModel
{
    public IActionResult OnGet(string? categoria) =>
        RedirectToPage("/Habitaciones/Index", string.IsNullOrWhiteSpace(categoria) ? null : new { Categoria = categoria });
}
