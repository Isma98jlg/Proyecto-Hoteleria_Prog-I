using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Hoteleria_App.Services;

public interface IReservaService
{
    Task<List<ReservaViewModel>> GetAllAsync(string? buscar = null, string? estado = null, string? usuario = null);
    Task<ReservaDetalleViewModel?> GetByIdAsync(long id);
    Task<List<UsuarioComboViewModel>> GetUsuariosActivosAsync();
    Task<List<HabitacionDisponibleViewModel>> GetHabitacionesDisponiblesAsync(DateTime inicio, DateTime fin);
    Task<bool> CrearAsync(ReservaCreateModel model);
    Task<bool> EditarAsync(long id, ReservaEditModel model);
    Task<bool> EliminarAsync(long id);
    Task<List<string>> GetEstadosDisponiblesAsync();
}

public class ReservaViewModel
{
    public long ReservaId { get; set; }
    public string UsuarioNombre { get; set; } = "";
    public string UsuarioDocumento { get; set; } = "";
    public string EstadoReserva { get; set; } = "";
    public DateTime FechaReservaCreacion { get; set; }
    public DateTime InicioReserva { get; set; }
    public DateTime FinReserva { get; set; }
    public int TotalNoches { get; set; }
    public int TotalHabitaciones { get; set; }
    public decimal PrecioTotal { get; set; }
    public string EstadoEtiqueta { get; set; } = "";
    public string BadgeEstado { get; set; } = "";
}

public class ReservaDetalleViewModel
{
    public long ReservaId { get; set; }
    public string UsuarioNombre { get; set; } = "";
    public string UsuarioDocumento { get; set; } = "";
    public string UsuarioEmail { get; set; } = "";
    public string EstadoReserva { get; set; } = "";
    public string EstadoEtiqueta { get; set; } = "";
    public string BadgeEstado { get; set; } = "";
    public DateTime FechaReservaCreacion { get; set; }
    public DateTime InicioReserva { get; set; }
    public DateTime FinReserva { get; set; }
    public int TotalNoches { get; set; }
    public List<ReservaHabitacionDetalle> Habitaciones { get; set; } = new();
    public decimal PrecioTotal { get; set; }
}

public class ReservaHabitacionDetalle
{
    public long HabitacionId { get; set; }
    public string CodigoHabitacion { get; set; } = "";
    public int NroCamas { get; set; }
    public string TipoVip { get; set; } = "";
    public decimal PrecioNochePactado { get; set; }
}

public class UsuarioComboViewModel
{
    public long UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string CargoNombre { get; set; } = "";
}

public class HabitacionDisponibleViewModel
{
    public long HabitacionId { get; set; }
    public string Codigo { get; set; } = "";
    public int NroCamas { get; set; }
    public string TipoVip { get; set; } = "";
    public decimal PrecioCama { get; set; }
}

public class ReservaCreateModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione un huésped.")]
    public long UsuarioId { get; set; }
    [DataType(DataType.Date)]
    public DateTime InicioReserva { get; set; }
    [Range(1, 365, ErrorMessage = "El número de noches debe estar entre 1 y 365.")]
    public int TotalNoches { get; set; }
    public string EstadoReserva { get; set; } = "PENDIENTE";
    public List<long> HabitacionIds { get; set; } = new();
    public List<decimal> PreciosNochePactado { get; set; } = new();
}

public class ReservaEditModel
{
    public string EstadoReserva { get; set; } = "";
    public List<long> HabitacionIds { get; set; } = new();
    public List<decimal> PreciosNochePactado { get; set; } = new();
}

public class ReservaService : IReservaService
{
    private readonly HoteleriaDbContext _context;

    public ReservaService(HoteleriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReservaViewModel>> GetAllAsync(string? buscar = null, string? estado = null, string? usuario = null)
    {
        var query = _context.Reservas
            .Include(r => r.Usuario)
            .Include(r => r.ReservaHabitaciones)
                .ThenInclude(rh => rh.Habitacion)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(r => r.EstadoReserva == estado);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            query = query.Where(r =>
                r.Usuario.Nombres.Contains(buscar) ||
                r.Usuario.Apellidos.Contains(buscar));
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            query = query.Where(r => r.UsuarioId == long.Parse(usuario));
        }

        return await query
            .OrderByDescending(r => r.FechaReservaCreacion)
            .Select(r => new ReservaViewModel
            {
                ReservaId = r.ReservaId,
                UsuarioNombre = $"{r.Usuario.Nombres} {r.Usuario.Apellidos}".Trim(),
                UsuarioDocumento = r.Usuario.NumeroDocumento,
                EstadoReserva = r.EstadoReserva,
                FechaReservaCreacion = r.FechaReservaCreacion,
                InicioReserva = r.InicioReserva,
                FinReserva = r.FinReserva,
                TotalNoches = r.TotalNoches,
                TotalHabitaciones = r.ReservaHabitaciones.Count,
                PrecioTotal = r.ReservaHabitaciones.Sum(rh => rh.PrecioNochePactado) * r.TotalNoches,
                EstadoEtiqueta = GetEstadoEtiqueta(r.EstadoReserva),
                BadgeEstado = GetBadgeEstado(r.EstadoReserva)
            })
            .ToListAsync();
    }

