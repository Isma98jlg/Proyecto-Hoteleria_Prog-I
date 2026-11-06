using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

[AllowAnonymous]
public class RestablecerModel(CuentaService cuentas) : PageModel
{
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Codigo { get; set; }

    [BindProperty, Required(ErrorMessage = "Indique una contraseña."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Repita la contraseña."), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var resultado = await cuentas.RestablecerAsync(Id, Codigo ?? "", Password);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }
        TempData["MensajeExito"] = "Contraseña actualizada. Ya puede iniciar sesión.";
        return RedirectToPage("/Account/Login");
    }
}
