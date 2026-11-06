using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hoteleria_App.Data;

/// <summary>
/// Factory de diseño (solo `dotnet ef`). Evita ejecutar el arranque real de la app
/// al generar migraciones. `migrations add` no se conecta a esta dirección.
/// </summary>
public class HoteleriaDbContextFactory : IDesignTimeDbContextFactory<HoteleriaDbContext>
{
    public HoteleriaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HoteleriaDbContext>()
            .UseNpgsql("Host=localhost;Database=hotel")
            .Options;

        return new HoteleriaDbContext(options);
    }
}
