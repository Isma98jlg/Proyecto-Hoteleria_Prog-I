using System.ComponentModel.DataAnnotations;

namespace Hoteleria_App.Services;

/// <summary>
/// Dominio único de los correos de acceso (DOMINIO_CORREO). Estos correos identifican la cuenta,
/// pero no son buzones reales: para escribir a una persona se usa su correo de contacto.
/// </summary>
public sealed class DominioCorreo(IConfiguration config)
{
    public string Dominio { get; } = Normalizar(config["DOMINIO_CORREO"]);

    public bool Admite(string? email)
    {
        var correo = email?.Trim() ?? "";
        var arroba = correo.LastIndexOf('@');
        return arroba > 0 && correo[(arroba + 1)..].Equals(Dominio, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Conserva lo que va antes de la @ y cambia el dominio.</summary>
    public string Convertir(string email)
    {
        var correo = email.Trim();
        var arroba = correo.LastIndexOf('@');
        var local = arroba > 0 ? correo[..arroba] : correo;
        return $"{local}@{Dominio}".ToLowerInvariant();
    }

    private static string Normalizar(string? dominio)
    {
        var valor = dominio?.Trim().TrimStart('@').ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(valor) && valor.Contains('.')
            ? valor
            : throw new InvalidOperationException("Falta la variable DOMINIO_CORREO o no es un dominio válido (ver .env.example).");
    }
}

/// <summary>Exige que el correo pertenezca al dominio único del sistema.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CorreoDelDominioAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Required se ocupa de los campos vacíos.
        if (value is not string correo || string.IsNullOrWhiteSpace(correo)) return ValidationResult.Success;

        var dominio = validationContext.GetRequiredService<DominioCorreo>();
        return dominio.Admite(correo)
            ? ValidationResult.Success
            : new ValidationResult($"Use un correo @{dominio.Dominio}.", validationContext.MemberName is { } miembro ? [miembro] : null);
    }
}
