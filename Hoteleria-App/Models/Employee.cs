namespace Hoteleria_App.Models;

/// <summary>Extensión laboral de Usuario: comparte su clave primaria.</summary>
public class Employee
{
    public long UsuarioId { get; set; }
    public DateTime FechaContratacion { get; set; }
    public decimal Sueldo { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
}
