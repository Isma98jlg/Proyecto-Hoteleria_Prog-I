namespace Hoteleria_App.Models;

/// <summary>
/// Una búsqueda de habitaciones hecha por un huésped en el portal. Es la materia prima del ranking: guarda qué se
/// pidió (fechas, personas, categoría) y, en <see cref="Resultados"/>, qué se le mostró y qué terminó reservando.
/// No guarda el texto escrito, solo si lo hubo.
/// </summary>
public class Busqueda
{
    public long BusquedaId { get; set; }
    public long UsuarioId { get; set; }
    public DateTime CreadaEn { get; set; }
    public DateTime Inicio { get; set; }
    public int Noches { get; set; }
    public int? Personas { get; set; }
    public string? Categoria { get; set; }
    public bool ConTexto { get; set; }

    /// <summary>Cuándo terminó en reserva. Sin reserva, la búsqueda no enseña nada al ranking y se borra antes.</summary>
    public DateTime? ReservadaEn { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
    public virtual ICollection<BusquedaResultado> Resultados { get; set; } = new List<BusquedaResultado>();
}

/// <summary>Una habitación mostrada en una búsqueda: en qué posición salió y si fue la elegida.</summary>
public class BusquedaResultado
{
    public long BusquedaId { get; set; }
    public long HabitacionId { get; set; }
    public int Posicion { get; set; }
    public bool Elegida { get; set; }

    public virtual Busqueda Busqueda { get; set; } = null!;
    public virtual Habitacion Habitacion { get; set; } = null!;
}
