using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Chat de reservas del huésped. El titular es siempre quien inició sesión.</summary>
[Authorize(Roles = CargosSistema.Huesped)]
public class AsistenteModel(AsistenteHuespedService asistente) : PageModel
{
    public bool Disponible => asistente.Disponible;
    public object Inicial { get; private set; } = new { };

    public async Task OnGetAsync() => Inicial = Vista(new(await asistente.ObtenerAsync(User.Id() ?? 0)));

    public async Task<IActionResult> OnPostEnviarAsync([FromForm] string? mensaje) =>
        new JsonResult(Vista(await asistente.EnviarAsync(User.Id() ?? 0, mensaje, HttpContext.RequestAborted)));

    public async Task<IActionResult> OnPostConfirmarAsync() =>
        new JsonResult(Vista(await asistente.ConfirmarAsync(User.Id() ?? 0)));

    public async Task<IActionResult> OnPostReiniciarAsync()
    {
        await asistente.ReiniciarAsync(User.Id() ?? 0);
        return new JsonResult(Vista(new(new ConversacionAsistente())));
    }

    private static object Vista(ResultadoAsistente r) => new
    {
        mensajes = r.Conversacion.Mensajes.Select(m => new { rol = m.Rol, texto = m.Texto }),
        propuesta = r.Conversacion.Propuesta is { } p
            ? new
            {
                entrada = p.Inicio.ToString("dd/MM/yyyy"),
                salida = p.Inicio.AddDays(p.Noches).ToString("dd/MM/yyyy"),
                noches = p.Noches,
                personas = p.Personas,
                total = Soles.Formatear(p.Total),
                habitaciones = p.Lineas.Select(l => new { codigo = l.Codigo, categoria = l.Categoria, camas = l.Camas, precio = Soles.Formatear(l.PrecioNoche), ubicacion = l.Ubicacion })
            }
            : null,
        error = r.Error,
        reservaCreada = r.ReservaCreada
    };
}
