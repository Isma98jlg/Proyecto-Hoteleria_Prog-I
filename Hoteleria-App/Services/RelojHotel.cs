namespace Hoteleria_App.Services;

/// <summary>
/// Fecha y hora en la zona horaria del hotel (HOTEL_TIMEZONE). Evita que «hoy» dependa
/// del servidor: en Render el contenedor funciona en UTC.
/// </summary>
public sealed class RelojHotel(IConfiguration config)
{
    private readonly TimeZoneInfo _zona = ObtenerZona(config["HOTEL_TIMEZONE"]);

    public DateTime Ahora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zona);
    public DateTime Hoy => DateTime.SpecifyKind(Ahora.Date, DateTimeKind.Unspecified);
    public DateTime ALocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zona);

    private static TimeZoneInfo ObtenerZona(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
