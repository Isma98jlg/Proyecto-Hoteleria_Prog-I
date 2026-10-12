using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hoteleria_App.Pages.Habitaciones;

public class IndexModel : PageModel
{
    private readonly IHabitacionService _habitacionService;

    public IndexModel(IHabitacionService habitacionService)
    {
        _habitacionService = habitacionService;
    }

    public List<Habitacion> Habitaciones { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? Piso { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TipoVip { get; set; }

    public SelectList EstadosDisponibles { get; set; } = default!;
    public SelectList PisosDisponibles { get; set; } = default!;
    public SelectList TiposVipDisponibles { get; set; } = default!;

    public async Task OnGetAsync()
    {
        // Cargar datos para los dropdowns
        var todas = await _habitacionService.GetAllAsync();
        
        EstadosDisponibles = new SelectList(new[] { "LIBRE", "OCUPADO", "MANTENIMIENTO" }, Estado);
        PisosDisponibles = new SelectList(todas.Select(h => h.NroPiso).Distinct().OrderBy(p => p), Piso);
        TiposVipDisponibles = new SelectList(todas.Select(h => h.TipoVipNombre).Distinct().OrderBy(t => t), TipoVip);

        // Aplicar filtros
        IEnumerable<Habitacion> query = todas;

        if (!string.IsNullOrEmpty(Estado))
        {
            query = query.Where(h => h.Estado == Estado);
        }

        if (Piso.HasValue)
        {
            query = query.Where(h => h.NroPiso == Piso.Value);
        }

        if (!string.IsNullOrEmpty(TipoVip))
        {
            query = query.Where(h => h.TipoVipNombre == TipoVip);
        }

        Habitaciones = query.ToList();
    }
}