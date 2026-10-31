using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hoteleria_App.Data;

/// <summary>
/// Factory de diseño (solo `dotnet ef`). Evita ejecutar el arranque real de la app
/// al generar migraciones. No se conecta a la BD para `migrations add`.
/// </summary>
public class HoteleriaDbContextFactory : IDesignTimeDbContextFactory<HoteleriaDbContext>
{
    public HoteleriaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HoteleriaDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=HOTEL;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;")
            .Options;

        return new HoteleriaDbContext(options);
    }
}
