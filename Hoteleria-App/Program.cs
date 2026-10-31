using Hoteleria_App.Data;
using Hoteleria_App.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// =============================================
// CONFIGURACIÓN DE BASE DE DATOS (OPCIONAL)
// =============================================
// Variable de entorno: DB_ENABLED=false  →  NO registra DbContext, usa servicio mock
// Por defecto: true (intenta conectar a SQL Server)
var useDatabase = GetUseDatabaseSetting(builder.Configuration);

if (useDatabase)
{
    var connectionString = BuildConnectionString(builder.Configuration);
    builder.Services.AddDbContext<HoteleriaDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            )));

    builder.Services.AddScoped<IHabitacionService, HabitacionService>();
}
else
{
    // Modo "sin BD" - usa implementación en memoria para desarrollo frontend
    builder.Services.AddScoped<IHabitacionService, MockHabitacionService>();
}

// =============================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

// =============================================
// APLICAR MIGRACIONES PENDIENTES + DATOS SEMILLA
// (Solo cuando hay base de datos configurada)
// =============================================
if (useDatabase && GetAutoMigrateSetting(builder.Configuration))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HoteleriaDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();

/// <summary>
/// Determina si se debe usar base de datos real.
/// Variable de entorno: DB_ENABLED (true/false, default: true)
/// appsettings.json: UseDatabase (true/false)
/// </summary>
static bool GetUseDatabaseSetting(IConfiguration config)
{
    var env = Environment.GetEnvironmentVariable("DB_ENABLED");
    if (!string.IsNullOrWhiteSpace(env))
        return bool.TryParse(env, out var b) && b;

    var cfg = config["UseDatabase"];
    if (!string.IsNullOrWhiteSpace(cfg))
        return bool.TryParse(cfg, out var b) && b;

    return true; // default: usar BD
}

/// <summary>
/// Determina si se aplican migraciones + datos semilla al arrancar.
/// Variable de entorno: DB_AUTO_MIGRATE (true/false, default: true).
/// Desactívala (false) cuando la BD ya se aprovisiona por el Dockerfile
/// (Resources/DDBB/Dockerfile + init-docker.sql) para no chocar con EF Core.
/// </summary>
static bool GetAutoMigrateSetting(IConfiguration config)
{
    var env = Environment.GetEnvironmentVariable("DB_AUTO_MIGRATE");
    if (!string.IsNullOrWhiteSpace(env))
        return bool.TryParse(env, out var b) && b;

    var cfg = config["AutoMigrate"];
    if (!string.IsNullOrWhiteSpace(cfg))
        return bool.TryParse(cfg, out var b) && b;

    return true; // default: migrar + semblar al arrancar
}

/// <summary>
/// Construye la connection string priorizando variables de entorno.
/// Variables esperadas:
///   - DB_SERVER (default: localhost,1433)
///   - DB_NAME   (default: HOTEL)
///   - DB_USER   (default: sa)
///   - DB_PASSWORD (requerido si DB_ENABLED=true)
///   - DB_TRUST_CERT (default: true)
///   - DB_CONNECTION_STRING (override total: si existe, ignora las demás)
/// </summary>
static string BuildConnectionString(IConfiguration config)
{
    // 1. Override total via env var (útil para Azure/conn strings completas)
    var fullConnStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    if (!string.IsNullOrWhiteSpace(fullConnStr))
        return fullConnStr;

    // 2. Construir desde partes
    var server   = Environment.GetEnvironmentVariable("DB_SERVER")   ?? config["DbSettings:Server"]   ?? "localhost,1433";
    var database = Environment.GetEnvironmentVariable("DB_NAME")     ?? config["DbSettings:Database"] ?? "HOTEL";
    var user     = Environment.GetEnvironmentVariable("DB_USER")     ?? config["DbSettings:User"]     ?? "sa";
    var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? config["DbSettings:Password"];
    var trustCert = Environment.GetEnvironmentVariable("DB_TRUST_CERT") ?? config["DbSettings:TrustServerCertificate"] ?? "true";

    if (string.IsNullOrWhiteSpace(password))
    {
        var fallback = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        throw new InvalidOperationException(
            "DB_PASSWORD no configurado. Define variable de entorno DB_PASSWORD " +
            "o ConnectionStrings:DefaultConnection en appsettings.json");
    }

    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
    {
        DataSource = server,
        InitialCatalog = database,
        UserID = user,
        Password = password,
        TrustServerCertificate = bool.TryParse(trustCert, out var b) && b,
        ConnectTimeout = 30,
        Encrypt = false // true solo si usas certificado válido en prod
    };

    return builder.ConnectionString;
}