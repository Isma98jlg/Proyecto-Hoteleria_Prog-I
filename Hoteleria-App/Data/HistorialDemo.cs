using System.Globalization;
using System.Text;
using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Data;

/// <summary>
/// Historial de demostración para ML.NET, solo con <c>DEMO_HISTORIAL=true</c>: 36 huéspedes con nombres comunes
/// en Perú, sus reservas pasadas (finalizadas) y las búsquedas que las originaron. Cada huésped sigue un perfil
/// (ahorro, confort o lujo) para que los modelos tengan algo real que aprender.
/// Los DNI usan el rango 99xxxxxx, que RENIEC aún no asigna, y no llevan correo de contacto: no corresponden a
/// nadie. Se crea una sola vez.
/// </summary>
public static partial class DbSeeder
{
    private const string PrefijoDniDemo = "99";
    private const int HuespedesDemo = 36;

    private static readonly string[] NombresMujer =
        ["María Elena", "Rosa María", "Ana Lucía", "Carmen", "Patricia", "Lucía", "Fiorella", "Valeria", "Gabriela",
         "Milagros", "Daniela", "Claudia", "Sofía", "Andrea", "Jimena", "Paola", "Silvia", "Karina"];
    private static readonly string[] NombresHombre =
        ["José Luis", "Carlos Alberto", "Luis Miguel", "Jorge", "Juan Carlos", "Miguel Ángel", "César", "Ricardo",
         "Fernando", "Diego", "Renzo", "Alonso", "Sebastián", "Gonzalo", "Martín", "Víctor", "Andrés", "Raúl"];
    private static readonly string[] ApellidosComunes =
        ["Quispe", "Flores", "Rodríguez", "García", "Mamani", "Huamán", "Chávez", "Rojas", "Vargas", "Ramírez", "Torres",
         "Mendoza", "Castillo", "Salazar", "Gutiérrez", "Díaz", "Paredes", "Cárdenas", "Herrera", "Medina", "Vásquez",
         "Zevallos", "Benavides", "Delgado", "Núñez", "Ponce"];

    private sealed record HabitacionDemo(long Id, int Camas, long Piso, string Tipo, decimal PrecioNoche);

