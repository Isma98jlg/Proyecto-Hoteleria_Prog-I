using Hoteleria_App.Security;
using Hoteleria_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Chat;

[Authorize(Policy = HotelPermissionPolicies.CrudRequerimientos)]
public class IndexModel(CopilotoService copiloto) : PageModel
{
    [BindProperty] public string? Mensaje { get; set; }

    public ResultadoCopiloto? Resultado { get; private set; }
    public bool Disponible => copiloto.Disponible;

    public void OnGet() { }

    public async Task OnPostAsync(CancellationToken ct)
    {
        Resultado = await copiloto.ProponerAsync(Mensaje ?? "", ct);
    }
}
