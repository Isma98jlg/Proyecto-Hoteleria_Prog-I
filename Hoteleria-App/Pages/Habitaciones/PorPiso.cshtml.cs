using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Habitaciones;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class PorPisoModel : PageModel
{
    private readonly IHabitacionService _habitacionService;

    public PorPisoModel(IHabitacionService habitacionService)
    {
        _habitacionService = habitacionService;
    }

    public Dictionary<long, List<Habitacion>> PisosConHabitaciones { get; set; } = new();

    public async Task OnGetAsync()
    {
        var habitaciones = await _habitacionService.GetAllAsync();
        PisosConHabitaciones = habitaciones
            .GroupBy(h => h.NroPiso)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}