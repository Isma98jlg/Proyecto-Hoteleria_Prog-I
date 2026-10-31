using Hoteleria_App.Data;
using Hoteleria_App.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Carga variables locales desde .env (si existe) como configuración de desarrollo.
// Precedencia: variables de entorno del SO > .env > appsettings.
LoadDotEnv(builder.Configuration, builder.Environment.ContentRootPath);

// Add services to the container.
builder.Services.AddRazorPages();

// =============================================
// CONFIGURACIÓN DE BASE DE DATOS (OPCIONAL)
// =============================================
// DB_ENABLED=false  →  NO registra DbContext, usa servicio mock (front-end sin BD).
// Por defecto: true (conecta a SQL Server vía DB_SERVER / DB_PASSWORD...).
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
    // Modo "sin BD" - usa implementación en memoria para desarrollo frontend.
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

// Aplica migraciones pendientes + datos semilla cuando hay BD configurada.
if (useDatabase && GetAutoMigrateSetting(builder.Configuration))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HoteleriaDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();

/// <summary>
/// Carga un archivo .env (clave=valor) en la configuración, con MENOR precedencia
/// que las variables reales del sistema (el SO gana sobre .env). Así Docker Compose
/// y la app comparten el mismo .env sin exportar variables a mano.
/// </summary>
static void LoadDotEnv(IConfigurationBuilder config, string contentRoot)
{
    var path = FindEnvFile(contentRoot);
    if (path is null) return;

    var values = new Dictionary<string, string?>();
    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line[0] == '#') continue;

        var eq = line.IndexOf('=');
        if (eq <= 0) continue;

        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim().Trim('"').Trim('\'');
        if (key.Length == 0) continue;

        // Las variables de entorno del SO tienen prioridad sobre .env.
        if (Environment.GetEnvironmentVariable(key) is not null) continue;

        values[key] = value;
    }

    if (values.Count > 0)
        config.AddInMemoryCollection(values);
}

/// <summary>
/// Localiza el archivo .env partiendo del content root y subiendo por los
/// directorios padre (cubre el caso repositorio/.env con la app en una subcarpeta).
/// </summary>
static string? FindEnvFile(string contentRoot)
{
    var dir = new DirectoryInfo(contentRoot);
    for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

/// <summary>
/// Determina si se debe usar base de datos real.
/// Lee DB_ENABLED (entorno o .env); por defecto true.
/// </summary>
static bool GetUseDatabaseSetting(IConfiguration config) =>
    GetBool(config, "DB_ENABLED", true);

/// <summary>
/// Determina si se aplican migraciones + datos semilla al arrancar.
/// Lee DB_AUTO_MIGRATE (entorno o .env); por defecto true.
/// Desactívala (false) cuando la BD ya se aprovisiona con el Dockerfile.
/// </summary>
static bool GetAutoMigrateSetting(IConfiguration config) =>
    GetBool(config, "DB_AUTO_MIGRATE", true);

static bool GetBool(IConfiguration config, string key, bool defaultValue)
{
    var value = config[key];
    return !string.IsNullOrWhiteSpace(value) && bool.TryParse(value, out var parsed)
        ? parsed
        : defaultValue;
}

/// <summary>
/// Construye la connection string a partir de variables de entorno / .env.
/// Variables: DB_CONNECTION_STRING (override total), DB_SERVER, DB_NAME, DB_USER,
/// DB_PASSWORD (obligatoria si hay BD), DB_TRUST_CERT.
/// </summary>
static string BuildConnectionString(IConfiguration config)
{
    // 1. Override total (connection string completa).
    var full = config["DB_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(full)) return full;

    // 2. Construir desde partes.
    var server   = config["DB_SERVER"]      ?? config["DbSettings:Server"]   ?? "localhost,1433";
    var database = config["DB_NAME"]        ?? config["DbSettings:Database"] ?? "HOTEL";
    var user     = config["DB_USER"]        ?? config["DbSettings:User"]     ?? "sa";
    var password = config["DB_PASSWORD"]    ?? config["DbSettings:Password"];
    var trust    = config["DB_TRUST_CERT"]  ?? config["DbSettings:TrustServerCertificate"] ?? "true";

    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "DB_PASSWORD no está configurada. Define DB_PASSWORD en las variables " +
            "de entorno o en el archivo .env (ver .env.example).");
    }

    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
    {
        DataSource = server,
        InitialCatalog = database,
        UserID = user,
        Password = password,
        TrustServerCertificate = bool.TryParse(trust, out var b) && b,
        ConnectTimeout = 30,
        Encrypt = false
    };

    return builder.ConnectionString;
}
