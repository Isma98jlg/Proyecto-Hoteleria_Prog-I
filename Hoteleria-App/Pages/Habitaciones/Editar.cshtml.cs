using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Habitaciones;

public class EditarModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public EditarModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Habitacion Habitacion { get; set; } = new();

    public SelectList PisosDisponibles { get; set; } = default!;
    public SelectList TiposVipDisponibles { get; set; } = default!;
    public SelectList EstadosDisponibles { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Habitacion = await _context.Habitaciones.FindAsync(id);
        
        if (Habitacion == null)
        {
            return NotFound();
        }

        await CargarDatosAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarDatosAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Attach(Habitacion).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            if (!await _context.Habitaciones.AnyAsync(h => h.HabitacionId == Habitacion.HabitacionId))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = $"Habitación #{Habitacion.HabitacionId} actualizada correctamente.";
        return RedirectToPage("./Detalles", new { id = Habitacion.HabitacionId });
    }

    private async Task CargarDatosAsync()
    {
        var pisos = await _context.Pisos.OrderBy(p => p.NroPiso).ToListAsync();
        var tiposVip = await _context.TiposVip.OrderBy(t => t.TipoVipNombre).ToListAsync();

        PisosDisponibles = new SelectList(pisos, "NroPiso", "NroPiso", Habitacion.NroPiso);
        TiposVipDisponibles = new SelectList(tiposVip, "TipoVipNombre", "TipoVipNombre", Habitacion.TipoVipNombre);
        EstadosDisponibles = new SelectList(new[] { "LIBRE", "OCUPADO", "MANTENIMIENTO" }, Habitacion.Estado);
    }
}