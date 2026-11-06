using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<HotelUser> signInManager, HoteleriaDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Correo no válido.")]
        [CorreoDelDominio]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "La cuenta está bloqueada temporalmente por varios intentos fallidos. Inténtelo en unos minutos.");
            return Page();
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return Page();
        }

        return LocalRedirect(await DestinoAsync(returnUrl));
    }

    private async Task<string> DestinoAsync(string? returnUrl)
    {
        // «/» es el panel de administración: no es un destino pedido por la persona.
        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl != "/" && Url.IsLocalUrl(returnUrl)) return returnUrl;

        var cargo = await db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == Input.Email.ToUpper())
            .Select(u => new { u.Usuario.Cargo.Name, u.Usuario.Cargo.CrudRequerimientos })
            .SingleAsync();

        if (cargo.Name == CargosSistema.Admin) return Url.Content("~/");
        return cargo.CrudRequerimientos ? Url.Page("/Reservas/Index")! : Url.Page("/Portal/Index")!;
    }
}