    public static async Task HistorialDemoAsync(HoteleriaDbContext db, DominioCorreo dominio, ILogger logger)
    {
        if (await db.Usuarios.AnyAsync(u => u.TipoDocumento == TiposDocumento.Dni && u.NumeroDocumento.StartsWith(PrefijoDniDemo))) return;

        var cargoHuesped = await db.Roles.Where(r => r.Name == CargosSistema.Huesped).Select(r => r.Id).SingleAsync();
        var habitaciones = await db.Habitaciones.AsNoTracking()
            .Select(h => new HabitacionDemo(h.HabitacionId, h.NroCamas, h.NroPiso, h.TipoVipNombre, h.NroCamas * h.TipoVip.PrecioCama))
            .ToListAsync();
        if (habitaciones.Count < 2) return;

        // Perfiles: qué habitaciones prefiere cada tipo de huésped (si alguno queda vacío, vale cualquiera).
        var precioMedio = habitaciones.Select(h => h.PrecioNoche).Order().ElementAt(habitaciones.Count / 2);
        var pisoAlto = habitaciones.Max(h => h.Piso) - 1;
        List<HabitacionDemo>[] perfiles =
        [
            habitaciones.Where(h => h.PrecioNoche < precioMedio).ToList(),                     // ahorro
            habitaciones.Where(h => h.PrecioNoche >= precioMedio && h.Piso < pisoAlto).ToList(), // confort
            habitaciones.Where(h => h.PrecioNoche >= precioMedio && h.Piso >= pisoAlto).ToList() // lujo
        ];
        for (var i = 0; i < perfiles.Length; i++) if (perfiles[i].Count == 0) perfiles[i] = habitaciones;

        var azar = new Random(2026);
        var hoy = DateTime.UtcNow.Date;
        var correos = (await db.Usuarios.Select(u => u.EMail.ToLower()).ToListAsync()).ToHashSet();
        var dnis = new HashSet<string>();
        var reservas = 0;

        for (var n = 0; n < HuespedesDemo; n++)
        {
            var nombres = n % 2 == 0 ? NombresMujer[azar.Next(NombresMujer.Length)] : NombresHombre[azar.Next(NombresHombre.Length)];
            var paterno = ApellidosComunes[azar.Next(ApellidosComunes.Length)];
            var materno = ApellidosComunes.Where(a => a != paterno).ElementAt(azar.Next(ApellidosComunes.Length - 1));
            string dni;
            do dni = PrefijoDniDemo + azar.Next(100000, 1000000).ToString(CultureInfo.InvariantCulture); while (!dnis.Add(dni));

            var baseCorreo = $"{SinTildes(nombres.Split(' ')[0])}.{SinTildes(paterno)}";
            var correo = $"{baseCorreo}@{dominio.Dominio}";
            for (var k = 2; correos.Contains(correo); k++) correo = $"{baseCorreo}{k}@{dominio.Dominio}";
            correos.Add(correo);

            var huesped = new Usuario
            {
                TipoDocumento = TiposDocumento.Dni,
                NumeroDocumento = dni,
                Nombres = nombres,
                Apellidos = $"{paterno} {materno}",
                Nacionalidad = "Peruana",
                Telefono = "9" + azar.Next(10000000, 100000000).ToString(CultureInfo.InvariantCulture),
                EMail = correo,
                CargoId = cargoHuesped
            };
            db.Usuarios.Add(huesped);

            var preferidas = perfiles[n % perfiles.Length];
            for (var r = azar.Next(3, 7); r > 0; r--)
            {
                // El 85 % de las veces elige según su perfil; el resto, cualquier habitación.
                var elegida = azar.NextDouble() < 0.85 ? preferidas[azar.Next(preferidas.Count)] : habitaciones[azar.Next(habitaciones.Count)];
                var inicio = DateTime.SpecifyKind(hoy.AddDays(-azar.Next(15, 420)), DateTimeKind.Unspecified);
                var noches = azar.Next(1, 6);
                var buscadaEn = DateTime.SpecifyKind(inicio.AddDays(-azar.Next(1, 40)).AddHours(azar.Next(8, 22)).AddMinutes(azar.Next(60)), DateTimeKind.Utc);
                var categoria = azar.NextDouble() < 0.3 ? elegida.Tipo : null;

                // Lo que vio: como hace el selector, habitaciones con tantas camas como personas (y su categoría,
                // si la eligió), en el orden de siempre, por piso.
                var mostradas = habitaciones.Where(h => h.Camas == elegida.Camas && (categoria is null || h.Tipo == categoria))
                    .OrderBy(h => h.Piso).ThenBy(h => h.Id).ToList();

                huesped.Reservas.Add(new Reserva
                {
                    EstadoReserva = EstadosReserva.Finalizada,
                    FechaReservaCreacion = buscadaEn.AddMinutes(azar.Next(2, 15)),
                    InicioReserva = inicio,
                    TotalNoches = noches,
                    ReservaHabitaciones =
                    [
                        new ReservaHabitacion
                        {
                            HabitacionId = elegida.Id, PrecioNochePactado = elegida.PrecioNoche,
                            Inicio = inicio, Fin = inicio.AddDays(noches), Activa = false
                        }
                    ]
                });
                db.Busquedas.Add(new Busqueda
                {
                    Usuario = huesped,
                    CreadaEn = buscadaEn,
                    Inicio = inicio,
                    Noches = noches,
                    Personas = elegida.Camas,
                    Categoria = categoria,
                    ConTexto = false,
                    ReservadaEn = buscadaEn.AddMinutes(azar.Next(2, 15)),
                    Resultados = mostradas.Select((h, i) => new BusquedaResultado { HabitacionId = h.Id, Posicion = i + 1, Elegida = h.Id == elegida.Id }).ToList()
                });
                reservas++;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Historial de demostración creado: {Huespedes} huéspedes y {Reservas} reservas pasadas con su búsqueda.", HuespedesDemo, reservas);
    }

    private static string SinTildes(string texto) =>
        new string(texto.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();
}
