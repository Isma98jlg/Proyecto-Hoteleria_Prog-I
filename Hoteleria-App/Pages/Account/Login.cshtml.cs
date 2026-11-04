using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Hoteleria_App.Data;
using Hoteleria_App.Models;

namespace Hoteleria_App.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<HotelUser> _signInManager;
    private readonly HoteleriaDbContext _db;

    public LoginModel(SignInManager<HotelUser> signInManager, HoteleriaDbContext db)
    {
        _signInManager = signInManager;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Correo no válido.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (ModelState.IsValid)
        {
            var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
            var employeeExists = user is not null && await _db.Employees.AnyAsync(e => e.UsuarioId == user.Id);
            var result = employeeExists
                ? await _signInManager.PasswordSignInAsync(user!, Input.Password, Input.RememberMe, lockoutOnFailure: false)
                : Microsoft.AspNetCore.Identity.SignInResult.Failed;

            if (result.Succeeded)
            {
                var person = await _db.Usuarios.SingleAsync(u => u.UsuarioId == user!.Id);
                var cargo = person.CargoId.HasValue
                    ? await _db.Roles.SingleOrDefaultAsync(r => r.Id == person.CargoId.Value)
                    : null;
                var identityRoles = await _signInManager.UserManager.GetRolesAsync(user!);
                TempData["AuthLoginDebug"] = JsonSerializer.Serialize(new
                {
                    usuarioId = person.UsuarioId,
                    nombre = $"{person.Nombres} {person.Apellidos}".Trim(),
                    email = user!.Email,
                    cargoId = cargo?.Id,
                    cargo = cargo?.Name,
                    rolesIdentity = identityRoles,
                    permisos = cargo is null ? null : new
                    {
                        ver = cargo.Ver,
                        crudRequerimientos = cargo.CrudRequerimientos,
                        crudPisos = cargo.CrudPisos,
                        crudUsuarios = cargo.CrudUsuarios,
                        crudHabitaciones = cargo.CrudHabitaciones,
                        crudTipoVip = cargo.CrudTipoVip
                    }
                });
                var destinoPredeterminado = string.Equals(cargo?.Name, "admin", StringComparison.OrdinalIgnoreCase)
                    ? Url.Content("~/")
                    : cargo?.CrudRequerimientos == true
                        ? Url.Page("/Reservas/Index")!
                        : cargo?.Ver == true
                            ? Url.Page("/Habitaciones/Index")!
                            : Url.Content("~/Account/Login");
                var destino = EsDestinoPermitido(returnUrl, cargo) ? returnUrl! : destinoPredeterminado;
                return LocalRedirect(destino);
            }

            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");
        return Page();
    }

    private bool EsDestinoPermitido(string? returnUrl, HotelRole? cargo)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl)) return false;
        var path = returnUrl.Split('?', '#')[0];
        if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
            return string.Equals(cargo?.Name, "admin", StringComparison.OrdinalIgnoreCase);
        if (path.StartsWith("/Reservas", StringComparison.OrdinalIgnoreCase))
            return cargo?.CrudRequerimientos == true;
        if (path.StartsWith("/Habitaciones", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Usuarios", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Chat", StringComparison.OrdinalIgnoreCase))
            return cargo?.Ver == true;
        return path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase);
    }
}
