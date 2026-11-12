using System.Globalization;

namespace Hoteleria_App.Services;

/// <summary>
/// Los filtros de una página funcionan como una sola búsqueda: cualquier enlace o redirección parte de todos los
/// filtros activos (la consulta actual) y solo cambia el que corresponde. Así, elegir un piso no borra la búsqueda
/// escrita, y buscar no borra el piso elegido.
/// </summary>
public static class FiltrosActuales
{
    private static readonly HashSet<string> Ignorados = new(StringComparer.OrdinalIgnoreCase) { "handler", "modal", "id", "nroPiso", "__RequestVerificationToken" };

    /// <param name="cambios">Filtros que cambian; un valor null o vacío lo quita.</param>
    /// <param name="conservarPagina">La página se conserva tras una acción, pero vuelve a la 1 al cambiar un filtro.</param>
    public static Dictionary<string, string> Con(HttpRequest request, bool conservarPagina, params (string Clave, object? Valor)[] cambios)
    {
        var filtros = request.Query
            .Where(q => !Ignorados.Contains(q.Key) && !string.IsNullOrWhiteSpace(q.Value)
                && (conservarPagina || !q.Key.Equals("Pagina", StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (clave, valor) in cambios)
        {
            var texto = Convert.ToString(valor, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(texto)) filtros.Remove(clave);
            else filtros[clave] = texto;
        }
        return filtros;
    }

    public static Dictionary<string, string> Con(HttpRequest request, params (string Clave, object? Valor)[] cambios) =>
        Con(request, conservarPagina: false, cambios);
}
