using Hoteleria_App.Data;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class IndexModel(HoteleriaDbContext context, IAuthorizationService authorization) : PageModel
{
    public int TotalClientes { get; set; }
    public int ConReservas { get; set; }
    public int SinReservas => TotalClientes - ConReservas;
    public bool PuedeAdministrar { get; private set; }
    public string? Buscar { get; set; }
    public List<PersonaFila> Personas { get; set; } = new();

    public async Task OnGetAsync(string? buscar)
    {
        Buscar = buscar?.Trim();
        PuedeAdministrar = (await authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudUsuarios)).Succeeded;
        var query = context.Usuarios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Buscar))
            query = query.Where(u => u.Nombres.Contains(Buscar) || u.Apellidos.Contains(Buscar)
                || u.NumeroDocumento.Contains(Buscar) || u.EMail.Contains(Buscar) || u.Telefono.Contains(Buscar));

        TotalClientes = await context.Usuarios.CountAsync();
        ConReservas = await context.Usuarios.CountAsync(u => context.Reservas.Any(r => r.UsuarioId == u.UsuarioId));
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
                EsEmpleado = u.Employee != null,
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
        public int TotalReservas { get; set; }
        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    }
}
