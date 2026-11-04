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
public class CrearModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public CrearModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Habitacion Habitacion { get; set; } = new();

    public SelectList PisosDisponibles { get; set; } = default!;
    public SelectList TiposVipDisponibles { get; set; } = default!;
    public SelectList EstadosDisponibles { get; set; } = default!;

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
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

        // El esquema usa identificadores explícitos, por lo que se asigna el siguiente ID disponible.
        var maxId = await _context.Habitaciones.MaxAsync(h => (long?)h.HabitacionId) ?? 0;
        Habitacion.HabitacionId = maxId + 1;

        _context.Habitaciones.Add(Habitacion);
        await _context.SaveChangesAsync();

        TempData["MensajeExito"] = $"Habitación #{Habitacion.HabitacionId} creada correctamente.";
        return RedirectToPage("./Index");
    }

    private async Task CargarDatosAsync()
    {
        var pisos = await _context.Pisos.OrderBy(p => p.NroPiso).ToListAsync();
        var tiposVip = await _context.TiposVip.OrderBy(t => t.TipoVipNombre).ToListAsync();

        PisosDisponibles = new SelectList(pisos, "NroPiso", "NroPiso");
        TiposVipDisponibles = new SelectList(tiposVip, "TipoVipNombre", "TipoVipNombre");
        EstadosDisponibles = new SelectList(new[] { "LIBRE", "OCUPADO", "MANTENIMIENTO" });
    }
}
