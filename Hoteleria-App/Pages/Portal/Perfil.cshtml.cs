using Hoteleria_App.Data;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hoteleria_App.Models;

namespace Hoteleria_App.Pages.Portal;

[Authorize]
public class PerfilModel(HoteleriaDbContext db, CuentaService cuentas, UserManager<HotelUser> userManager, SignInManager<HotelUser> signInManager) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.UsuarioId == User.Id());
        if (usuario is null) return Challenge();
        Persona = DatosPersona.Desde(usuario);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var id = User.Id();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (usuario is null) return Challenge();
        if (!ModelState.IsValid) return Page();

        var duplicado = await cuentas.ValidarDuplicadosAsync(Persona, id);
        if (duplicado is not null)
        {
            ModelState.AddModelError(string.Empty, duplicado);
            return Page();
        }

        Persona.CopiarA(usuario);
        await db.SaveChangesAsync();
        var correo = await cuentas.SincronizarCorreoAsync(usuario.UsuarioId, usuario.EMail);
        if (!correo.Ok)
        {
            ModelState.AddModelError(string.Empty, correo.Error!);
            return Page();
        }

        // El nombre de usuario de la sesión es el correo.
        if (await userManager.GetUserAsync(User) is { } cuenta) await signInManager.RefreshSignInAsync(cuenta);
        TempData["MensajeExito"] = "Datos actualizados.";
        return RedirectToPage();
    }
}
