using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.TiposVip;

[Authorize(Policy = HotelPermissionPolicies.CrudTipoVip)]
public class IndexModel(HoteleriaDbContext db, IBuscadorHabitaciones buscador) : PageModel
{
    public sealed record Fila(string Nombre, char Sigla, decimal PrecioCama, int Habitaciones);

    public List<Fila> Tipos { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Tipos = await db.TiposVip.AsNoTracking().OrderBy(t => t.PrecioCama)
            .Select(t => new Fila(t.TipoVipNombre, t.Sigla, t.PrecioCama, t.Habitaciones.Count))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync(string? nombre, string? sigla, decimal precioCama)
    {
        nombre = nombre?.Trim().ToUpperInvariant();
        sigla = sigla?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 15 || sigla is not { Length: 1 } || precioCama < 0)
            return Volver(false, "Indique un nombre de hasta 15 caracteres, una sigla de una letra y un precio válido.");
        if (await db.TiposVip.AnyAsync(t => t.TipoVipNombre == nombre || t.Sigla == sigla[0]))
            return Volver(false, "Ya existe un tipo con ese nombre o sigla.");

        db.TiposVip.Add(new TipoVip { TipoVipNombre = nombre, Sigla = sigla[0], PrecioCama = precioCama });
        await db.SaveChangesAsync();
        return Volver(true, "Tipo de habitación creado.");
    }

    public async Task<IActionResult> OnPostGuardarAsync(string nombre, decimal precioCama)
    {
        var tipo = await db.TiposVip.FirstOrDefaultAsync(t => t.TipoVipNombre == nombre);
        if (tipo is null) return NotFound();
        if (precioCama < 0) return Volver(false, "El precio no puede ser negativo.");

        // Las reservas conservan el precio pactado; el cambio solo afecta a las nuevas.
        tipo.PrecioCama = precioCama;
        await db.SaveChangesAsync();
        await buscador.SincronizarAsync();
        return Volver(true, "Precio actualizado.");
    }

    public async Task<IActionResult> OnPostEliminarAsync(string nombre)
    {
        var tipo = await db.TiposVip.FirstOrDefaultAsync(t => t.TipoVipNombre == nombre);
        if (tipo is null) return NotFound();
        if (await db.Habitaciones.AnyAsync(h => h.TipoVipNombre == nombre))
            return Volver(false, "No se puede eliminar: hay habitaciones de este tipo.");

        db.TiposVip.Remove(tipo);
        await db.SaveChangesAsync();
        return Volver(true, "Tipo de habitación eliminado.");
    }

    private RedirectToPageResult Volver(bool ok, string mensaje)
    {
        TempData[ok ? "MensajeExito" : "MensajeError"] = mensaje;
        return RedirectToPage();
    }
}
