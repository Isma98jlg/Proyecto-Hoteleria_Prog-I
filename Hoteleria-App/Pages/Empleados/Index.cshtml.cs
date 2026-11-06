using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Empleados;

[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(HoteleriaDbContext db, CuentaService cuentas) : PageModel
{
    public sealed record Fila(long UsuarioId, string Nombre, string Email, string Cargo, DateTime Contratacion, decimal Sueldo);

    public List<Fila> Empleados { get; private set; } = [];
    public List<SelectListItem> CargosPersonal { get; private set; } = [];

    [BindProperty, Required(ErrorMessage = "Indique el correo."), EmailAddress(ErrorMessage = "Correo no válido."), StringLength(254), CorreoDelDominio]
    public string Email { get; set; } = "";

    [BindProperty, Range(1, long.MaxValue, ErrorMessage = "Seleccione un cargo.")]
    public long CargoId { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostInvitarAsync()
    {
        var cargo = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == CargoId);
        if (cargo is null || cargo.Name == CargosSistema.Huesped) ModelState.AddModelError(nameof(CargoId), "Seleccione un cargo de personal.");
        var correo = Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.EMail.ToLower() == correo)) ModelState.AddModelError(nameof(Email), "Ya existe una persona con ese correo.");
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            return Page();
        }

        var token = await cuentas.CrearInvitacionPersonalAsync(Email, CargoId, User.Id() ?? 0);
        TempData["EnlaceGenerado"] = Url.Page("/Account/Invitacion", null, new { token }, Request.Scheme);
        return RedirectToPage();
    }

    private async Task CargarAsync()
    {
        Empleados = await db.Employees.AsNoTracking()
            .OrderBy(e => e.Usuario.Apellidos).ThenBy(e => e.Usuario.Nombres)
            .Select(e => new Fila(e.UsuarioId, e.Usuario.Nombres + " " + e.Usuario.Apellidos, e.Usuario.EMail, e.Usuario.Cargo.Name!, e.FechaContratacion, e.Sueldo))
            .ToListAsync();
        CargosPersonal = await db.Roles.AsNoTracking().Where(r => r.Name != CargosSistema.Huesped).OrderBy(r => r.Name)
            .Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToListAsync();
    }
}
