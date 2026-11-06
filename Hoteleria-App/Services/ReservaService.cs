using System.ComponentModel.DataAnnotations;
using Hoteleria_App.Data;
using Hoteleria_App.Hubs;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hoteleria_App.Services;

public sealed record ResultadoOperacion(bool Ok, string? Error = null)
{
    public static readonly ResultadoOperacion Exito = new(true);
    public static ResultadoOperacion Fallo(string error) => new(false, error);
}

public interface IReservaService
{
    Task<List<ReservaViewModel>> GetAllAsync(string? buscar = null, string? estado = null, long? usuarioId = null);
    Task<ReservaDetalleViewModel?> GetByIdAsync(long id);
    Task<List<UsuarioComboViewModel>> GetHuespedesAsync();
    Task<List<HabitacionDisponibleViewModel>> GetHabitacionesDisponiblesAsync(DateTime inicio, DateTime fin, long? excluirReservaId = null);
    Task<ResultadoOperacion> CrearAsync(ReservaCreateModel model);
    Task<ResultadoOperacion> EditarAsync(long id, ReservaEditModel model);
    Task<ResultadoOperacion> CancelarPropiaAsync(long reservaId, long usuarioId);
    Task<ResultadoOperacion> EliminarAsync(long id);
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
    public long UsuarioId { get; set; }
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
    public string ReferenciaPiso { get; set; } = "";
    public decimal PrecioNochePactado { get; set; }
}

public class UsuarioComboViewModel
{
    public long UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Documento { get; set; } = "";
}

public class HabitacionDisponibleViewModel
{
    public long HabitacionId { get; set; }
    public string Codigo { get; set; } = "";
    public int NroCamas { get; set; }
    public string TipoVip { get; set; } = "";
    public decimal PrecioCama { get; set; }
    public decimal PrecioNoche => NroCamas * PrecioCama;
}

public class ReservaCreateModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione un huésped.")]
    public long UsuarioId { get; set; }
    [DataType(DataType.Date)]
    public DateTime InicioReserva { get; set; }
    [Range(1, 365, ErrorMessage = "El número de noches debe estar entre 1 y 365.")]
    public int TotalNoches { get; set; }
    public string EstadoReserva { get; set; } = EstadosReserva.Pendiente;
    public List<long> HabitacionIds { get; set; } = new();
}

public class ReservaEditModel
{
    public string EstadoReserva { get; set; } = "";
    [DataType(DataType.Date)]
    public DateTime? InicioReserva { get; set; }
    [Range(1, 365, ErrorMessage = "El número de noches debe estar entre 1 y 365.")]
    public int? TotalNoches { get; set; }
    public List<long> HabitacionIds { get; set; } = new();
}

public class ReservaService(HoteleriaDbContext context, RelojHotel reloj, INotificadorHotel notificador, ILogger<ReservaService> logger) : IReservaService
{
    private const string MensajeNoDisponible = "Alguna habitación ya no está disponible en esas fechas.";

