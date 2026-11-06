using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class DetallesModel(HoteleriaDbContext db, CuentaService cuentas) : PageModel
{
    public PersonaDetalle? Persona { get; set; }
    public bool PuedeAdministrar => true;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        // El personal se administra en /Empleados; aquí solo se ven huéspedes, salvo para admin.
        var esAdmin = User.IsInRole(CargosSistema.Admin);
        Persona = await db.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == id && (esAdmin || u.Cargo.Name == CargosSistema.Huesped))
            .Select(u => new PersonaDetalle
            {
                UsuarioId = u.UsuarioId, TipoDocumento = u.TipoDocumento, NumeroDocumento = u.NumeroDocumento,
                Nombres = u.Nombres, Apellidos = u.Apellidos, Nacionalidad = u.Nacionalidad,
                Telefono = u.Telefono, EMail = u.EMail, CorreoContacto = u.CorreoContacto, EsEmpleado = u.Employee != null,
                TieneCuenta = db.Users.Any(c => c.Id == u.UsuarioId),
                Reservas = u.Reservas.OrderByDescending(r => r.InicioReserva).Select(r => new ReservaFila
                {
                    ReservaId = r.ReservaId, Estado = r.EstadoReserva, Inicio = r.InicioReserva,
                    Fin = r.FinReserva, TotalNoches = r.TotalNoches,
                    TotalHabitaciones = r.ReservaHabitaciones.Count,
                    Total = r.ReservaHabitaciones.Sum(rh => rh.PrecioNochePactado) * r.TotalNoches
                }).ToList()
            }).SingleOrDefaultAsync();
        if (Persona is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostActivacionAsync(long id)
    {
        if (!await EsHuespedAsync(id)) return NotFound();
        var token = await cuentas.CrearActivacionHuespedAsync(id, User.Id() ?? 0);
        if (token is null) TempData["MensajeError"] = "Esta persona ya tiene una cuenta.";
        else TempData["EnlaceGenerado"] = Url.Page("/Account/Invitacion", null, new { token }, Request.Scheme);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRestablecerAsync(long id)
    {
        if (!await EsHuespedAsync(id)) return NotFound();
        var codigo = await cuentas.CrearEnlaceRestablecimientoAsync(id);
        if (codigo is null) TempData["MensajeError"] = "Esta persona no tiene cuenta.";
        else TempData["EnlaceGenerado"] = Url.Page("/Account/Restablecer", null, new { id, codigo }, Request.Scheme);
        return RedirectToPage(new { id });
    }

    private Task<bool> EsHuespedAsync(long id) =>
        db.Usuarios.AsNoTracking().AnyAsync(u => u.UsuarioId == id && u.Cargo.Name == CargosSistema.Huesped);

    public sealed class PersonaDetalle
    {
        public long UsuarioId { get; set; }
        public string TipoDocumento { get; set; } = "";
        public string NumeroDocumento { get; set; } = "";
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string Nacionalidad { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string EMail { get; set; } = "";
        public string? CorreoContacto { get; set; }
        public bool EsEmpleado { get; set; }
        public bool TieneCuenta { get; set; }
        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
        public List<ReservaFila> Reservas { get; set; } = new();
    }

    public sealed class ReservaFila
    {
        public long ReservaId { get; set; }
        public string Estado { get; set; } = "";
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
        public int TotalNoches { get; set; }
        public int TotalHabitaciones { get; set; }
        public decimal Total { get; set; }
    }
}
