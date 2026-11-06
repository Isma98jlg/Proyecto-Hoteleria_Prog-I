using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Habitaciones;

[Authorize(Policy = HotelPermissionPolicies.CrudHabitaciones)]
public class CrearModel(HoteleriaDbContext context, IBuscadorHabitaciones buscador) : PageModel
{
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

        if (Habitacion.HabitacionId < 1) ModelState.AddModelError("Habitacion.HabitacionId", "Indique el número de la habitación.");
        else if (await context.Habitaciones.AnyAsync(h => h.HabitacionId == Habitacion.HabitacionId)) ModelState.AddModelError("Habitacion.HabitacionId", "Ya existe una habitación con ese número.");
        if (Habitacion.NroCamas < 1) ModelState.AddModelError("Habitacion.NroCamas", "Debe registrar al menos una cama.");
        if (!await context.Pisos.AnyAsync(p => p.NroPiso == Habitacion.NroPiso)) ModelState.AddModelError("Habitacion.NroPiso", "Seleccione un piso válido.");
        if (!await context.TiposVip.AnyAsync(t => t.TipoVipNombre == Habitacion.TipoVipNombre)) ModelState.AddModelError("Habitacion.TipoVipNombre", "Seleccione una categoría válida.");
        if (Habitacion.Estado is not (EstadosHabitacion.Libre or EstadosHabitacion.Mantenimiento)) ModelState.AddModelError("Habitacion.Estado", "Seleccione un estado válido.");

        if (!ModelState.IsValid) return Page();

        context.Habitaciones.Add(new Habitacion
        {
            HabitacionId = Habitacion.HabitacionId,
            NroPiso = Habitacion.NroPiso,
            NroCamas = Habitacion.NroCamas,
            TipoVipNombre = Habitacion.TipoVipNombre,
            Estado = Habitacion.Estado
        });
        await context.SaveChangesAsync();
        await buscador.SincronizarAsync();

        TempData["MensajeExito"] = $"Habitación #{Habitacion.HabitacionId} creada correctamente.";
        return RedirectToPage("./Index");
    }

    private async Task CargarDatosAsync()
    {
        var pisos = await context.Pisos.OrderBy(p => p.NroPiso).ToListAsync();
        var tiposVip = await context.TiposVip.OrderBy(t => t.TipoVipNombre).ToListAsync();

        PisosDisponibles = new SelectList(pisos, "NroPiso", "NroPiso");
        TiposVipDisponibles = new SelectList(tiposVip, "TipoVipNombre", "TipoVipNombre");
        EstadosDisponibles = new SelectList(new[] { EstadosHabitacion.Libre, EstadosHabitacion.Mantenimiento });
    }
}
