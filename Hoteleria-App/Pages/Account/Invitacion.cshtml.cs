using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

/// <summary>Alta de personal por invitación y activación de la cuenta de un huésped ya registrado.</summary>
[AllowAnonymous]
public class InvitacionModel(CuentaService cuentas) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    // Se lee a mano: al activar una ficha existente no se piden ni validan datos personales.
    public DatosPersona Persona { get; set; } = new();

    [BindProperty, Required(ErrorMessage = "Indique una contraseña."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Repita la contraseña."), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; set; } = "";

    public InvitacionVista? Invitacion { get; private set; }

    public async Task OnGetAsync()
    {
        Invitacion = await cuentas.LeerInvitacionAsync(Token);
        if (Invitacion is not null) Persona.EMail = Invitacion.Email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Invitacion = await cuentas.LeerInvitacionAsync(Token);
        if (Invitacion is null) return Page();

        if (!Invitacion.EsActivacion)
        {
            await TryUpdateModelAsync(Persona, nameof(Persona));
            // El correo lo fija la invitación, no el formulario.
            Persona.EMail = Invitacion.Email;
            ModelState.Remove("Persona.EMail");
        }
        if (!ModelState.IsValid) return Page();

        var resultado = await cuentas.AceptarInvitacionAsync(Token!, Invitacion.EsActivacion ? null : Persona, Password);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        TempData["MensajeExito"] = "Su cuenta está lista. Ya puede iniciar sesión.";
        return RedirectToPage("/Account/Login");
    }
}
