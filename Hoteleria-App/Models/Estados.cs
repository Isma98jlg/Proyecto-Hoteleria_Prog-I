namespace Hoteleria_App.Models;

public static class EstadosHabitacion
{
    public const string Libre = "LIBRE";
    public const string Ocupado = "OCUPADO";
    public const string Mantenimiento = "MANTENIMIENTO";
}

/// <summary>Cargos que la aplicación necesita para funcionar; no se pueden eliminar.</summary>
public static class CargosSistema
{
    public const string Admin = "admin";
    public const string Trabajador = "trabajador";
    public const string Huesped = "huesped";

    public static bool DeSistema(string? nombre) => nombre is Admin or Trabajador or Huesped;
}

public static class CodigoHabitacion
{
    public static string Formatear(long nroPiso, char sigla, long habitacionId) => $"{nroPiso}{sigla}-{habitacionId:D3}";
}
