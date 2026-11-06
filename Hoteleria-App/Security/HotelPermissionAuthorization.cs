using System.Security.Claims;
using System.Text.Json;
using Hoteleria_App.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

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

public sealed record PermisosCargo(bool Ver, bool CrudRequerimientos, bool CrudPisos, bool CrudUsuarios, bool CrudHabitaciones, bool CrudTipoVip);

/// <summary>
/// Permisos del usuario con caché distribuida (Redis si está configurado), para no
/// consultar la base de datos en cada comprobación de cada página.
/// </summary>
public sealed class PermisosService(HoteleriaDbContext db, IDistributedCache cache)
{
    private static readonly DistributedCacheEntryOptions Ttl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
    private readonly Dictionary<long, PermisosCargo?> _peticion = [];

    private static string Clave(long usuarioId) => $"permisos:{usuarioId}";

    public async Task<PermisosCargo?> ObtenerAsync(long usuarioId)
    {
        if (_peticion.TryGetValue(usuarioId, out var enPeticion)) return enPeticion;

        PermisosCargo? permisos = null;
        var guardado = await LeerCacheAsync(usuarioId);
        if (guardado is not null)
        {
            permisos = JsonSerializer.Deserialize<PermisosCargo>(guardado);
        }
        else
        {
            permisos = await db.Usuarios.AsNoTracking()
                .Where(u => u.UsuarioId == usuarioId)
                .Select(u => new PermisosCargo(u.Cargo.Ver, u.Cargo.CrudRequerimientos, u.Cargo.CrudPisos,
                    u.Cargo.CrudUsuarios, u.Cargo.CrudHabitaciones, u.Cargo.CrudTipoVip))
                .SingleOrDefaultAsync();
            if (permisos is not null) await EscribirCacheAsync(usuarioId, JsonSerializer.Serialize(permisos));
        }

        _peticion[usuarioId] = permisos;
        return permisos;
    }

    public async Task InvalidarAsync(long usuarioId)
    {
        _peticion.Remove(usuarioId);
        try { await cache.RemoveAsync(Clave(usuarioId)); } catch (Exception ex) when (EsFalloDeCache(ex)) { }
    }

    /// <summary>Tras editar un cargo, invalida a todas las personas que lo tienen.</summary>
    public async Task InvalidarCargoAsync(long cargoId)
    {
        var ids = await db.Usuarios.AsNoTracking().Where(u => u.CargoId == cargoId).Select(u => u.UsuarioId).ToListAsync();
        foreach (var id in ids) await InvalidarAsync(id);
    }

    // Si Redis cae, la app sigue funcionando contra la base de datos.
    private async Task<string?> LeerCacheAsync(long id)
    {
        try { return await cache.GetStringAsync(Clave(id)); } catch (Exception ex) when (EsFalloDeCache(ex)) { return null; }
    }

    private async Task EscribirCacheAsync(long id, string valor)
    {
        try { await cache.SetStringAsync(Clave(id), valor, Ttl); } catch (Exception ex) when (EsFalloDeCache(ex)) { }
    }

    private static bool EsFalloDeCache(Exception ex) => ex is StackExchange.Redis.RedisException or TimeoutException;
}

public sealed class HotelPermissionAuthorizationHandler(PermisosService permisosService)
    : AuthorizationHandler<HotelPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelPermissionRequirement requirement)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idValue, out var userId)) return;

        var permisos = await permisosService.ObtenerAsync(userId);
        if (permisos is null) return;

        var allowed = requirement.Permission switch
        {
            "ver" => permisos.Ver,
            "crud_requerimientos" => permisos.CrudRequerimientos,
            "crud_pisos" => permisos.CrudPisos,
            "crud_usuarios" => permisos.CrudUsuarios,
            "crud_habitaciones" => permisos.CrudHabitaciones,
            "crud_tipo_vip" => permisos.CrudTipoVip,
            _ => false
        };

        if (allowed) context.Succeed(requirement);
    }
}

public static class UsuarioActual
{
    public static long? Id(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
