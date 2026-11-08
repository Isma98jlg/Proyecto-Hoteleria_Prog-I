using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class EditarModel(HoteleriaDbContext db, CuentaService cuentas, UserManager<HotelUser> userManager, VerificacionDocumentoService verificacion) : PageModel
{
    [BindProperty] public DatosPersona Persona { get; set; } = new();
    public long UsuarioId { get; set; }
    public int ReservasAsociadas { get; set; }

    /// <summary>Solo para verificar carnés con Migraciones. No se guarda.</summary>
    [BindProperty] public DateTime? FechaNacimiento { get; set; }
    [BindProperty] public string? TokenVerificacion { get; set; }

    /// <summary>Verificación en ventanilla: devuelve los datos oficiales para autocompletar.</summary>
    public async Task<IActionResult> OnPostVerificarAsync([FromForm] bool consentimiento)
    {
        if (!consentimiento) return new JsonResult(new ResultadoVerificacion(EstadosVerificacion.SinVerificar, "Marque que el huésped autoriza la consulta.", null, null, null, null));
        if ((Persona.NumeroDocumento?.Length ?? 0) > 25 || (Persona.Nombres?.Length ?? 0) > 100 || (Persona.Apellidos?.Length ?? 0) > 100) return BadRequest();
        var solicitud = new SolicitudVerificacion(Persona.TipoDocumento ?? "", Persona.NumeroDocumento ?? "", Persona.Nombres ?? "", Persona.Apellidos ?? "", Persona.PaisEmisor, FechaNacimiento);
        return new JsonResult(await verificacion.AutocompletarAsync(solicitud, User.Id() ?? 0, HttpContext.RequestAborted));
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var usuario = await BuscarHuesped().AsNoTracking().FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (usuario is null) return NotFound();
        UsuarioId = id;
        ReservasAsociadas = await db.Reservas.CountAsync(r => r.UsuarioId == id);
        Persona = DatosPersona.Desde(usuario);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        UsuarioId = id;
        var usuario = await BuscarHuesped().FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (usuario is null) return NotFound();
        ReservasAsociadas = await db.Reservas.CountAsync(r => r.UsuarioId == id);
        if (!ModelState.IsValid) return Page();

        var duplicado = await cuentas.ValidarDuplicadosAsync(Persona, id);
        if (duplicado is not null)
        {
            ModelState.AddModelError(string.Empty, duplicado);
            return Page();
        }

        Persona.CopiarA(usuario);
        var documento = await verificacion.ConsumirAsync(TokenVerificacion, Persona, HttpContext.RequestAborted);
        if (documento.Estado != EstadosVerificacion.SinVerificar)
        {
            usuario.EstadoVerificacion = documento.Estado;
            usuario.FuenteVerificacion = documento.Fuente;
            usuario.VerificadoEn = documento.En;
        }
        await db.SaveChangesAsync();
        var correo = await cuentas.SincronizarCorreoAsync(id, usuario.EMail);
        if (!correo.Ok)
        {
            ModelState.AddModelError(string.Empty, correo.Error!);
            return Page();
        }

        TempData["MensajeExito"] = "Datos actualizados correctamente.";
        return RedirectToPage("Detalles", new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(long id)
    {
        var usuario = await BuscarHuesped().FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (usuario is null) return NotFound();
        if (await db.Reservas.AnyAsync(r => r.UsuarioId == id) || await userManager.FindByIdAsync(id.ToString()) is not null)
        {
            TempData["MensajeError"] = "No se puede eliminar: la persona tiene reservas o una cuenta de acceso.";
            return RedirectToPage("Detalles", new { id });
        }

        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync();
        TempData["MensajeExito"] = "Huésped eliminado correctamente.";
        return RedirectToPage("Index");
    }

    private IQueryable<Usuario> BuscarHuesped() => db.Usuarios.Where(u => u.Cargo.Name == CargosSistema.Huesped);
}
