using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;

namespace Hoteleria_App.Pages.Habitaciones;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class DetallesModel : PageModel
{
    private readonly IHabitacionService _habitacionService;
    private readonly IAuthorizationService _authorization;

    public DetallesModel(IHabitacionService habitacionService, IAuthorizationService authorization)
    {
        _habitacionService = habitacionService;
        _authorization = authorization;
    }

    public Habitacion? Habitacion { get; set; }
    public bool PuedeEditar { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        PuedeEditar = (await _authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudHabitaciones)).Succeeded;
        Habitacion = await _habitacionService.GetByIdAsync(id);
        
        if (Habitacion == null)
        {
            return NotFound();
        }

        return Page();
    }
}
