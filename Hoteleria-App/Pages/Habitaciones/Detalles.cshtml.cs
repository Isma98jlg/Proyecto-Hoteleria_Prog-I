using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Habitaciones;

public class DetallesModel : PageModel
{
    private readonly IHabitacionService _habitacionService;

    public DetallesModel(IHabitacionService habitacionService)
    {
        _habitacionService = habitacionService;
    }

    public Habitacion? Habitacion { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Habitacion = await _habitacionService.GetByIdAsync(id);
        
        if (Habitacion == null)
        {
            return NotFound();
        }

        return Page();
    }
}