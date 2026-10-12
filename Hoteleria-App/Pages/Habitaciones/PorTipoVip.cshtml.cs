using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Habitaciones;

public class PorTipoVipModel : PageModel
{
    private readonly IHabitacionService _habitacionService;

    public PorTipoVipModel(IHabitacionService habitacionService)
    {
        _habitacionService = habitacionService;
    }

    public Dictionary<TipoVip, List<Habitacion>> TiposVipConHabitaciones { get; set; } = new();

    public async Task OnGetAsync()
    {
        var habitaciones = await _habitacionService.GetAllAsync();
        TiposVipConHabitaciones = habitaciones
            .Where(h => h.TipoVip != null)
            .GroupBy(h => h.TipoVip!)
            .OrderBy(g => g.Key.TipoVipNombre)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}