using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services;

public class DatosPersona
{
    [Required(ErrorMessage = "Indique el tipo de documento."), StringLength(15)] public string TipoDocumento { get; set; } = "DNI";
    [Required(ErrorMessage = "Indique el número de documento."), StringLength(25)] public string NumeroDocumento { get; set; } = "";
    [Required(ErrorMessage = "Indique los nombres."), StringLength(100)] public string Nombres { get; set; } = "";
    [Required(ErrorMessage = "Indique los apellidos."), StringLength(100)] public string Apellidos { get; set; } = "";
    [Required(ErrorMessage = "Indique la nacionalidad."), StringLength(60)] public string Nacionalidad { get; set; } = "";
    [Required(ErrorMessage = "Indique el teléfono."), StringLength(25)] public string Telefono { get; set; } = "";
    [Required(ErrorMessage = "Indique el correo de acceso."), EmailAddress(ErrorMessage = "Correo no válido."), StringLength(254), CorreoDelDominio] public string EMail { get; set; } = "";
    [EmailAddress(ErrorMessage = "Correo no válido."), StringLength(254)] public string? CorreoContacto { get; set; }

    public void CopiarA(Usuario u)
    {
        u.TipoDocumento = TipoDocumento.Trim();
        u.NumeroDocumento = NumeroDocumento.Trim();
        u.Nombres = Nombres.Trim();
        u.Apellidos = Apellidos.Trim();
        u.Nacionalidad = Nacionalidad.Trim();
        u.Telefono = Telefono.Trim();
        u.EMail = EMail.Trim().ToLowerInvariant();
        u.CorreoContacto = string.IsNullOrWhiteSpace(CorreoContacto) ? null : CorreoContacto.Trim();
    }

    public static DatosPersona Desde(Usuario u) => new()
    {
        TipoDocumento = u.TipoDocumento,
        NumeroDocumento = u.NumeroDocumento,
        Nombres = u.Nombres,
        Apellidos = u.Apellidos,
        Nacionalidad = u.Nacionalidad,
        Telefono = u.Telefono,
        EMail = u.EMail,
        CorreoContacto = u.CorreoContacto
    };
}

public sealed record InvitacionVista(string Email, string Cargo, bool EsActivacion);

