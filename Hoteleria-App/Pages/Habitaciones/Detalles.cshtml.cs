using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Habitaciones;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class DetallesModel(IHabitacionService habitacionService, IAuthorizationService authorization) : PageModel
{
    public Habitacion? Habitacion { get; set; }
    public bool PuedeEditar { get; private set; }
    public bool PuedeVerReservas { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        PuedeEditar = (await authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudHabitaciones)).Succeeded;
        // El historial muestra nombres de huéspedes: solo para quien gestiona reservas.
        PuedeVerReservas = (await authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudRequerimientos)).Succeeded;
        Habitacion = await habitacionService.GetByIdAsync(id);
        return Habitacion is null ? NotFound() : Page();
    }
}
