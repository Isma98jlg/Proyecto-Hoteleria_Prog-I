using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

/// <summary>
/// Cierre de sesión. Abrir la página solo pide confirmación (un enlace no debe cerrar la sesión por sí solo);
/// el botón del menú y el de esta página envían el formulario.
/// </summary>
[Authorize]
public class LogoutModel(SignInManager<Hoteleria_App.Models.HotelUser> signInManager) : PageModel
{
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        TempData["MensajeExito"] = "Su sesión se cerró correctamente.";
        return RedirectToPage("/Account/Login");
    }
}
