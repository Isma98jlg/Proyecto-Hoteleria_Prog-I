namespace Hoteleria_App.Models;

/// <summary>Hotel que el administrador decidió vigilar.</summary>
public class Competidor
{
    public long CompetidorId { get; set; }
    public string PropertyToken { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Ciudad { get; set; } = null!;
    public DateTime CreadoEn { get; set; }
}

/// <summary>
/// Foto de los precios del mercado en un momento dado. Se conserva para reutilizarla, contar las
/// búsquedas gastadas y tener historial. Solo contiene datos públicos de hoteles.
/// </summary>
public class ConsultaCompetencia
{
    public long ConsultaId { get; set; }
    public DateTime RealizadaEn { get; set; }
    public string Ciudad { get; set; } = null!;
    public DateTime Entrada { get; set; }
    public int Noches { get; set; }
    public int Adultos { get; set; }
    public string Resultado { get; set; } = "[]";
}
