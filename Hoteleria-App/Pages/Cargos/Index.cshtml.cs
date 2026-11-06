using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Cargos;

[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(HoteleriaDbContext db, RoleManager<HotelRole> roleManager, PermisosService permisos) : PageModel
{
    public sealed record Fila(HotelRole Cargo, int Personas);

    public List<Fila> Filas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Filas = await db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new Fila(r, db.Usuarios.Count(u => u.CargoId == r.Id)))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync(string? nombre)
    {
        nombre = nombre?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 50)
        {
            TempData["MensajeError"] = "Indique un nombre de hasta 50 caracteres.";
            return RedirectToPage();
        }

        var resultado = await roleManager.CreateAsync(new HotelRole { Name = nombre, Ver = true });
        TempData[resultado.Succeeded ? "MensajeExito" : "MensajeError"] = resultado.Succeeded
            ? "Cargo creado."
            : string.Join(" ", resultado.Errors.Select(e => e.Description));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGuardarAsync(long id, bool ver, bool crudRequerimientos, bool crudPisos, bool crudUsuarios, bool crudHabitaciones, bool crudTipoVip)
    {
        var cargo = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (cargo is null) return NotFound();
        if (cargo.Name == CargosSistema.Admin)
        {
            TempData["MensajeError"] = "Los permisos del administrador no se pueden reducir.";
            return RedirectToPage();
        }

        cargo.Ver = ver;
        cargo.CrudRequerimientos = crudRequerimientos;
        cargo.CrudPisos = crudPisos;
        cargo.CrudUsuarios = crudUsuarios;
        cargo.CrudHabitaciones = crudHabitaciones;
        cargo.CrudTipoVip = crudTipoVip;
        await db.SaveChangesAsync();
        await permisos.InvalidarCargoAsync(id);
        TempData["MensajeExito"] = "Permisos actualizados.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var cargo = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (cargo is null) return NotFound();
        if (CargosSistema.DeSistema(cargo.Name) || await db.Usuarios.AnyAsync(u => u.CargoId == id))
        {
            TempData["MensajeError"] = "No se puede eliminar: es un cargo del sistema o tiene personas asignadas.";
            return RedirectToPage();
        }

        await roleManager.DeleteAsync(cargo);
        TempData["MensajeExito"] = "Cargo eliminado.";
        return RedirectToPage();
    }
}
