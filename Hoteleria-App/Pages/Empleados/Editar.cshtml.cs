using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Empleados;

[Authorize(Roles = CargosSistema.Admin)]
public class EditarModel(HoteleriaDbContext db, CuentaService cuentas) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();
    [BindProperty] public long CargoId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime FechaContratacion { get; set; }
    [BindProperty, Range(0, 9999999999.99, ErrorMessage = "Sueldo no válido.")] public decimal Sueldo { get; set; }

    public long UsuarioId { get; private set; }
    public List<SelectListItem> CargosDisponibles { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var empleado = await db.Employees.AsNoTracking().Include(e => e.Usuario).FirstOrDefaultAsync(e => e.UsuarioId == id);
        if (empleado is null) return NotFound();
        UsuarioId = id;
        Persona = DatosPersona.Desde(empleado.Usuario);
        CargoId = empleado.Usuario.CargoId;
        FechaContratacion = empleado.FechaContratacion;
        Sueldo = empleado.Sueldo;
        await CargarCargosAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        UsuarioId = id;
        var empleado = await db.Employees.Include(e => e.Usuario).FirstOrDefaultAsync(e => e.UsuarioId == id);
        if (empleado is null) return NotFound();
        await CargarCargosAsync();
        if (!ModelState.IsValid) return Page();

        var duplicado = await cuentas.ValidarDuplicadosAsync(Persona, id);
        if (duplicado is not null)
        {
            ModelState.AddModelError(string.Empty, duplicado);
            return Page();
        }

        Persona.CopiarA(empleado.Usuario);
        empleado.FechaContratacion = DateTime.SpecifyKind(FechaContratacion.Date, DateTimeKind.Unspecified);
        empleado.Sueldo = Sueldo;
        await db.SaveChangesAsync();

        var correo = await cuentas.SincronizarCorreoAsync(id, empleado.Usuario.EMail);
        var cargo = await cuentas.AsignarCargoAsync(id, CargoId);
        var error = correo.Error ?? cargo.Error;
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return Page();
        }

        TempData["MensajeExito"] = "Empleado actualizado.";
        // Pasar a huésped elimina la ficha laboral: se vuelve al listado.
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEnlaceAsync(long id)
    {
        var codigo = await cuentas.CrearEnlaceRestablecimientoAsync(id);
        if (codigo is null) TempData["MensajeError"] = "Esta persona no tiene cuenta.";
        else TempData["EnlaceGenerado"] = Url.Page("/Account/Restablecer", null, new { id, codigo }, Request.Scheme);
        return RedirectToPage(new { id });
    }

    private async Task CargarCargosAsync()
    {
        CargosDisponibles = await db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToListAsync();
    }
}
