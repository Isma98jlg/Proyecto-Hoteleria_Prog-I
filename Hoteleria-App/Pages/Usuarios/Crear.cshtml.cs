using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class CrearModel(HoteleriaDbContext db) : PageModel
{
    [BindProperty] public PersonaInput Persona { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await db.Usuarios.AnyAsync(u => u.NumeroDocumento == Persona.NumeroDocumento))
            ModelState.AddModelError("Persona.NumeroDocumento", "Ya existe una persona con ese documento.");
        if (await db.Usuarios.AnyAsync(u => u.EMail == Persona.EMail))
            ModelState.AddModelError("Persona.EMail", "Ya existe una persona con ese correo.");
        if (!ModelState.IsValid) return Page();

        var id = (await db.Usuarios.MaxAsync(u => (long?)u.UsuarioId) ?? 0) + 1;
        db.Usuarios.Add(new Usuario
        {
            UsuarioId = id,
            TipoDocumento = Persona.TipoDocumento.Trim(),
            NumeroDocumento = Persona.NumeroDocumento.Trim(),
            Nombres = Persona.Nombres.Trim(),
            Apellidos = Persona.Apellidos.Trim(),
            Nacionalidad = Persona.Nacionalidad.Trim(),
            Telefono = Persona.Telefono.Trim(),
            EMail = Persona.EMail.Trim()
        });
        await db.SaveChangesAsync();
        TempData["MensajeExito"] = "Huésped registrado correctamente.";
        return RedirectToPage("Index");
    }

    public sealed class PersonaInput
    {
        [Required, StringLength(15)] public string TipoDocumento { get; set; } = "DNI";
        [Required, StringLength(25)] public string NumeroDocumento { get; set; } = "";
        [Required, StringLength(25)] public string Nombres { get; set; } = "";
        [Required, StringLength(25)] public string Apellidos { get; set; } = "";
        [Required, StringLength(25)] public string Nacionalidad { get; set; } = "";
        [Required, StringLength(25)] public string Telefono { get; set; } = "";
        [Required, EmailAddress, StringLength(25)] public string EMail { get; set; } = "";
    }
}
