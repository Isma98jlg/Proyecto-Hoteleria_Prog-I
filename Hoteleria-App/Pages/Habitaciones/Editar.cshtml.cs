using Hoteleria_App.Data;
using Hoteleria_App.Hubs;
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
public class EditarModel(HoteleriaDbContext context, IBuscadorHabitaciones buscador, INotificadorHotel notificador, RelojHotel reloj) : PageModel
{
    [BindProperty]
    public Habitacion Habitacion { get; set; } = new();

    public SelectList PisosDisponibles { get; set; } = default!;
    public SelectList TiposVipDisponibles { get; set; } = default!;
    public SelectList EstadosDisponibles { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var habitacion = await context.Habitaciones.AsNoTracking().FirstOrDefaultAsync(h => h.HabitacionId == id);
        if (habitacion is null) return NotFound();

        Habitacion = habitacion;
        await CargarDatosAsync(habitacion.Estado);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        var actual = await context.Habitaciones.FirstOrDefaultAsync(h => h.HabitacionId == id);
        if (actual is null) return NotFound();
        await CargarDatosAsync(actual.Estado);

        if (Habitacion.NroCamas < 1) ModelState.AddModelError("Habitacion.NroCamas", "Debe registrar al menos una cama.");
        if (!await context.Pisos.AnyAsync(p => p.NroPiso == Habitacion.NroPiso)) ModelState.AddModelError("Habitacion.NroPiso", "Seleccione un piso válido.");
        // El número no cambia: si se cambia el piso, debe seguir coincidiendo con él (la 401 va en el piso 4).
        else if (Habitacion.NroPiso != actual.NroPiso && !CodigoHabitacion.EncajaConPiso(id, Habitacion.NroPiso))
            ModelState.AddModelError("Habitacion.NroPiso", $"La habitación {id} va en el piso {id / 100}: el número empieza por el piso.");
        if (!await context.TiposVip.AnyAsync(t => t.TipoVipNombre == Habitacion.TipoVipNombre)) ModelState.AddModelError("Habitacion.TipoVipNombre", "Seleccione una categoría válida.");

        // «Ocupado» refleja una estancia real: lo cambian el check-in y el check-out.
        if (actual.Estado == EstadosHabitacion.Ocupado && Habitacion.Estado != EstadosHabitacion.Ocupado)
            ModelState.AddModelError("Habitacion.Estado", "La habitación está ocupada; se libera con el check-out de la reserva.");
        else if (actual.Estado != EstadosHabitacion.Ocupado && Habitacion.Estado is not (EstadosHabitacion.Libre or EstadosHabitacion.Mantenimiento))
            ModelState.AddModelError("Habitacion.Estado", "Seleccione libre o mantenimiento.");
        // No se manda a mantenimiento una habitación con una reserva que cubre hoy: el check-in fallaría.
        else if (Habitacion.Estado == EstadosHabitacion.Mantenimiento && actual.Estado != EstadosHabitacion.Mantenimiento)
        {
            var hoy = reloj.Hoy;
            var reservaHoy = await context.ReservaHabitaciones.AsNoTracking()
                .Where(rh => rh.HabitacionId == id && rh.Activa && rh.Inicio <= hoy && rh.Fin > hoy)
                .Select(rh => (long?)rh.ReservaId)
                .FirstOrDefaultAsync();
            if (reservaHoy is { } reservaId)
                ModelState.AddModelError("Habitacion.Estado", $"La habitación tiene una reserva para hoy (#{reservaId}). Cámbiela de habitación o cancélela antes de ponerla en mantenimiento.");
        }

        if (!ModelState.IsValid) return Page();

        var cambioEstado = actual.Estado != Habitacion.Estado;
        actual.NroPiso = Habitacion.NroPiso;
        actual.NroCamas = Habitacion.NroCamas;
        actual.TipoVipNombre = Habitacion.TipoVipNombre;
        actual.Estado = Habitacion.Estado;
        await context.SaveChangesAsync();
        await buscador.SincronizarAsync();
        if (cambioEstado) await notificador.HabitacionActualizadaAsync(id, actual.Estado);

        TempData["MensajeExito"] = $"Habitación #{id} actualizada correctamente.";
        return RedirectToPage("./Detalles", new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var habitacion = await context.Habitaciones.FirstOrDefaultAsync(h => h.HabitacionId == id);
        if (habitacion is null) return NotFound();
        if (await context.ReservaHabitaciones.AnyAsync(rh => rh.HabitacionId == id))
        {
            TempData["MensajeError"] = "No se puede eliminar una habitación con reservas; márquela en mantenimiento.";
            return RedirectToPage("./Detalles", new { id });
        }

        context.Habitaciones.Remove(habitacion);
        await context.SaveChangesAsync();
        await buscador.SincronizarAsync();
        TempData["MensajeExito"] = $"Habitación #{id} eliminada.";
        return RedirectToPage("./Index");
    }

    private async Task CargarDatosAsync(string estadoActual)
    {
        var pisos = await context.Pisos.OrderBy(p => p.NroPiso).ToListAsync();
        var tiposVip = await context.TiposVip.OrderBy(t => t.TipoVipNombre).ToListAsync();

        PisosDisponibles = new SelectList(pisos, "NroPiso", "NroPiso", Habitacion.NroPiso);
        TiposVipDisponibles = new SelectList(tiposVip, "TipoVipNombre", "TipoVipNombre", Habitacion.TipoVipNombre);
        EstadosDisponibles = estadoActual == EstadosHabitacion.Ocupado
            ? new SelectList(new[] { EstadosHabitacion.Ocupado }, estadoActual)
            : new SelectList(new[] { EstadosHabitacion.Libre, EstadosHabitacion.Mantenimiento }, estadoActual);
    }
}
