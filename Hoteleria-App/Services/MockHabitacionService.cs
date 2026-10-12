using Hoteleria_App.Models;

namespace Hoteleria_App.Services;

/// <summary>
/// Implementación en memoria de IHabitacionService para desarrollo frontend sin BD.
/// Útil cuando DB_ENABLED=false.
/// </summary>
public class MockHabitacionService : IHabitacionService
{
    private readonly List<Habitacion> _habitaciones;

    public MockHabitacionService()
    {
        // Datos de prueba simulados (mismos que init-docker.sql)
        var pisos = new Dictionary<long, Piso>
        {
            [1] = new Piso { NroPiso = 1, RefPiso = "Planta Baja" },
            [2] = new Piso { NroPiso = 2, RefPiso = "Primer Piso" },
            [3] = new Piso { NroPiso = 3, RefPiso = "Segundo Piso" },
            [4] = new Piso { NroPiso = 4, RefPiso = "Tercer Piso" },
            [5] = new Piso { NroPiso = 5, RefPiso = "Piso VIP" }
        };

        var tiposVip = new Dictionary<string, TipoVip>
        {
            ["ESTANDAR"] = new TipoVip { TipoVipNombre = "ESTANDAR", Sigla = 'E', PrecioCama = 50.00m },
            ["SUPERIOR"] = new TipoVip { TipoVipNombre = "SUPERIOR", Sigla = 'S', PrecioCama = 80.00m },
            ["SUITE"] = new TipoVip { TipoVipNombre = "SUITE", Sigla = 'U', PrecioCama = 150.00m },
            ["PRESIDENCIAL"] = new TipoVip { TipoVipNombre = "PRESIDENCIAL", Sigla = 'P', PrecioCama = 300.00m }
        };

        _habitaciones = new List<Habitacion>
        {
            new Habitacion { HabitacionId = 101, NroPiso = 1, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE", Piso = pisos[1], TipoVip = tiposVip["ESTANDAR"] },
            new Habitacion { HabitacionId = 102, NroPiso = 1, NroCamas = 1, TipoVipNombre = "ESTANDAR", Estado = "LIBRE", Piso = pisos[1], TipoVip = tiposVip["ESTANDAR"] },
            new Habitacion { HabitacionId = 103, NroPiso = 1, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE", Piso = pisos[1], TipoVip = tiposVip["SUPERIOR"] },
            new Habitacion { HabitacionId = 201, NroPiso = 2, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE", Piso = pisos[2], TipoVip = tiposVip["ESTANDAR"] },
            new Habitacion { HabitacionId = 202, NroPiso = 2, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "OCUPADO", Piso = pisos[2], TipoVip = tiposVip["SUPERIOR"] },
            new Habitacion { HabitacionId = 203, NroPiso = 2, NroCamas = 1, TipoVipNombre = "SUITE", Estado = "LIBRE", Piso = pisos[2], TipoVip = tiposVip["SUITE"] },
            new Habitacion { HabitacionId = 301, NroPiso = 3, NroCamas = 2, TipoVipNombre = "ESTANDAR", Estado = "LIBRE", Piso = pisos[3], TipoVip = tiposVip["ESTANDAR"] },
            new Habitacion { HabitacionId = 302, NroPiso = 3, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE", Piso = pisos[3], TipoVip = tiposVip["SUPERIOR"] },
            new Habitacion { HabitacionId = 303, NroPiso = 3, NroCamas = 2, TipoVipNombre = "SUITE", Estado = "MANTENIMIENTO", Piso = pisos[3], TipoVip = tiposVip["SUITE"] },
            new Habitacion { HabitacionId = 401, NroPiso = 4, NroCamas = 2, TipoVipNombre = "SUPERIOR", Estado = "LIBRE", Piso = pisos[4], TipoVip = tiposVip["SUPERIOR"] },
            new Habitacion { HabitacionId = 402, NroPiso = 4, NroCamas = 2, TipoVipNombre = "SUITE", Estado = "LIBRE", Piso = pisos[4], TipoVip = tiposVip["SUITE"] },
            new Habitacion { HabitacionId = 501, NroPiso = 5, NroCamas = 2, TipoVipNombre = "PRESIDENCIAL", Estado = "LIBRE", Piso = pisos[5], TipoVip = tiposVip["PRESIDENCIAL"] },
            new Habitacion { HabitacionId = 502, NroPiso = 5, NroCamas = 3, TipoVipNombre = "PRESIDENCIAL", Estado = "LIBRE", Piso = pisos[5], TipoVip = tiposVip["PRESIDENCIAL"] }
        };
    }

    public Task<List<Habitacion>> GetAllAsync()
        => Task.FromResult(_habitaciones.OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId).ToList());

    public Task<Habitacion?> GetByIdAsync(long id)
        => Task.FromResult(_habitaciones.FirstOrDefault(h => h.HabitacionId == id));

    public Task<List<Habitacion>> GetByEstadoAsync(string estado)
        => Task.FromResult(_habitaciones.Where(h => h.Estado == estado).OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId).ToList());

    public Task<List<Habitacion>> GetByPisoAsync(long nroPiso)
        => Task.FromResult(_habitaciones.Where(h => h.NroPiso == nroPiso).OrderBy(h => h.HabitacionId).ToList());

    public Task<List<Habitacion>> GetByTipoVipAsync(string tipoVip)
        => Task.FromResult(_habitaciones.Where(h => h.TipoVipNombre == tipoVip).OrderBy(h => h.NroPiso).ThenBy(h => h.HabitacionId).ToList());
}