/// <summary>
/// Único punto que crea cuentas y asigna cargos, para que Usuarios.cargo_id y los roles
/// de Identity no puedan desincronizarse.
/// </summary>
public class CuentaService(
    HoteleriaDbContext db,
    UserManager<HotelUser> userManager,
    PermisosService permisos)
{
    private static readonly TimeSpan VigenciaInvitacion = TimeSpan.FromDays(7);

    public async Task<ResultadoOperacion> RegistrarHuespedAsync(DatosPersona datos, string password)
    {
        var duplicado = await ValidarDuplicadosAsync(datos, null);
        if (duplicado is not null) return ResultadoOperacion.Fallo(duplicado);

        var cargo = await db.Roles.AsNoTracking().SingleAsync(r => r.Name == CargosSistema.Huesped);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var usuario = new Usuario { CargoId = cargo.Id };
            datos.CopiarA(usuario);
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();

            var resultado = await CrearCuentaAsync(usuario, password, CargosSistema.Huesped);
            if (resultado.Ok) await transaction.CommitAsync();
            else db.ChangeTracker.Clear();
            return resultado;
        });
    }

    /// <summary>Alta sin cuenta hecha por recepción para quien llega al mostrador.</summary>
    public async Task<ResultadoOperacion> RegistrarHuespedSinCuentaAsync(DatosPersona datos)
    {
        var duplicado = await ValidarDuplicadosAsync(datos, null);
        if (duplicado is not null) return ResultadoOperacion.Fallo(duplicado);

        var cargo = await db.Roles.AsNoTracking().SingleAsync(r => r.Name == CargosSistema.Huesped);
        var usuario = new Usuario { CargoId = cargo.Id };
        datos.CopiarA(usuario);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return ResultadoOperacion.Exito;
    }

    public async Task<string?> ValidarDuplicadosAsync(DatosPersona datos, long? excluirId)
    {
        var documento = datos.NumeroDocumento.Trim();
        var tipo = datos.TipoDocumento.Trim();
        var email = datos.EMail.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.UsuarioId != excluirId && u.TipoDocumento == tipo && u.NumeroDocumento == documento))
            return "Ya existe una persona con ese documento. Si es usted, pida en recepción su enlace de activación.";
        if (await db.Usuarios.AnyAsync(u => u.UsuarioId != excluirId && u.EMail.ToLower() == email))
            return "Ya existe una persona con ese correo. Si es usted, pida en recepción su enlace de activación.";
        return null;
    }

    public async Task<string> CrearInvitacionPersonalAsync(string email, long cargoId, long creadaPorId)
    {
        return await GuardarInvitacionAsync(new Invitacion { Email = email.Trim(), CargoId = cargoId, CreadaPorId = creadaPorId });
    }

    public async Task<string?> CrearActivacionHuespedAsync(long usuarioId, long creadaPorId)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.UsuarioId == usuarioId);
        if (usuario is null || await userManager.FindByIdAsync(usuarioId.ToString()) is not null) return null;
        return await GuardarInvitacionAsync(new Invitacion { Email = usuario.EMail, CargoId = usuario.CargoId, UsuarioId = usuarioId, CreadaPorId = creadaPorId });
    }

    private async Task<string> GuardarInvitacionAsync(Invitacion invitacion)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        invitacion.TokenHash = Hash(token);
        invitacion.ExpiraEn = DateTime.UtcNow.Add(VigenciaInvitacion);
        db.Invitaciones.Add(invitacion);
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<InvitacionVista?> LeerInvitacionAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var ahora = DateTime.UtcNow;
        return await db.Invitaciones.AsNoTracking()
            .Where(i => i.TokenHash == hash && i.UsadaEn == null && i.ExpiraEn > ahora)
            .Select(i => new InvitacionVista(i.Email, i.Cargo.Name!, i.UsuarioId != null))
            .FirstOrDefaultAsync();
    }

    public async Task<ResultadoOperacion> AceptarInvitacionAsync(string token, DatosPersona? datos, string password)
    {
        var hash = Hash(token);
        var ahora = DateTime.UtcNow;
        var invitacion = await db.Invitaciones.AsNoTracking().Include(i => i.Cargo)
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.UsadaEn == null && i.ExpiraEn > ahora);
        if (invitacion is null) return ResultadoOperacion.Fallo("El enlace no es válido o ya caducó.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Marcar el token dentro de la transacción impide que dos peticiones lo usen a la vez.
            var marcadas = await db.Invitaciones
                .Where(i => i.InvitacionId == invitacion.InvitacionId && i.UsadaEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsadaEn, ahora));
            if (marcadas != 1) return ResultadoOperacion.Fallo("El enlace ya fue utilizado.");

            ResultadoOperacion resultado;
            if (invitacion.UsuarioId is long usuarioId)
            {
                var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == usuarioId);
                resultado = usuario is null
                    ? ResultadoOperacion.Fallo("La persona ya no existe.")
                    : await CrearCuentaAsync(usuario, password, invitacion.Cargo.Name!);
            }
            else
            {
                if (datos is null) return ResultadoOperacion.Fallo("Faltan sus datos personales.");
                datos.EMail = invitacion.Email; // El correo lo fija la invitación.
                var duplicado = await ValidarDuplicadosAsync(datos, null);
                if (duplicado is not null) return ResultadoOperacion.Fallo(duplicado);

                var usuario = new Usuario { CargoId = invitacion.CargoId };
                datos.CopiarA(usuario);
                db.Usuarios.Add(usuario);
                await db.SaveChangesAsync();
                if (invitacion.Cargo.Name != CargosSistema.Huesped)
                {
                    db.Employees.Add(new Employee { UsuarioId = usuario.UsuarioId, FechaContratacion = DateTime.UtcNow.Date, Sueldo = 0m });
                    await db.SaveChangesAsync();
                }
                resultado = await CrearCuentaAsync(usuario, password, invitacion.Cargo.Name!);
            }

            if (resultado.Ok) await transaction.CommitAsync();
            else db.ChangeTracker.Clear();
            return resultado;
        });
    }

    public async Task<ResultadoOperacion> AsignarCargoAsync(long usuarioId, long cargoId)
    {
        var usuario = await db.Usuarios.Include(u => u.Cargo).Include(u => u.Employee).FirstOrDefaultAsync(u => u.UsuarioId == usuarioId);
        var cargo = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == cargoId);
        if (usuario is null || cargo is null) return ResultadoOperacion.Fallo("La persona o el cargo no existen.");
        if (usuario.CargoId == cargoId) return ResultadoOperacion.Exito;

        if (usuario.Cargo.Name == CargosSistema.Admin && await db.Usuarios.CountAsync(u => u.CargoId == usuario.CargoId) <= 1)
            return ResultadoOperacion.Fallo("Debe quedar al menos un administrador.");

        var strategy = db.Database.CreateExecutionStrategy();
        var resultado = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            usuario.CargoId = cargoId;
            if (cargo.Name == CargosSistema.Huesped)
            {
                if (usuario.Employee is not null) db.Employees.Remove(usuario.Employee);
            }
            else if (usuario.Employee is null)
            {
                db.Employees.Add(new Employee { UsuarioId = usuarioId, FechaContratacion = DateTime.UtcNow.Date, Sueldo = 0m });
            }
            await db.SaveChangesAsync();

            var cuenta = await userManager.FindByIdAsync(usuarioId.ToString());
            if (cuenta is not null)
            {
                foreach (var rol in await userManager.GetRolesAsync(cuenta))
                    await userManager.RemoveFromRoleAsync(cuenta, rol);
                var agregado = await userManager.AddToRoleAsync(cuenta, cargo.Name!);
                if (!agregado.Succeeded) return ResultadoOperacion.Fallo(UnirErrores(agregado));
                // Fuerza a renovar la cookie, que guarda los roles.
                await userManager.UpdateSecurityStampAsync(cuenta);
            }
            await transaction.CommitAsync();
            return ResultadoOperacion.Exito;
        });

        if (resultado.Ok) await permisos.InvalidarAsync(usuarioId);
        return resultado;
    }

    /// <summary>Mantiene el correo de acceso igual al de la ficha de la persona.</summary>
    public async Task<ResultadoOperacion> SincronizarCorreoAsync(long usuarioId, string email)
    {
        var cuenta = await userManager.FindByIdAsync(usuarioId.ToString());
        if (cuenta is null || string.Equals(cuenta.Email, email, StringComparison.OrdinalIgnoreCase)) return ResultadoOperacion.Exito;
        cuenta.Email = email;
        cuenta.UserName = email;
        var actualizado = await userManager.UpdateAsync(cuenta);
        return actualizado.Succeeded ? ResultadoOperacion.Exito : ResultadoOperacion.Fallo(UnirErrores(actualizado));
    }

    public async Task<string?> CrearEnlaceRestablecimientoAsync(long usuarioId)
    {
        var cuenta = await userManager.FindByIdAsync(usuarioId.ToString());
        if (cuenta is null) return null;
        var token = await userManager.GeneratePasswordResetTokenAsync(cuenta);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public async Task<ResultadoOperacion> RestablecerAsync(long usuarioId, string codigo, string password)
    {
        var cuenta = await userManager.FindByIdAsync(usuarioId.ToString());
        if (cuenta is null) return ResultadoOperacion.Fallo("El enlace no es válido o ya caducó.");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(codigo)); }
        catch (FormatException) { return ResultadoOperacion.Fallo("El enlace no es válido o ya caducó."); }

        var resultado = await userManager.ResetPasswordAsync(cuenta, token, password);
        if (resultado.Succeeded) return ResultadoOperacion.Exito;
        return resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
            ? ResultadoOperacion.Fallo("El enlace no es válido o ya caducó.")
            : ResultadoOperacion.Fallo(UnirErrores(resultado));
    }

    private async Task<ResultadoOperacion> CrearCuentaAsync(Usuario usuario, string password, string cargoNombre)
    {
        if (await userManager.FindByIdAsync(usuario.UsuarioId.ToString()) is not null)
            return ResultadoOperacion.Fallo("Esta persona ya tiene una cuenta.");

        var cuenta = new HotelUser { Id = usuario.UsuarioId, UserName = usuario.EMail, Email = usuario.EMail, EmailConfirmed = true };
        var creada = await userManager.CreateAsync(cuenta, password);
        if (!creada.Succeeded) return ResultadoOperacion.Fallo(UnirErrores(creada));
        var rol = await userManager.AddToRoleAsync(cuenta, cargoNombre);
        return rol.Succeeded ? ResultadoOperacion.Exito : ResultadoOperacion.Fallo(UnirErrores(rol));
    }

    private static string UnirErrores(IdentityResult resultado) => string.Join(" ", resultado.Errors.Select(e => e.Description));

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
