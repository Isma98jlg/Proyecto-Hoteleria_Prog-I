using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Data;

/// <summary>
/// Datos semilla de referencia (idempotente). Espejo de Resources/DDBB/init-docker.sql:
/// Pisos, Tipos Vip, Habitaciones y usuario admin.
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
}
