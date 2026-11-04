using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class EditarModel(HoteleriaDbContext db, UserManager<HotelUser> userManager) : PageModel
{
    [BindProperty] public CrearModel.PersonaInput Persona { get; set; } = new();
    public long UsuarioId { get; set; }
    public int ReservasAsociadas { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var user = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (user is null || await db.Employees.AnyAsync(e => e.UsuarioId == id)) return NotFound();
        UsuarioId = id;
        ReservasAsociadas = await db.Reservas.CountAsync(r => r.UsuarioId == id);
        Persona = new CrearModel.PersonaInput
        {
            TipoDocumento = user.TipoDocumento, NumeroDocumento = user.NumeroDocumento,
            Nombres = user.Nombres, Apellidos = user.Apellidos, Nacionalidad = user.Nacionalidad,
            Telefono = user.Telefono, EMail = user.EMail
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        UsuarioId = id;
        var user = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (user is null || await db.Employees.AnyAsync(e => e.UsuarioId == id)) return NotFound();
        ReservasAsociadas = await db.Reservas.CountAsync(r => r.UsuarioId == id);
        if (await db.Usuarios.AnyAsync(u => u.UsuarioId != id && u.NumeroDocumento == Persona.NumeroDocumento))
            ModelState.AddModelError("Persona.NumeroDocumento", "Ya existe una persona con ese documento.");
        if (await db.Usuarios.AnyAsync(u => u.UsuarioId != id && u.EMail == Persona.EMail))
            ModelState.AddModelError("Persona.EMail", "Ya existe una persona con ese correo.");
        if (!ModelState.IsValid) return Page();

        user.TipoDocumento = Persona.TipoDocumento.Trim();
        user.NumeroDocumento = Persona.NumeroDocumento.Trim();
        user.Nombres = Persona.Nombres.Trim();
        user.Apellidos = Persona.Apellidos.Trim();
        user.Nacionalidad = Persona.Nacionalidad.Trim();
        user.Telefono = Persona.Telefono.Trim();
        user.EMail = Persona.EMail.Trim();
        var identityUser = await userManager.FindByIdAsync(id.ToString());
        if (identityUser is not null)
        {
            identityUser.Email = user.EMail;
            identityUser.UserName = user.EMail;
            var result = await userManager.UpdateAsync(identityUser);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", string.Join(" ", result.Errors.Select(e => e.Description)));
                return Page();
            }
        }
        await db.SaveChangesAsync();
        TempData["MensajeExito"] = "Datos actualizados correctamente.";
        return RedirectToPage("Detalles", new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var user = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (user is null) return NotFound();
        var hasReservations = await db.Reservas.AnyAsync(r => r.UsuarioId == id);
        var isEmployee = await db.Employees.AnyAsync(e => e.UsuarioId == id);
        var account = await userManager.FindByIdAsync(id.ToString());
        if (hasReservations || isEmployee || account is not null)
        {
            TempData["MensajeError"] = "No se puede eliminar: la persona tiene reservas o una cuenta de acceso.";
            return RedirectToPage("Detalles", new { id });
        }
        db.Usuarios.Remove(user);
        await db.SaveChangesAsync();
        TempData["MensajeExito"] = "Huésped eliminado correctamente.";
        return RedirectToPage("Index");
    }
}
