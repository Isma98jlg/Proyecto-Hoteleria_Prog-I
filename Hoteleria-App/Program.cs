using System.Threading.RateLimiting;
using Hoteleria_App.Api;
using Hoteleria_App.Data;
using Hoteleria_App.Hubs;
using Hoteleria_App.Models;
using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Carga variables locales desde .env (si existe). Precedencia: variables del sistema > .env > appsettings.
LoadDotEnv(builder.Configuration, builder.Environment.ContentRootPath);

var config = builder.Configuration;

builder.Services.AddRazorPages();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IAuthorizationHandler, HotelPermissionAuthorizationHandler>();

builder.Services.AddDbContext<HoteleriaDbContext>(options =>
    options.UseNpgsql(BuildConnectionString(config), npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

builder.Services.AddSingleton<RelojHotel>();
builder.Services.AddSingleton<DominioCorreo>();
builder.Services.AddSingleton<INotificadorHotel, NotificadorSignalR>();
builder.Services.AddScoped<PermisosService>();
builder.Services.AddScoped<IHabitacionService, HabitacionService>();
builder.Services.AddScoped<IReservaService, ReservaService>();
builder.Services.AddScoped<CuentaService>();
builder.Services.AddScoped<IBuscadorHabitaciones, BuscadorHabitaciones>();
builder.Services.AddScoped<SelectorHabitaciones>();
builder.Services.AddHttpClient<ClimaService>(cliente => cliente.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<LugaresService>(cliente => cliente.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddHttpClient<VerificacionDocumentoService>(cliente => cliente.Timeout = TimeSpan.FromSeconds(10));
// El cliente HTTP anota cada dirección consultada, y la de PeruAPI lleva el DNI: solo se registran sus avisos.
builder.Logging.AddFilter("System.Net.Http.HttpClient.VerificacionDocumentoService", LogLevel.Warning);
builder.Services.AddScoped<AsistenteHuespedService>();

// Redis es opcional: da caché compartida y conecta SignalR entre instancias.
var redis = BuildRedisOptions(config["REDIS_URL"]);
var signalR = builder.Services.AddSignalR();
// Con AbortOnConnectFail = false, esta llamada no falla si Redis no responde: sigue reintentando.
IConnectionMultiplexer? conexionRedis = redis is null ? null : await ConnectionMultiplexer.ConnectAsync(redis);
if (redis is not null && conexionRedis is not null)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConnectionMultiplexerFactory = () => Task.FromResult(conexionRedis);
        options.InstanceName = "hoteleria:";
    });
    signalR.AddStackExchangeRedis(redis.ToString(), options =>
        options.Configuration.ChannelPrefix = RedisChannel.Literal("hoteleria"));
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Las claves viven en la base de datos: el disco de un contenedor se pierde en cada despliegue,
// y con él las sesiones y los enlaces de restablecimiento.
builder.Services.AddDataProtection()
    .SetApplicationName("Hoteleria-App")
    .PersistKeysToDbContext<HoteleriaDbContext>();

builder.Services.AddIdentity<HotelUser, HotelRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<HoteleriaDbContext>()
    .AddDefaultTokenProviders();

// Un cambio de cargo o de contraseña se aplica a las sesiones abiertas en menos de un minuto.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

// Render termina TLS en su proxy y reenvía la petición por HTTP interno.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? "";
        var esEnvioSensible = HttpMethods.IsPost(context.Request.Method) &&
            (path.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
             || path.StartsWith("/Account/Registro", StringComparison.OrdinalIgnoreCase)
             || path.StartsWith("/Account/Invitacion", StringComparison.OrdinalIgnoreCase)
             || path.StartsWith("/Account/Restablecer", StringComparison.OrdinalIgnoreCase)
             || path.StartsWith("/Portal/Asistente", StringComparison.OrdinalIgnoreCase));
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        // Cada búsqueda de lugares puede consumir créditos de la API externa.
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Query.ContainsKey("handler")
            && path.StartsWith("/Portal/Explora", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"{ip}:explora", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            });
        }
        if (!esEnvioSensible) return RateLimitPartition.GetNoLimiter("sin-limite");

        return RateLimitPartition.GetFixedWindowLimiter($"{ip}:{path.ToLowerInvariant()}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = int.TryParse(config["RATE_LIMIT_POR_MINUTO"], out var limite) ? limite : 20,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

// Todo requiere autenticación salvo lo marcado como anónimo.
builder.Services.Configure<AuthorizationOptions>(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    AddPermissionPolicy(options, HotelPermissionPolicies.Ver, "ver");
    AddPermissionPolicy(options, HotelPermissionPolicies.CrudRequerimientos, "crud_requerimientos");
    AddPermissionPolicy(options, HotelPermissionPolicies.CrudPisos, "crud_pisos");
    AddPermissionPolicy(options, HotelPermissionPolicies.CrudUsuarios, "crud_usuarios");
    AddPermissionPolicy(options, HotelPermissionPolicies.CrudHabitaciones, "crud_habitaciones");
    AddPermissionPolicy(options, HotelPermissionPolicies.CrudTipoVip, "crud_tipo_vip");
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseVentanaFlotante();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<HotelHub>("/hubs/hotel");
app.MapApiV1();
app.MapOpenApi().AllowAnonymous();
// Redis es opcional: si falla se informa aquí, pero solo la base de datos marca el servicio como caído.
app.MapGet("/health", async (HoteleriaDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { estado = "ok", redis = await EstadoRedisAsync(conexionRedis) })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).AllowAnonymous();

if (GetBool(config, "DB_AUTO_MIGRATE", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HoteleriaDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HotelUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<HotelRole>>();

    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    await DbSeeder.EnsureRolesAsync(roleManager);

    var dominio = scope.ServiceProvider.GetRequiredService<DominioCorreo>();
    await DbSeeder.ConvertirCorreosAsync(db, userManager, dominio, app.Logger);

    // El administrador inicial solo se crea si no hay ninguno. Las variables no se vuelven a usar:
    // después, su nombre, correo y contraseña se cambian desde la aplicación.
    if (!await db.Usuarios.AnyAsync(u => u.Cargo.Name == CargosSistema.Admin))
    {
        await DbSeeder.EnsureAccountAsync(db, userManager, roleManager,
            CorreoPermitido(config, "ADMIN_EMAIL", dominio), Requerida(config, "ADMIN_PASSWORD"),
            "Administrador", "del Sistema", CargosSistema.Admin, esPersonal: true);
    }

    // Cuentas de demostración: solo si se piden de forma explícita.
    if (GetBool(config, "DEMO_ACCOUNTS", false))
    {
        await DbSeeder.EnsureAccountAsync(db, userManager, roleManager,
            CorreoPermitido(config, "DEMO_WORKER_EMAIL", dominio), Requerida(config, "DEMO_WORKER_PASSWORD"),
            "Trabajador", "Demostracion", CargosSistema.Trabajador, esPersonal: true);
        await DbSeeder.EnsureAccountAsync(db, userManager, roleManager,
            CorreoPermitido(config, "DEMO_USER_EMAIL", dominio), Requerida(config, "DEMO_USER_PASSWORD"),
            "Huesped", "Demostracion", CargosSistema.Huesped, esPersonal: false);
    }
}

// Sin bloquear el arranque: si Algolia falla, la búsqueda usa PostgreSQL.
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IBuscadorHabitaciones>().SincronizarAsync();
});

app.Run();

static async Task<object> EstadoRedisAsync(IConnectionMultiplexer? conexion)
{
    if (conexion is null) return new { estado = "no_configurado" };
    if (!conexion.IsConnected) return new { estado = "sin_conexion" };
    try
    {
        var latencia = await conexion.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromSeconds(2));
        return new { estado = "conectado", latenciaMs = Math.Round(latencia.TotalMilliseconds, 1) };
    }
    catch (Exception ex) when (ex is RedisException or TimeoutException)
    {
        return new { estado = "sin_conexion" };
    }
}

