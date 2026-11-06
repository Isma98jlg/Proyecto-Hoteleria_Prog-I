using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

/// <summary>Directorio de huéspedes. Contiene datos personales: solo para quien los gestiona.</summary>
[Authorize(Policy = HotelPermissionPolicies.CrudUsuarios)]
public class IndexModel(HoteleriaDbContext context) : PageModel
{
    public int TotalClientes { get; set; }
    public int ConReservas { get; set; }
    public int SinReservas => TotalClientes - ConReservas;
    public bool PuedeAdministrar => true;
    public string? Buscar { get; set; }
    public List<PersonaFila> Personas { get; set; } = new();

    public async Task OnGetAsync(string? buscar)
    {
        Buscar = buscar?.Trim();
        var huespedes = context.Usuarios.AsNoTracking().Where(u => u.Cargo.Name == CargosSistema.Huesped);

        TotalClientes = await huespedes.CountAsync();
        ConReservas = await huespedes.CountAsync(u => u.Reservas.Any());

        var query = huespedes;
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var patron = $"%{Buscar}%";
            query = query.Where(u => EF.Functions.ILike(u.Nombres, patron) || EF.Functions.ILike(u.Apellidos, patron)
                || EF.Functions.ILike(u.NumeroDocumento, patron) || EF.Functions.ILike(u.EMail, patron) || EF.Functions.ILike(u.Telefono, patron));
        }

        Personas = await query.OrderBy(u => u.Apellidos).ThenBy(u => u.Nombres)
            .Select(u => new PersonaFila
            {
                UsuarioId = u.UsuarioId,
                TipoDocumento = u.TipoDocumento,
                NumeroDocumento = u.NumeroDocumento,
                Nombres = u.Nombres,
                Apellidos = u.Apellidos,
                EMail = u.EMail,
                Telefono = u.Telefono,
                EsEmpleado = false,
                TieneCuenta = context.Users.Any(c => c.Id == u.UsuarioId),
                TotalReservas = u.Reservas.Count
            }).ToListAsync();
    }

    public sealed class PersonaFila
    {
        public long UsuarioId { get; set; }
        public string TipoDocumento { get; set; } = "";
        public string NumeroDocumento { get; set; } = "";
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string EMail { get; set; } = "";
        public string Telefono { get; set; } = "";
        public bool EsEmpleado { get; set; }
        public bool TieneCuenta { get; set; }
        public int TotalReservas { get; set; }
        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    }
}
