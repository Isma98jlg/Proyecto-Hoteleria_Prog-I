using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Mapa de lugares de interés para el huésped, por ubicación del navegador o por ciudad.</summary>
[Authorize(Roles = CargosSistema.Huesped)]
public class ExploraModel(LugaresService lugares, ClimaService clima) : PageModel
{
    public bool Disponible => lugares.Disponible;

    public void OnGet() { }

    /// <summary>Convierte una ciudad en coordenadas: no consume créditos de Geoapify.</summary>
    public async Task<IActionResult> OnGetCiudadAsync(string? ciudad)
    {
        if (string.IsNullOrWhiteSpace(ciudad) || ciudad.Length > 60) return BadRequest();
        return await clima.BuscarLugarAsync(ciudad, HttpContext.RequestAborted) is { } lugar
            ? new JsonResult(lugar)
            : NotFound(new { error = "No encontramos esa ciudad. Revise el nombre o use su ubicación." });
    }

    public async Task<IActionResult> OnGetBuscarAsync(double? lat, double? lon, int radio = 2_000, string? filtros = null, int desplazamiento = 0)
    {
        if (lat is not double latitud || lon is not double longitud || !ClimaService.CoordenadasValidas(latitud, longitud)
            || !LugaresService.Radios.Contains(radio) || desplazamiento is < 0 or > LugaresService.MaximoDesplazamiento
            || LugaresService.LeerFiltros(filtros) is not { } elegidos) return BadRequest();

        return await lugares.BuscarAsync(latitud, longitud, radio, elegidos, desplazamiento, HttpContext.RequestAborted) is { } resultado
            ? new JsonResult(resultado)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "La búsqueda de lugares no está disponible en este momento." });
    }
}
