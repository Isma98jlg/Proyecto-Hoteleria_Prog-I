using System.Security.Claims;
using Hoteleria_App.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Security;

public static class HotelPermissionPolicies
{
    public const string Ver = "hotel.ver";
    public const string CrudRequerimientos = "hotel.crud_requerimientos";
    public const string CrudPisos = "hotel.crud_pisos";
    public const string CrudUsuarios = "hotel.crud_usuarios";
    public const string CrudHabitaciones = "hotel.crud_habitaciones";
    public const string CrudTipoVip = "hotel.crud_tipo_vip";
}

public sealed record HotelPermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class HotelPermissionAuthorizationHandler(HoteleriaDbContext db)
    : AuthorizationHandler<HotelPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelPermissionRequirement requirement)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idValue, out var userId)) return;

        var cargoId = await db.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == userId)
            .Select(u => u.CargoId)
            .SingleOrDefaultAsync();
        if (!cargoId.HasValue) return;

        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == cargoId.Value);
        if (role is null) return;

        var allowed = requirement.Permission switch
        {
            "ver" => role.Ver,
            "crud_requerimientos" => role.CrudRequerimientos,
            "crud_pisos" => role.CrudPisos,
            "crud_usuarios" => role.CrudUsuarios,
            "crud_habitaciones" => role.CrudHabitaciones,
            "crud_tipo_vip" => role.CrudTipoVip,
            _ => false
        };

        if (allowed) context.Succeed(requirement);
    }
}
