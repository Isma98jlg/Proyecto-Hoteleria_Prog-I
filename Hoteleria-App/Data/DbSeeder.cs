using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Data;

/// <summary>
/// Datos de referencia y cuentas iniciales (idempotente). No hay contraseñas por defecto:
/// una cuenta solo se crea si su contraseña llega por configuración.
/// </summary>
public static partial class DbSeeder
{
    public static async Task SeedAsync(HoteleriaDbContext db)
    {
        if (!await db.Pisos.AnyAsync())
        {
            db.Pisos.AddRange(
                new Piso { NroPiso = 1, RefPiso = "Planta Baja" },
                new Piso { NroPiso = 2, RefPiso = "Primer Piso" },
                new Piso { NroPiso = 3, RefPiso = "Segundo Piso" },
                new Piso { NroPiso = 4, RefPiso = "Tercer Piso" },
                new Piso { NroPiso = 5, RefPiso = "Piso VIP" });
        }

        if (!await db.TiposVip.AnyAsync())
        {
            db.TiposVip.AddRange(
                new TipoVip { TipoVipNombre = "ESTANDAR", Sigla = 'E', PrecioCama = 50.00m },
                new TipoVip { TipoVipNombre = "VIP", Sigla = 'V', PrecioCama = 150.00m });
        }

        await db.SaveChangesAsync();

        if (!await db.Habitaciones.AnyAsync())
        {
            db.Habitaciones.AddRange(
                Hab(101, 1, 2, "ESTANDAR"), Hab(102, 1, 1, "ESTANDAR"), Hab(103, 1, 2, "VIP"),
                Hab(201, 2, 2, "ESTANDAR"), Hab(202, 2, 2, "VIP"), Hab(203, 2, 1, "VIP"),
                Hab(301, 3, 2, "ESTANDAR"), Hab(302, 3, 2, "VIP"), Hab(303, 3, 2, "VIP"),
                Hab(401, 4, 2, "VIP"), Hab(402, 4, 2, "VIP"),
                Hab(501, 5, 2, "VIP"), Hab(502, 5, 3, "VIP"));
        }

        await db.SaveChangesAsync();
    }

    private static Habitacion Hab(long id, long piso, int camas, string tipo) =>
        new() { HabitacionId = id, NroPiso = piso, NroCamas = camas, TipoVipNombre = tipo, Estado = EstadosHabitacion.Libre };

    public static async Task EnsureRolesAsync(RoleManager<HotelRole> roleManager)
    {
        await EnsureRoleAsync(roleManager, CargosSistema.Huesped);
        await EnsureRoleAsync(roleManager, CargosSistema.Trabajador);
        await EnsureRoleAsync(roleManager, CargosSistema.Admin);
    }

    /// <summary>Crea una cuenta inicial si no existe. Las contraseñas existentes no se tocan.</summary>
    public static async Task EnsureAccountAsync(HoteleriaDbContext db, UserManager<HotelUser> userManager,
        RoleManager<HotelRole> roleManager, string email, string password, string nombres, string apellidos,
        string cargoNombre, bool esPersonal)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var cargo = await roleManager.FindByNameAsync(cargoNombre)
            ?? throw new InvalidOperationException($"Falta el cargo {cargoNombre}.");

        // Ficha provisional: la propia persona completa sus datos reales en «Mis datos».
        var tipoDocumento = cargoNombre == CargosSistema.Admin ? "SISTEMA" : "DEMO";
        var numeroDocumento = $"{tipoDocumento}-{email}";
        var persona = new Usuario
        {
            TipoDocumento = tipoDocumento,
            NumeroDocumento = numeroDocumento[..Math.Min(25, numeroDocumento.Length)],
            Nombres = nombres,
            Apellidos = apellidos,
            Nacionalidad = "Local",
            Telefono = "0000000000",
            EMail = email,
            CargoId = cargo.Id
        };
        db.Usuarios.Add(persona);
        await db.SaveChangesAsync();

        if (esPersonal)
        {
            db.Employees.Add(new Employee { UsuarioId = persona.UsuarioId, FechaContratacion = DateTime.UtcNow.Date, Sueldo = 0m });
            await db.SaveChangesAsync();
        }

        var cuenta = new HotelUser { Id = persona.UsuarioId, UserName = email, Email = email, EmailConfirmed = true };
        var creada = await userManager.CreateAsync(cuenta, password);
        if (!creada.Succeeded)
        {
            db.Usuarios.Remove(persona);
            await db.SaveChangesAsync();
            throw new InvalidOperationException($"No se pudo crear la cuenta {email}: " + string.Join("; ", creada.Errors.Select(e => e.Description)));
        }

        var rol = await userManager.AddToRoleAsync(cuenta, cargoNombre);
        if (!rol.Succeeded)
            throw new InvalidOperationException($"No se pudo asignar el cargo {cargoNombre} a {email}: " + string.Join("; ", rol.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Pasa al dominio único las personas e invitaciones que aún usen otro. El correo anterior queda
    /// como correo de contacto y la contraseña no cambia. Si el correo nuevo ya existe, no se toca la cuenta.
    /// </summary>
    public static async Task ConvertirCorreosAsync(HoteleriaDbContext db, UserManager<HotelUser> userManager, DominioCorreo dominio, ILogger logger)
    {
        var personas = await db.Usuarios.ToListAsync();
        var ocupados = personas.Select(u => u.EMail.ToLowerInvariant()).ToHashSet();

        foreach (var persona in personas.Where(u => !dominio.Admite(u.EMail)))
        {
            var nuevo = dominio.Convertir(persona.EMail);
            if (!ocupados.Add(nuevo))
            {
                logger.LogWarning("No se convirtió la persona {UsuarioId}: el correo {Correo} ya existe.", persona.UsuarioId, nuevo);
                continue;
            }

            persona.CorreoContacto ??= persona.EMail;
            persona.EMail = nuevo;
            await db.SaveChangesAsync();

            var cuenta = await userManager.FindByIdAsync(persona.UsuarioId.ToString());
            if (cuenta is null) continue;
            cuenta.Email = nuevo;
            cuenta.UserName = nuevo;
            var actualizada = await userManager.UpdateAsync(cuenta);
            if (!actualizada.Succeeded)
                throw new InvalidOperationException($"No se pudo convertir la cuenta {persona.UsuarioId}: " + string.Join("; ", actualizada.Errors.Select(e => e.Description)));
        }

        var invitaciones = await db.Invitaciones.Where(i => i.UsadaEn == null).ToListAsync();
        foreach (var invitacion in invitaciones.Where(i => !dominio.Admite(i.Email)))
            invitacion.Email = dominio.Convertir(invitacion.Email);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureRoleAsync(RoleManager<HotelRole> roleManager, string cargoNombre)
    {
        if (await roleManager.FindByNameAsync(cargoNombre) is not null) return;

        var esAdmin = cargoNombre == CargosSistema.Admin;
        var esPersonal = esAdmin || cargoNombre == CargosSistema.Trabajador;
        var role = new HotelRole
        {
            Name = cargoNombre,
            Ver = esPersonal, // El huésped solo accede a sus reservas.
            CrudRequerimientos = esPersonal,
            CrudPisos = esAdmin,
            CrudUsuarios = esPersonal, // Recepción registra huéspedes; el personal solo lo gestiona admin.
            CrudHabitaciones = esPersonal,
            CrudTipoVip = esAdmin
        };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException($"No se pudo crear el cargo {cargoNombre}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
