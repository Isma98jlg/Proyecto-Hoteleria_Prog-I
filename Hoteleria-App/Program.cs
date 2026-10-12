using Hoteleria_App.Data;
using Hoteleria_App.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// DbContext - Connection string from env vars with fallback to appsettings
var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<HoteleriaDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )));

// Services
builder.Services.AddScoped<IHabitacionService, HabitacionService>();

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

app.Run();

/// <summary>
/// Construye la connection string priorizando variables de entorno.
/// Variables esperadas:
///   - DB_SERVER (default: localhost,1433)
///   - DB_NAME   (default: HOTEL)
///   - DB_USER   (default: sa)
///   - DB_PASSWORD (requerido, sin default por seguridad)
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