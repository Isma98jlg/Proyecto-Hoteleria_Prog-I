using Hoteleria_App.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hoteleria_App.Hubs;

/// <summary>Avisos en tiempo real. Los mensajes llevan solo identificadores y estados, sin datos personales.</summary>
[Authorize]
public sealed class HotelHub(IAuthorizationService authorization) : Hub
{
    public const string GrupoPersonal = "personal";

    public override async Task OnConnectedAsync()
    {
        if (Context.User is not null &&
            (await authorization.AuthorizeAsync(Context.User, HotelPermissionPolicies.CrudRequerimientos)).Succeeded)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoPersonal);
        }
        await base.OnConnectedAsync();
    }
}

public interface INotificadorHotel
{
    Task ReservaActualizadaAsync(long reservaId, string estado, long usuarioId);
    Task HabitacionActualizadaAsync(long habitacionId, string estado);
}

public sealed class NotificadorSignalR(IHubContext<HotelHub> hub) : INotificadorHotel
{
    public async Task ReservaActualizadaAsync(long reservaId, string estado, long usuarioId)
    {
        var mensaje = new { reservaId, estado };
        await hub.Clients.Group(HotelHub.GrupoPersonal).SendAsync("reservaActualizada", mensaje);
        await hub.Clients.User(usuarioId.ToString()).SendAsync("reservaPropiaActualizada", mensaje);
    }

    public Task HabitacionActualizadaAsync(long habitacionId, string estado) =>
        hub.Clients.Group(HotelHub.GrupoPersonal).SendAsync("habitacionActualizada", new { habitacionId, estado });
}
