using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

/// <summary>Registro público: siempre crea un huésped. El personal entra por invitación.</summary>
[AllowAnonymous]
public class RegistroModel(CuentaService cuentas, SignInManager<HotelUser> signInManager) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();

    [BindProperty, Required(ErrorMessage = "Indique una contraseña."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Repita la contraseña."), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var resultado = await cuentas.RegistrarHuespedAsync(Persona, Password);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        await signInManager.PasswordSignInAsync(Persona.EMail.Trim(), Password, isPersistent: false, lockoutOnFailure: false);
        TempData["MensajeExito"] = "Su cuenta se creó correctamente.";
        return RedirectToPage("/Portal/Index");
    }
}
