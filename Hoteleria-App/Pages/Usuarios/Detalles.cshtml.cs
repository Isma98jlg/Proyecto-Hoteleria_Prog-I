using Hoteleria_App.Data;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Usuarios;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class DetallesModel(HoteleriaDbContext db, IAuthorizationService authorization) : PageModel
{
    public PersonaDetalle? Persona { get; set; }
    public bool PuedeAdministrar { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        PuedeAdministrar = (await authorization.AuthorizeAsync(User, HotelPermissionPolicies.CrudUsuarios)).Succeeded;
        Persona = await db.Usuarios.AsNoTracking().Where(u => u.UsuarioId == id)
            .Select(u => new PersonaDetalle
            {
                UsuarioId = u.UsuarioId, TipoDocumento = u.TipoDocumento, NumeroDocumento = u.NumeroDocumento,
                Nombres = u.Nombres, Apellidos = u.Apellidos, Nacionalidad = u.Nacionalidad,
                Telefono = u.Telefono, EMail = u.EMail, EsEmpleado = u.Employee != null,
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
        public bool EsEmpleado { get; set; }
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