static void AddPermissionPolicy(AuthorizationOptions options, string policy, string permission) =>
    options.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new HotelPermissionRequirement(permission)));

static string Requerida(IConfiguration config, string clave)
{
    var valor = config[clave];
    return !string.IsNullOrWhiteSpace(valor)
        ? valor
        : throw new InvalidOperationException($"Falta la variable de entorno {clave} (ver .env.example).");
}

static string CorreoPermitido(IConfiguration config, string clave, DominioCorreo dominio)
{
    var correo = Requerida(config, clave);
    return dominio.Admite(correo)
        ? correo
        : throw new InvalidOperationException($"{clave} debe ser un correo @{dominio.Dominio}.");
}

/// <summary>
/// Carga un archivo .env (clave=valor) con menor precedencia que las variables del sistema.
/// En producción no existe: la configuración llega por variables de entorno.
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

        if (Environment.GetEnvironmentVariable(key) is not null) continue;

        values[key] = value;
    }

    if (values.Count > 0)
        config.AddInMemoryCollection(values);
}

/// <summary>Busca .env desde el content root hacia arriba (repositorio/.env con la app en una subcarpeta).</summary>
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

static bool GetBool(IConfiguration config, string key, bool defaultValue)
{
    var value = config[key];
    return !string.IsNullOrWhiteSpace(value) && bool.TryParse(value, out var parsed)
        ? parsed
        : defaultValue;
}

/// <summary>
/// Convierte DATABASE_URL (postgres://usuario:clave@host:puerto/base, el formato de Render)
/// en una cadena de conexión de Npgsql. No hay valor por defecto: sin ella la app no arranca.
/// </summary>
static string BuildConnectionString(IConfiguration config)
{
    var url = Requerida(config, "DATABASE_URL");
    if (!url.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)) return url;

    var uri = new Uri(url);
    var credenciales = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(credenciales[0]),
        Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null
    };
    // La URL puede fijar el modo con ?sslmode=disable. Si no lo hace, las direcciones internas de
    // Render (sin dominio) y las locales lo negocian, y cualquier otro servidor remoto exige TLS.
    var sslmode = System.Web.HttpUtility.ParseQueryString(uri.Query)["sslmode"];
    var esInterna = uri.Host is "localhost" or "127.0.0.1" || !uri.Host.Contains('.');
    builder.SslMode = Enum.TryParse<SslMode>(sslmode, ignoreCase: true, out var modo)
        ? modo
        : esInterna ? SslMode.Prefer : SslMode.Require;
    return builder.ConnectionString;
}

/// <summary>Acepta redis://[:clave@]host:puerto y rediss:// (TLS), el formato de Render Key Value.</summary>
static ConfigurationOptions? BuildRedisOptions(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;

    var uri = new Uri(url);
    var options = new ConfigurationOptions
    {
        Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
        AbortOnConnectFail = false,
        // Sin conexión, los comandos fallan al instante y se consulta la base de datos.
        // Si esperasen en cola, cada página tardaría más de 10 segundos con Redis caído.
        BacklogPolicy = BacklogPolicy.FailFast
    };
    options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);
    if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        var partes = uri.UserInfo.Split(':', 2);
        if (partes.Length == 2)
        {
            if (partes[0].Length > 0) options.User = Uri.UnescapeDataString(partes[0]);
            options.Password = Uri.UnescapeDataString(partes[1]);
        }
        else
        {
            options.Password = Uri.UnescapeDataString(partes[0]);
        }
    }
    return options;
}
