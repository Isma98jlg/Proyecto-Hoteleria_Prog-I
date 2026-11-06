using Hoteleria_App.Models;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Portal;

/// <summary>Clima de la zona del huésped, por ubicación del navegador o por ciudad.</summary>
[Authorize(Roles = CargosSistema.Huesped)]
public class ClimaModel(ClimaService clima) : PageModel
{
    private const int LargoMaximoCiudad = 60;

    public async Task<IActionResult> OnGetAsync(double? lat, double? lon, string? ciudad)
    {
        var ct = HttpContext.RequestAborted;
        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            if (ciudad.Length > LargoMaximoCiudad) return BadRequest();
            return await clima.PorCiudadAsync(ciudad, ct) is { } porCiudad
                ? new JsonResult(porCiudad)
                : NotFound(new { error = "No encontramos esa ciudad o el servicio no está disponible." });
        }

        if (lat is not double latitud || lon is not double longitud || !ClimaService.CoordenadasValidas(latitud, longitud)) return BadRequest();
        return await clima.PorCoordenadasAsync(latitud, longitud, ct: ct) is { } porUbicacion
            ? new JsonResult(porUbicacion)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "El servicio del clima no está disponible en este momento." });
    }
}