    public async Task<ReservaDetalleViewModel?> GetByIdAsync(long id)
    {
        var r = await _context.Reservas
            .Include(r => r.Usuario)
            .Include(r => r.ReservaHabitaciones)
                .ThenInclude(rh => rh.Habitacion)
                    .ThenInclude(h => h.TipoVip)
            .FirstOrDefaultAsync(r => r.ReservaId == id);

        if (r is null) return null;

        return new ReservaDetalleViewModel
        {
            ReservaId = r.ReservaId,
            UsuarioNombre = $"{r.Usuario.Nombres} {r.Usuario.Apellidos}".Trim(),
            UsuarioDocumento = r.Usuario.NumeroDocumento,
            UsuarioEmail = r.Usuario.EMail,
            EstadoReserva = r.EstadoReserva,
            FechaReservaCreacion = r.FechaReservaCreacion,
            InicioReserva = r.InicioReserva,
            FinReserva = r.FinReserva,
            TotalNoches = r.TotalNoches,
            PrecioTotal = r.ReservaHabitaciones.Sum(rh => rh.PrecioNochePactado) * r.TotalNoches,
            EstadoEtiqueta = GetEstadoEtiqueta(r.EstadoReserva),
            BadgeEstado = GetBadgeEstado(r.EstadoReserva),
            Habitaciones = r.ReservaHabitaciones.Select(rh => new ReservaHabitacionDetalle
            {
                HabitacionId = rh.HabitacionId,
                CodigoHabitacion = $"{rh.Habitacion.NroPiso}{rh.Habitacion.TipoVip.Sigla}-{rh.Habitacion.HabitacionId:D3}",
                NroCamas = rh.Habitacion.NroCamas,
                TipoVip = rh.Habitacion.TipoVipNombre,
                PrecioNochePactado = rh.PrecioNochePactado
            }).ToList()
        };
    }

