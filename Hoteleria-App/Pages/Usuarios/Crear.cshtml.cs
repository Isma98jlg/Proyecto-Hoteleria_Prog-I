using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Usuarios;

/// <summary>Alta de un huésped en el mostrador; queda disponible para reservar de inmediato.</summary>
[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class CrearModel(CuentaService cuentas, VerificacionDocumentoService verificacion) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();

    public void OnGet() { }

    /// <summary>Solo para verificar carnés con Migraciones. No se guarda.</summary>
    [BindProperty] public DateTime? FechaNacimiento { get; set; }
    [BindProperty] public string? TokenVerificacion { get; set; }

    /// <summary>Verificación en ventanilla: devuelve los datos oficiales para autocompletar.</summary>
    public async Task<IActionResult> OnPostVerificarAsync([FromForm] bool consentimiento)
    {
        if (!consentimiento) return new JsonResult(new ResultadoAutocompletado(EstadosVerificacion.SinVerificar, "Marque que el huésped autoriza la consulta.", null, null, null, null));
        if ((Persona.NumeroDocumento?.Length ?? 0) > 25 || (Persona.Nombres?.Length ?? 0) > 100 || (Persona.Apellidos?.Length ?? 0) > 100) return BadRequest();
        var solicitud = new SolicitudVerificacion(Persona.TipoDocumento ?? "", Persona.NumeroDocumento ?? "", Persona.Nombres ?? "", Persona.Apellidos ?? "", Persona.PaisEmisor, FechaNacimiento);
        return new JsonResult(await verificacion.AutocompletarAsync(solicitud, User.Id() ?? 0, HttpContext.RequestAborted));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var documento = await verificacion.ConsumirAsync(TokenVerificacion, Persona, HttpContext.RequestAborted);
        var resultado = await cuentas.RegistrarHuespedSinCuentaAsync(Persona, documento);
        if (!resultado.Ok)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        TempData["MensajeExito"] = "Huésped registrado correctamente.";
        return RedirectToPage("Index");
    }
}
