using Hoteleria_App.Security;
using Hoteleria_App.Services;

namespace Hoteleria_App.Api;

/// <summary>API REST para integraciones externas. Comparte sesión y permisos con la aplicación.</summary>
public static class ApiEndpoints
{
    public static void MapApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization(HotelPermissionPolicies.Ver).WithTags("Hotel");

        api.MapGet("/habitaciones", async (string? q, IBuscadorHabitaciones buscador, CancellationToken ct) =>
            Results.Ok(new { motor = buscador.UsaAlgolia ? "algolia" : "postgresql", resultados = await buscador.BuscarAsync(q, ct: ct) }))
            .WithName("BuscarHabitaciones")
            .WithSummary("Busca en el catálogo de habitaciones.");

        api.MapGet("/disponibilidad", async (DateTime inicio, int noches, IReservaService reservas) =>
        {
            if (noches is < 1 or > 365) return Results.BadRequest(new { error = "noches debe estar entre 1 y 365." });
            var entrada = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Unspecified);
            return Results.Ok(await reservas.GetHabitacionesDisponiblesAsync(entrada, entrada.AddDays(noches)));
        })
            .WithName("ConsultarDisponibilidad")
            .WithSummary("Habitaciones disponibles para unas fechas.");
    }
}