    public async Task<List<UsuarioComboViewModel>> GetUsuariosActivosAsync()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.CargoId != null)
            .Join(_context.Roles,
                usuario => usuario.CargoId,
                cargo => (long?)cargo.Id,
                (usuario, cargo) => new { usuario, cargo })
            .Where(x => x.cargo.NormalizedName == "USUARIO")
            .Select(x => new
            {
                x.usuario.UsuarioId,
                x.usuario.Nombres,
                x.usuario.Apellidos,
                CargoNombre = x.cargo.Name ?? "Usuario"
            })
            .ToListAsync();

        return usuarios
            .OrderBy(u => u.Nombres)
            .ThenBy(u => u.Apellidos)
            .Select(u => new UsuarioComboViewModel
            {
                UsuarioId = u.UsuarioId,
                NombreCompleto = $"{u.Nombres} {u.Apellidos}".Trim(),
                CargoNombre = u.CargoNombre
            })
            .ToList();
    }

    public async Task<List<HabitacionDisponibleViewModel>> GetHabitacionesDisponiblesAsync(DateTime inicio, DateTime fin)
    {
        // Obtener IDs de habitaciones ya reservadas en el rango de fechas
        var habitacionesOcupadas = await _context.ReservaHabitaciones
            .Include(rh => rh.Reserva)
            .Where(rh =>
                rh.Reserva.EstadoReserva != "CANCELADA" &&
                rh.Reserva.InicioReserva < fin &&
                rh.Reserva.FinReserva > inicio)
            .Select(rh => rh.HabitacionId)
            .ToListAsync();

        return await _context.Habitaciones
            .Where(h => h.Estado == "LIBRE" && !habitacionesOcupadas.Contains(h.HabitacionId))
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .Select(h => new HabitacionDisponibleViewModel
            {
                HabitacionId = h.HabitacionId,
                Codigo = $"{h.NroPiso}{h.TipoVip.Sigla}-{h.HabitacionId:D3}",
                NroCamas = h.NroCamas,
                TipoVip = h.TipoVipNombre,
                PrecioCama = h.TipoVip.PrecioCama
            })
            .ToListAsync();
    }

    public async Task<bool> CrearAsync(ReservaCreateModel model)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                if (model.HabitacionIds.Count == 0 || model.HabitacionIds.Distinct().Count() != model.HabitacionIds.Count)
                    return false;

                var esUsuarioRegistrado = await _context.Usuarios
                    .Join(_context.Roles,
                        usuario => usuario.CargoId,
                        cargo => (long?)cargo.Id,
                        (usuario, cargo) => new { usuario.UsuarioId, cargo.NormalizedName })
                    .AnyAsync(x => x.UsuarioId == model.UsuarioId && x.NormalizedName == "USUARIO");
                if (!esUsuarioRegistrado) return false;

                var inicio = model.InicioReserva.Date;
                var fin = inicio.AddDays(model.TotalNoches);
                var ocupadas = await _context.ReservaHabitaciones
                    .Where(rh => rh.Reserva.EstadoReserva != "CANCELADA"
                        && rh.Reserva.InicioReserva < fin && rh.Reserva.FinReserva > inicio)
                    .Select(rh => rh.HabitacionId).ToListAsync();
                var disponibles = await _context.Habitaciones
                    .Where(h => model.HabitacionIds.Contains(h.HabitacionId)
                        && h.Estado == "LIBRE" && !ocupadas.Contains(h.HabitacionId))
                    .Select(h => h.HabitacionId).ToListAsync();
                if (disponibles.Count != model.HabitacionIds.Count) return false;

                var reserva = new Reserva
                {
                    ReservaId = (await _context.Reservas.MaxAsync(r => (long?)r.ReservaId) ?? 0) + 1,
                    UsuarioId = model.UsuarioId,
                    EstadoReserva = model.EstadoReserva,
                    FechaReservaCreacion = DateTime.Now,
                    InicioReserva = inicio,
                    TotalNoches = model.TotalNoches,
                    FinReserva = fin
                };

                _context.Reservas.Add(reserva);
                await _context.SaveChangesAsync();

                var preciosNoche = await _context.Habitaciones
                    .Where(h => model.HabitacionIds.Contains(h.HabitacionId))
                    .Select(h => new { h.HabitacionId, Precio = h.NroCamas * h.TipoVip.PrecioCama })
                    .ToDictionaryAsync(h => h.HabitacionId, h => h.Precio);

                for (int i = 0; i < model.HabitacionIds.Count; i++)
                {
                    _context.ReservaHabitaciones.Add(new ReservaHabitacion
                    {
                        ReservaId = reserva.ReservaId,
                        HabitacionId = model.HabitacionIds[i],
                        PrecioNochePactado = preciosNoche[model.HabitacionIds[i]]
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            });
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EditarAsync(long id, ReservaEditModel model)
    {
        if (!new[] { "PENDIENTE", "CONFIRMADA", "FINALIZADA", "CANCELADA" }.Contains(model.EstadoReserva))
            return false;
        var reserva = await _context.Reservas
            .Include(r => r.ReservaHabitaciones)
            .FirstOrDefaultAsync(r => r.ReservaId == id);

        if (reserva is null) return false;

        try
        {
            reserva.EstadoReserva = model.EstadoReserva;

            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EliminarAsync(long id)
    {
        var reserva = await _context.Reservas
            .Include(r => r.ReservaHabitaciones)
            .FirstOrDefaultAsync(r => r.ReservaId == id);

        if (reserva is null || reserva.EstadoReserva != "PENDIENTE") return false;

        _context.ReservaHabitaciones.RemoveRange(reserva.ReservaHabitaciones);
        _context.Reservas.Remove(reserva);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<List<string>> GetEstadosDisponiblesAsync() =>
        Task.FromResult(new List<string> { "PENDIENTE", "CONFIRMADA", "FINALIZADA", "CANCELADA" });

    private static string GetEstadoEtiqueta(string estado) => estado switch
    {
        "CONFIRMADA" => "Confirmada",
        "FINALIZADA" => "Finalizada",
        "CANCELADA" => "Cancelada",
        _ => "Pendiente"
    };

    private static string GetBadgeEstado(string estado) => estado switch
    {
        "CONFIRMADA" => "bg-secondary-fixed text-on-secondary-fixed",
        "FINALIZADA" => "bg-surface-container text-on-tertiary-container",
        "CANCELADA" => "bg-error-container text-on-error-container",
        _ => "bg-surface-container-low text-on-surface"
    };
}
