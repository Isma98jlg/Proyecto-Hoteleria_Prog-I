using Hoteleria_App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Data;

/// <summary>
/// Datos semilla de referencia y cuentas de demostración (idempotentes).
/// </summary>
public static class DbSeeder
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
                new TipoVip { TipoVipNombre = "SUPERIOR", Sigla = 'S', PrecioCama = 80.00m },
                new TipoVip { TipoVipNombre = "SUITE", Sigla = 'U', PrecioCama = 150.00m },
                new TipoVip { TipoVipNombre = "PRESIDENCIAL", Sigla = 'P', PrecioCama = 300.00m });
        }

        await db.SaveChangesAsync();

        if (!await db.Habitaciones.AnyAsync())
        {
            db.Habitaciones.AddRange(
                new Habitacion { HabitacionId = 101, NroPiso = 1, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 102, NroPiso = 1, NroCamas = 1, TipoVipNombre = "ESTANDAR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 103, NroPiso = 1, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 201, NroPiso = 2, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 202, NroPiso = 2, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 203, NroPiso = 2, NroCamas = 1, TipoVipNombre = "SUITE", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 301, NroPiso = 3, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 302, NroPiso = 3, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 303, NroPiso = 3, NroCamas = 2, TipoVipNombre = "SUITE", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 401, NroPiso = 4, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 402, NroPiso = 4, NroCamas = 2, TipoVipNombre = "SUITE", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 501, NroPiso = 5, NroCamas = 2, TipoVipNombre = "PRESIDENCIAL", Estado = "LIBRE" },
                new Habitacion { HabitacionId = 502, NroPiso = 5, NroCamas = 3, TipoVipNombre = "PRESIDENCIAL", Estado = "LIBRE" });
        }

        if (!await db.Usuarios.AnyAsync())
        {
            db.Usuarios.Add(new Usuario
            {
                UsuarioId = 1,
                TipoDocumento = "DNI",
                NumeroDocumento = "12345678",
                Nombres = "Admin",
                Apellidos = "Hoteleria",
                Nacionalidad = "Local",
                Telefono = "555-0000",
                EMail = "admin@hoteleria.com"
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Crea cargos y cuentas de demostración para validar cada perfil de permisos.
    /// </summary>
    public static async Task SeedAccessAccountsAsync(HoteleriaDbContext db, UserManager<HotelUser> userManager,
        RoleManager<HotelRole> roleManager, string adminEmail, string adminPassword,
        string workerEmail, string workerPassword, string userEmail, string userPassword)
    {
        var userRole = await EnsureRoleAsync(roleManager, "usuario");
        var workerRole = await EnsureRoleAsync(roleManager, "trabajador");
        var adminRole = await EnsureRoleAsync(roleManager, "admin");

        await EnsureAccountAsync(db, userManager, 1, adminEmail, adminPassword, "Admin", "Hoteleria", adminRole);
        await EnsureAccountAsync(db, userManager, 2, workerEmail, workerPassword, "Trabajador", "Demostracion", workerRole);
        await EnsureAccountAsync(db, userManager, 3, userEmail, userPassword, "Usuario", "Demostracion", userRole);
    }

    private static async Task EnsureAccountAsync(HoteleriaDbContext db, UserManager<HotelUser> userManager,
        long userId, string email, string password, string firstName, string lastName, HotelRole role)
    {
        var person = await db.Usuarios.SingleOrDefaultAsync(u => u.UsuarioId == userId);
        if (person is null)
        {
            person = new Usuario
            {
                UsuarioId = userId,
                TipoDocumento = "DEMO",
                NumeroDocumento = $"DEMO{userId}",
                Nombres = firstName,
                Apellidos = lastName,
                Nacionalidad = "Local",
                Telefono = "0000000000",
                EMail = email
            };
            db.Usuarios.Add(person);
        }
        person.EMail = email;
        person.CargoId = role.Id;

        if (!await db.Employees.AnyAsync(e => e.UsuarioId == userId))
            db.Employees.Add(new Employee { UsuarioId = userId, FechaContratacion = DateTime.UtcNow.Date, Sueldo = 0m });
        await db.SaveChangesAsync();

        var account = await userManager.FindByIdAsync(userId.ToString());
        if (account is null)
        {
            account = new HotelUser { Id = userId, UserName = email, Email = email, EmailConfirmed = true, Usuario = person };
            var result = await userManager.CreateAsync(account, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"No se pudo crear la cuenta {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        else if (!string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            account.UserName = email;
            account.Email = email;
            var update = await userManager.UpdateAsync(account);
            if (!update.Succeeded)
                throw new InvalidOperationException($"No se pudo actualizar la cuenta {email}: " + string.Join("; ", update.Errors.Select(e => e.Description)));
        }

        foreach (var currentRole in await userManager.GetRolesAsync(account))
            if (!string.Equals(currentRole, role.Name, StringComparison.OrdinalIgnoreCase))
                await userManager.RemoveFromRoleAsync(account, currentRole);
        if (!await userManager.IsInRoleAsync(account, role.Name!))
        {
            var addRole = await userManager.AddToRoleAsync(account, role.Name!);
            if (!addRole.Succeeded)
                throw new InvalidOperationException($"No se pudo asignar el cargo {role.Name} a {email}: " + string.Join("; ", addRole.Errors.Select(e => e.Description)));
        }
    }

    private static async Task<HotelRole> EnsureRoleAsync(RoleManager<HotelRole> roleManager, string cargoNombre)
    {
        var role = await roleManager.FindByNameAsync(cargoNombre);
        var isAdmin = cargoNombre == "admin";
        var isWorker = cargoNombre == "trabajador";
        if (role is not null) return role;

        role = new HotelRole
        {
            Name = cargoNombre,
            Ver = true,
            CrudRequerimientos = isAdmin || isWorker,
            CrudPisos = isAdmin,
            CrudUsuarios = isAdmin,
            CrudHabitaciones = isAdmin || isWorker,
            CrudTipoVip = isAdmin
        };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException($"No se pudo crear el cargo {cargoNombre}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return role;
    }
}
