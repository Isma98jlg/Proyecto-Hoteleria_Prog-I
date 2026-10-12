using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Services;

public interface IHabitacionService
{
    Task<List<Habitacion>> GetAllAsync();
    Task<Habitacion?> GetByIdAsync(long id);
    Task<List<Habitacion>> GetByEstadoAsync(string estado);
    Task<List<Habitacion>> GetByPisoAsync(long nroPiso);
    Task<List<Habitacion>> GetByTipoVipAsync(string tipoVip);
}

public class HabitacionService : IHabitacionService
{
    private readonly HoteleriaDbContext _context;

    public HabitacionService(HoteleriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<Habitacion>> GetAllAsync()
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<Habitacion?> GetByIdAsync(long id)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .FirstOrDefaultAsync(h => h.HabitacionId == id);
    }

    public async Task<List<Habitacion>> GetByEstadoAsync(string estado)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.Estado == estado)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<List<Habitacion>> GetByPisoAsync(long nroPiso)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.NroPiso == nroPiso)
            .OrderBy(h => h.HabitacionId)
            .ToListAsync();
    }

    public async Task<List<Habitacion>> GetByTipoVipAsync(string tipoVip)
    {
        return await _context.Habitaciones
            .Include(h => h.Piso)
            .Include(h => h.TipoVip)
            .Where(h => h.TipoVipNombre == tipoVip)
            .OrderBy(h => h.NroPiso)
            .ThenBy(h => h.HabitacionId)
            .ToListAsync();
    }
}