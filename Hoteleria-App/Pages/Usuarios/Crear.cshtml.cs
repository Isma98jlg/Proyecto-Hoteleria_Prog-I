using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Usuarios;

/// <summary>Alta de un huésped en el mostrador; queda disponible para reservar de inmediato.</summary>
[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class CrearModel(CuentaService cuentas) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var resultado = await cuentas.RegistrarHuespedSinCuentaAsync(Persona);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        TempData["MensajeExito"] = "Huésped registrado correctamente.";
        return RedirectToPage("Index");
    }
}
