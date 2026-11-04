using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Hoteleria_App.Security;

namespace Hoteleria_App.Pages.Habitaciones;

[Authorize(Policy = HotelPermissionPolicies.CrudHabitaciones)]
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
        var habitacion = await _context.Habitaciones.FindAsync(id);
        
        if (habitacion is null)
        {
            return NotFound();
        }

        Habitacion = habitacion;

        await CargarDatosAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarDatosAsync();

        if (Habitacion.NroCamas < 1) ModelState.AddModelError("Habitacion.NroCamas", "Debe registrar al menos una cama.");
        if (!await _context.Pisos.AnyAsync(p => p.NroPiso == Habitacion.NroPiso)) ModelState.AddModelError("Habitacion.NroPiso", "Seleccione un piso válido.");
        if (!await _context.TiposVip.AnyAsync(t => t.TipoVipNombre == Habitacion.TipoVipNombre)) ModelState.AddModelError("Habitacion.TipoVipNombre", "Seleccione una categoría válida.");
        if (!new[] { "LIBRE", "OCUPADO", "MANTENIMIENTO" }.Contains(Habitacion.Estado)) ModelState.AddModelError("Habitacion.Estado", "Seleccione un estado válido.");

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
