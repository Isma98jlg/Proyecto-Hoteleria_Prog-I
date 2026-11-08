using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

/// <summary>
/// Registro público en dos pasos: primero se verifica el documento y después se crean los datos de acceso.
/// Siempre crea un huésped. El personal entra por invitación del administrador.
/// </summary>
[AllowAnonymous]
public class RegistroModel(CuentaService cuentas, VerificacionDocumentoService verificacion, SignInManager<HotelUser> signInManager) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();

    /// <summary>Solo para verificar carnés con Migraciones. No se guarda.</summary>
    [BindProperty] public DateTime? FechaNacimiento { get; set; }

    [BindProperty] public string? TokenVerificacion { get; set; }

    [BindProperty, Required(ErrorMessage = "Indique una contraseña."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Repita la contraseña."), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; set; } = "";

    public void OnGet() { }

    /// <summary>Paso 1. Responde si el documento se pudo confirmar, sin devolver nunca los datos oficiales.</summary>
    public async Task<IActionResult> OnPostVerificarAsync([FromForm] bool consentimiento)
    {
        if (!consentimiento) return new JsonResult(new ResultadoVerificacion(EstadosVerificacion.SinVerificar, "Para verificar su documento, acepte la consulta a la fuente oficial.", null));
        if (string.IsNullOrWhiteSpace(Persona.Nombres) || string.IsNullOrWhiteSpace(Persona.Apellidos))
            return new JsonResult(new ResultadoVerificacion(EstadosVerificacion.SinVerificar, "Escriba sus nombres y apellidos tal como figuran en el documento.", null));
        if (Persona.Nombres.Length > 100 || Persona.Apellidos.Length > 100 || Persona.NumeroDocumento.Length > 25) return BadRequest();

        var solicitud = new SolicitudVerificacion(Persona.TipoDocumento, Persona.NumeroDocumento, Persona.Nombres, Persona.Apellidos, Persona.PaisEmisor, FechaNacimiento);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return new JsonResult(await verificacion.VerificarAsync(solicitud, ip, HttpContext.RequestAborted));
    }

    /// <summary>Paso 2. Crea la cuenta. Si el documento no se verificó, se crea igual y recepción lo revisa.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (VerificacionDocumentoService.ValidarFormato(Persona.TipoDocumento, Persona.NumeroDocumento, Persona.PaisEmisor) is { } formato)
            ModelState.AddModelError(string.Empty, formato);
        if (Persona.TipoDocumento == TiposDocumento.Dni) Persona.Nacionalidad = "Peruana";
        ModelState.Remove("Persona.Nacionalidad");
        if (string.IsNullOrWhiteSpace(Persona.Nacionalidad)) ModelState.AddModelError("Persona.Nacionalidad", "Indique la nacionalidad.");
        if (!ModelState.IsValid) return Page();

        var documento = await verificacion.ConsumirAsync(TokenVerificacion, Persona, HttpContext.RequestAborted);
        var resultado = await cuentas.RegistrarHuespedAsync(Persona, Password, documento);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        await signInManager.PasswordSignInAsync(Persona.EMail.Trim(), Password, isPersistent: false, lockoutOnFailure: false);
        TempData["MensajeExito"] = documento.Estado == EstadosVerificacion.SinVerificar
            ? "Su cuenta se creó correctamente. Recepción revisará su documento a su llegada."
            : "Su cuenta se creó correctamente.";
        return RedirectToPage("/Portal/Index");
    }
}
