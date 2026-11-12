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

/// <summary>
/// Precios en soles con un formato fijo, «S/ 1,500.00», igual en el servidor (Linux o Windows) y en el navegador
/// (selector-habitaciones.js usa el mismo).
/// </summary>
public static class Soles
{
    public static string Formatear(decimal monto) =>
        "S/ " + monto.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
}

public static class CodigoHabitacion
{
    public static string Formatear(long nroPiso, char sigla, long habitacionId) => $"{nroPiso}{sigla}-{habitacionId:D3}";

    /// <summary>El número empieza por su piso y sigue con dos dígitos: en el piso 4 van del 401 al 499.</summary>
    public static bool EncajaConPiso(long numero, long nroPiso) => numero / 100 == nroPiso && numero % 100 >= 1;

    public static string Rango(long nroPiso) => $"{nroPiso * 100 + 1} al {nroPiso * 100 + 99}";
}

/// <summary>Documentos que se aceptan en el registro de huéspedes.</summary>
public static class TiposDocumento
{
    public const string Dni = "DNI";
    public const string Pasaporte = "Pasaporte";
    public const string Cpp = "CPP";
    public const string Ce = "CE";

    public static readonly IReadOnlyList<string> Todos = [Dni, Pasaporte, Cpp, Ce];

    public static string Etiqueta(string tipo) => tipo switch
    {
        Dni => "DNI",
        Pasaporte => "Pasaporte o documento de viaje",
        Cpp => "Carné de Permiso Temporal de Permanencia (CPP)",
        Ce => "Carné de extranjería",
        _ => tipo
    };
}

/// <summary>Hasta dónde se comprobó el documento de una persona.</summary>
public static class EstadosVerificacion
{
    public const string SinVerificar = "SIN_VERIFICAR";
    /// <summary>El número es coherente para ese país, pero nadie confirmó que exista.</summary>
    public const string FormatoValido = "FORMATO_VALIDO";
    /// <summary>Una fuente oficial confirmó el documento y los nombres declarados.</summary>
    public const string Confirmado = "CONFIRMADO";

    public static string Etiqueta(string estado) => estado switch
    {
        Confirmado => "Confirmado",
        FormatoValido => "Formato válido",
        _ => "Sin verificar"
    };
}
