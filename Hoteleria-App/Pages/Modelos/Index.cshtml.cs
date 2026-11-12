using Hoteleria_App.Data;
using Hoteleria_App.Models;
using Hoteleria_App.Services.Ml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Modelos;

/// <summary>Estado de los modelos de ML.NET y botón para reentrenarlos. Solo cifras: ningún dato personal.</summary>
[Authorize(Roles = CargosSistema.Admin)]
public class IndexModel(RecomendadorHabitaciones recomendador, OrdenadorHabitaciones ordenador, EntrenamientoModelos entrenamiento, HoteleriaDbContext db) : PageModel
{
    public EstadoRecomendador Recomendador => recomendador.Estado;
    public EstadoRanking Ranking => ordenador.Estado;
    public string? Error => entrenamiento.UltimoError;
    public int Busquedas { get; private set; }
    public int BusquedasConReserva { get; private set; }

    public async Task OnGetAsync()
    {
        Busquedas = await db.Busquedas.CountAsync();
        BusquedasConReserva = await db.Busquedas.CountAsync(b => b.ReservadaEn != null);
    }

    public async Task<IActionResult> OnPostEntrenarAsync()
    {
        await entrenamiento.EntrenarAsync(HttpContext.RequestAborted);
        TempData[entrenamiento.UltimoError is null ? "MensajeExito" : "MensajeError"] = entrenamiento.UltimoError is null
            ? "Modelos reentrenados con los datos actuales."
            : "No se pudieron entrenar los modelos. Revise el log del servidor.";
        return RedirectToPage();
    }
}
