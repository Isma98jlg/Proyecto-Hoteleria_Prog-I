using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

public class CambiarContrasenaModel(UserManager<HotelUser> userManager, SignInManager<HotelUser> signInManager) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Indique su contraseña actual."), DataType(DataType.Password)]
    public string Actual { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Indique la nueva contraseña."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use al menos 8 caracteres.")]
    public string Nueva { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Repita la contraseña."), DataType(DataType.Password), Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var cuenta = await userManager.GetUserAsync(User);
        if (cuenta is null) return Challenge();

        var resultado = await userManager.ChangePasswordAsync(cuenta, Actual, Nueva);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await signInManager.RefreshSignInAsync(cuenta);
        TempData["MensajeExito"] = "Contraseña actualizada.";
        return RedirectToPage();
    }
}