    public async Task<List<ReservaViewModel>> GetAllAsync(string? buscar = null, string? estado = null, long? usuarioId = null)
    {
        var query = context.Reservas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(r => r.EstadoReserva == estado);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Usuario.Nombres, patron) ||
                EF.Functions.ILike(r.Usuario.Apellidos, patron) ||
                EF.Functions.ILike(r.Usuario.NumeroDocumento, patron));
        }

        if (usuarioId.HasValue)
            query = query.Where(r => r.UsuarioId == usuarioId.Value);

        return await query
            .OrderByDescending(r => r.FechaReservaCreacion)
            .Select(r => new ReservaViewModel
            {
                ReservaId = r.ReservaId,
                UsuarioNombre = r.Usuario.Nombres + " " + r.Usuario.Apellidos,
                UsuarioDocumento = r.Usuario.NumeroDocumento,
                EstadoReserva = r.EstadoReserva,
                FechaReservaCreacion = r.FechaReservaCreacion,
                InicioReserva = r.InicioReserva,
                FinReserva = r.FinReserva,
                TotalNoches = r.TotalNoches,
                TotalHabitaciones = r.ReservaHabitaciones.Count,
                PrecioTotal = r.ReservaHabitaciones.Sum(rh => rh.PrecioNochePactado) * r.TotalNoches,
                EstadoEtiqueta = EstadosReserva.Etiqueta(r.EstadoReserva),
                BadgeEstado = GetBadgeEstado(r.EstadoReserva)
            })
            .ToListAsync();
    }

    public async Task<ReservaDetalleViewModel?> GetByIdAsync(long id)
    {
        var r = await context.Reservas.AsNoTracking()
            .Include(r => r.Usuario)
            .Include(r => r.ReservaHabitaciones).ThenInclude(rh => rh.Habitacion).ThenInclude(h => h.TipoVip)
            .Include(r => r.ReservaHabitaciones).ThenInclude(rh => rh.Habitacion).ThenInclude(h => h.Piso)
            .FirstOrDefaultAsync(r => r.ReservaId == id);

        if (r is null) return null;

        return new ReservaDetalleViewModel
        {
            ReservaId = r.ReservaId,
            UsuarioId = r.UsuarioId,
            UsuarioNombre = $"{r.Usuario.Nombres} {r.Usuario.Apellidos}".Trim(),
            UsuarioDocumento = r.Usuario.NumeroDocumento,
            UsuarioEmail = r.Usuario.EMail,
            EstadoReserva = r.EstadoReserva,
            FechaReservaCreacion = r.FechaReservaCreacion,
            InicioReserva = r.InicioReserva,
            FinReserva = r.FinReserva,
            TotalNoches = r.TotalNoches,
            PrecioTotal = r.ReservaHabitaciones.Sum(rh => rh.PrecioNochePactado) * r.TotalNoches,
            EstadoEtiqueta = EstadosReserva.Etiqueta(r.EstadoReserva),
            BadgeEstado = GetBadgeEstado(r.EstadoReserva),
            Habitaciones = r.ReservaHabitaciones.Select(rh => new ReservaHabitacionDetalle
            {
                HabitacionId = rh.HabitacionId,
                CodigoHabitacion = CodigoHabitacion.Formatear(rh.Habitacion.NroPiso, rh.Habitacion.TipoVip.Sigla, rh.Habitacion.HabitacionId),
                NroCamas = rh.Habitacion.NroCamas,
                TipoVip = rh.Habitacion.TipoVipNombre,
                ReferenciaPiso = rh.Habitacion.Piso.RefPiso ?? $"Piso {rh.Habitacion.NroPiso}",
                PrecioNochePactado = rh.PrecioNochePactado
            }).ToList()
        };
    }

    public async Task<List<UsuarioComboViewModel>> GetHuespedesAsync()
    {
        return await context.Usuarios.AsNoTracking()
            .Where(u => u.Cargo.Name == CargosSistema.Huesped)
            .OrderBy(u => u.Nombres).ThenBy(u => u.Apellidos)
            .Select(u => new UsuarioComboViewModel
            {
                UsuarioId = u.UsuarioId,
                NombreCompleto = u.Nombres + " " + u.Apellidos,
                Documento = u.NumeroDocumento
            })
            .ToListAsync();
    }

    public async Task<List<HabitacionDisponibleViewModel>> GetHabitacionesDisponiblesAsync(DateTime inicio, DateTime fin, long? excluirReservaId = null)
    {
        var hoy = reloj.Hoy;
        var habitaciones = await HabitacionesLibresEn(inicio.Date, fin.Date, excluirReservaId, hoy)
            .AsNoTracking()
            .OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId)
            .Select(h => new { h.HabitacionId, h.NroPiso, h.TipoVip.Sigla, h.NroCamas, h.TipoVipNombre, h.TipoVip.PrecioCama })
            .ToListAsync();

        return habitaciones.Select(h => new HabitacionDisponibleViewModel
        {
            HabitacionId = h.HabitacionId,
            Codigo = CodigoHabitacion.Formatear(h.NroPiso, h.Sigla, h.HabitacionId),
            NroCamas = h.NroCamas,
            TipoVip = h.TipoVipNombre,
            PrecioCama = h.PrecioCama
        }).ToList();
    }

    /// <summary>
    /// Una habitación está disponible si ninguna reserva activa se solapa con las fechas.
    /// Su estado físico (ocupada, en mantenimiento) solo afecta a estancias que empiezan hoy.
    /// </summary>
    private IQueryable<Habitacion> HabitacionesLibresEn(DateTime inicio, DateTime fin, long? excluirReservaId, DateTime hoy)
    {
        var ocupadas = context.ReservaHabitaciones
            .Where(rh => rh.Activa && rh.Inicio < fin && rh.Fin > inicio
                && (excluirReservaId == null || rh.ReservaId != excluirReservaId))
            .Select(rh => rh.HabitacionId);

        return context.Habitaciones.Where(h =>
            !ocupadas.Contains(h.HabitacionId) && (inicio > hoy || h.Estado == EstadosHabitacion.Libre));
    }

    public async Task<ResultadoOperacion> CrearAsync(ReservaCreateModel model)
    {
        var inicio = DateTime.SpecifyKind(model.InicioReserva.Date, DateTimeKind.Unspecified);
        if (inicio < reloj.Hoy) return ResultadoOperacion.Fallo("La fecha de entrada no puede ser anterior a hoy.");
        if (model.TotalNoches is < 1 or > 365) return ResultadoOperacion.Fallo("El número de noches debe estar entre 1 y 365.");
        if (model.EstadoReserva is not (EstadosReserva.Pendiente or EstadosReserva.Confirmada))
            return ResultadoOperacion.Fallo("Seleccione un estado inicial válido.");
        if (model.HabitacionIds.Count == 0 || model.HabitacionIds.Distinct().Count() != model.HabitacionIds.Count)
            return ResultadoOperacion.Fallo("Seleccione al menos una habitación, sin repetirlas.");

        var fin = inicio.AddDays(model.TotalNoches);
        long reservaId = 0;
        var resultado = await EjecutarEnTransaccionAsync(async () =>
        {
            var esHuesped = await context.Usuarios.AnyAsync(u => u.UsuarioId == model.UsuarioId && u.Cargo.Name == CargosSistema.Huesped);
            if (!esHuesped) return ResultadoOperacion.Fallo("Seleccione un huésped registrado.");

            var habitaciones = await HabitacionesLibresEn(inicio, fin, null, reloj.Hoy)
                .Where(h => model.HabitacionIds.Contains(h.HabitacionId))
                .Select(h => new { h.HabitacionId, Precio = h.NroCamas * h.TipoVip.PrecioCama })
                .ToListAsync();
            if (habitaciones.Count != model.HabitacionIds.Count) return ResultadoOperacion.Fallo(MensajeNoDisponible);

            var reserva = new Reserva
            {
                UsuarioId = model.UsuarioId,
                EstadoReserva = model.EstadoReserva,
                FechaReservaCreacion = DateTime.UtcNow,
                InicioReserva = inicio,
                TotalNoches = model.TotalNoches,
                ReservaHabitaciones = habitaciones.Select(h => new ReservaHabitacion
                {
                    HabitacionId = h.HabitacionId,
                    PrecioNochePactado = h.Precio,
                    Inicio = inicio,
                    Fin = fin,
                    Activa = true
                }).ToList()
            };
            context.Reservas.Add(reserva);
            await context.SaveChangesAsync();
            reservaId = reserva.ReservaId;
            return ResultadoOperacion.Exito;
        });

        if (resultado.Ok) await NotificarReservaAsync(reservaId, model.EstadoReserva, model.UsuarioId);
        return resultado;
    }

    public async Task<ResultadoOperacion> EditarAsync(long id, ReservaEditModel model)
    {
        if (!EstadosReserva.Todos.Contains(model.EstadoReserva)) return ResultadoOperacion.Fallo("Seleccione un estado válido.");

        long usuarioId = 0;
        var cambiaronHabitaciones = new List<(long Id, string Estado)>();
        var resultado = await EjecutarEnTransaccionAsync(async () =>
        {
            cambiaronHabitaciones.Clear();
            var reserva = await context.Reservas
                .Include(r => r.ReservaHabitaciones).ThenInclude(rh => rh.Habitacion)
                .FirstOrDefaultAsync(r => r.ReservaId == id);
            if (reserva is null) return ResultadoOperacion.Fallo("La reserva no existe.");
            usuarioId = reserva.UsuarioId;

            var estancia = await AplicarCambiosDeEstanciaAsync(reserva, model);
            if (!estancia.Ok) return estancia;

            var estado = AplicarCambioDeEstado(reserva, model.EstadoReserva, cambiaronHabitaciones);
            if (!estado.Ok) return estado;

            await context.SaveChangesAsync();
            return ResultadoOperacion.Exito;
        });

        if (resultado.Ok)
        {
            await NotificarReservaAsync(id, model.EstadoReserva, usuarioId);
            foreach (var (habitacionId, estado) in cambiaronHabitaciones)
                await NotificarHabitacionAsync(habitacionId, estado);
        }
        return resultado;
    }

    public async Task<ResultadoOperacion> CancelarPropiaAsync(long reservaId, long usuarioId)
    {
        var propia = await context.Reservas.AsNoTracking().AnyAsync(r => r.ReservaId == reservaId && r.UsuarioId == usuarioId);
        if (!propia) return ResultadoOperacion.Fallo("La reserva no existe.");
        return await EditarAsync(reservaId, new ReservaEditModel { EstadoReserva = EstadosReserva.Cancelada });
    }

    public async Task<ResultadoOperacion> EliminarAsync(long id)
    {
        var reserva = await context.Reservas.Include(r => r.ReservaHabitaciones).FirstOrDefaultAsync(r => r.ReservaId == id);
        if (reserva is null) return ResultadoOperacion.Fallo("La reserva no existe.");
        if (reserva.EstadoReserva != EstadosReserva.Pendiente)
            return ResultadoOperacion.Fallo("Solo se eliminan reservas pendientes; las demás se cancelan para conservar el historial.");

        context.ReservaHabitaciones.RemoveRange(reserva.ReservaHabitaciones);
        context.Reservas.Remove(reserva);
        await context.SaveChangesAsync();
        await NotificarReservaAsync(id, "ELIMINADA", reserva.UsuarioId);
        return ResultadoOperacion.Exito;
    }

    /// <summary>Cambia fechas y habitaciones, conservando el precio pactado de las que siguen.</summary>
    private async Task<ResultadoOperacion> AplicarCambiosDeEstanciaAsync(Reserva reserva, ReservaEditModel model)
    {
        var inicio = model.InicioReserva is { } fecha ? DateTime.SpecifyKind(fecha.Date, DateTimeKind.Unspecified) : reserva.InicioReserva;
        var noches = model.TotalNoches ?? reserva.TotalNoches;
        var actuales = reserva.ReservaHabitaciones.Select(rh => rh.HabitacionId).ToHashSet();
        var nuevas = model.HabitacionIds.Count > 0 ? model.HabitacionIds.ToHashSet() : actuales;

        if (inicio == reserva.InicioReserva && noches == reserva.TotalNoches && nuevas.SetEquals(actuales))
            return ResultadoOperacion.Exito;

        if (!EstadosReserva.Editable(reserva.EstadoReserva))
            return ResultadoOperacion.Fallo("Las fechas y habitaciones solo se cambian antes del check-in.");
        if (inicio != reserva.InicioReserva && inicio < reloj.Hoy)
            return ResultadoOperacion.Fallo("La fecha de entrada no puede ser anterior a hoy.");
        if (noches is < 1 or > 365) return ResultadoOperacion.Fallo("El número de noches debe estar entre 1 y 365.");

        var fin = inicio.AddDays(noches);
        var disponibles = await HabitacionesLibresEn(inicio, fin, reserva.ReservaId, reloj.Hoy)
            .Where(h => nuevas.Contains(h.HabitacionId))
            .Select(h => new { h.HabitacionId, Precio = h.NroCamas * h.TipoVip.PrecioCama })
            .ToListAsync();
        if (disponibles.Count != nuevas.Count) return ResultadoOperacion.Fallo(MensajeNoDisponible);

        // Carga las habitaciones nuevas para que un check-in en este mismo cambio también las marque.
        await context.Habitaciones.Where(h => nuevas.Contains(h.HabitacionId)).LoadAsync();
        context.ReservaHabitaciones.RemoveRange(reserva.ReservaHabitaciones.Where(rh => !nuevas.Contains(rh.HabitacionId)));
        foreach (var linea in reserva.ReservaHabitaciones.Where(rh => nuevas.Contains(rh.HabitacionId)))
        {
            linea.Inicio = inicio;
            linea.Fin = fin;
        }
        foreach (var h in disponibles.Where(h => !actuales.Contains(h.HabitacionId)))
        {
            reserva.ReservaHabitaciones.Add(new ReservaHabitacion
            {
                HabitacionId = h.HabitacionId,
                PrecioNochePactado = h.Precio,
                Inicio = inicio,
                Fin = fin,
                Activa = true
            });
        }

        reserva.InicioReserva = inicio;
        reserva.TotalNoches = noches;
        return ResultadoOperacion.Exito;
    }

    /// <summary>Aplica la máquina de estados y mantiene sincronizado el estado de las habitaciones.</summary>
    private ResultadoOperacion AplicarCambioDeEstado(Reserva reserva, string nuevo, List<(long Id, string Estado)> habitacionesCambiadas)
    {
        if (reserva.EstadoReserva == nuevo) return ResultadoOperacion.Exito;

        if (!EstadosReserva.PuedeCambiar(reserva.EstadoReserva, nuevo))
            return ResultadoOperacion.Fallo($"Una reserva {EstadosReserva.Etiqueta(reserva.EstadoReserva).ToLowerInvariant()} no puede pasar a {EstadosReserva.Etiqueta(nuevo).ToLowerInvariant()}.");

        var lineas = reserva.ReservaHabitaciones.Where(rh => rh.Habitacion is not null).ToList();
        if (nuevo == EstadosReserva.EnCurso)
        {
            var hoy = reloj.Hoy;
            if (hoy < reserva.InicioReserva || hoy >= reserva.InicioReserva.AddDays(reserva.TotalNoches))
                return ResultadoOperacion.Fallo("El check-in solo se puede hacer dentro de las fechas de la reserva.");
            if (lineas.Any(rh => rh.Habitacion.Estado != EstadosHabitacion.Libre))
                return ResultadoOperacion.Fallo("Alguna habitación no está libre; revise su estado antes del check-in.");

            foreach (var linea in lineas)
            {
                linea.Habitacion.Estado = EstadosHabitacion.Ocupado;
                habitacionesCambiadas.Add((linea.HabitacionId, EstadosHabitacion.Ocupado));
            }
        }
        else if (nuevo == EstadosReserva.Finalizada)
        {
            foreach (var linea in lineas)
            {
                linea.Habitacion.Estado = EstadosHabitacion.Libre;
                habitacionesCambiadas.Add((linea.HabitacionId, EstadosHabitacion.Libre));
            }
        }

        reserva.EstadoReserva = nuevo;
        foreach (var linea in reserva.ReservaHabitaciones) linea.Activa = EstadosReserva.Activo(nuevo);
        return ResultadoOperacion.Exito;
    }

    private async Task<ResultadoOperacion> EjecutarEnTransaccionAsync(Func<Task<ResultadoOperacion>> operacion)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var resultado = await operacion();
                if (resultado.Ok) await transaction.CommitAsync();
                else context.ChangeTracker.Clear();
                return resultado;
            });
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // La restricción de la base de datos detuvo una reserva simultánea sobre las mismas fechas.
            context.ChangeTracker.Clear();
            return ResultadoOperacion.Fallo(MensajeNoDisponible);
        }
    }

    // Un fallo de notificación nunca debe deshacer una reserva ya guardada.
    private async Task NotificarReservaAsync(long reservaId, string estado, long usuarioId)
    {
        try { await notificador.ReservaActualizadaAsync(reservaId, estado, usuarioId); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo notificar la reserva {ReservaId}", reservaId); }
    }

    private async Task NotificarHabitacionAsync(long habitacionId, string estado)
    {
        try { await notificador.HabitacionActualizadaAsync(habitacionId, estado); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo notificar la habitación {HabitacionId}", habitacionId); }
    }

    public static string GetBadgeEstado(string estado) => estado switch
    {
        EstadosReserva.Confirmada => "bg-secondary-fixed text-on-secondary-fixed",
        EstadosReserva.EnCurso => "bg-tertiary-fixed text-on-tertiary-fixed-variant",
        EstadosReserva.Finalizada => "bg-surface-container text-on-tertiary-container",
        EstadosReserva.Cancelada => "bg-error-container text-on-error-container",
        _ => "bg-surface-container-low text-on-surface"
    };
}
