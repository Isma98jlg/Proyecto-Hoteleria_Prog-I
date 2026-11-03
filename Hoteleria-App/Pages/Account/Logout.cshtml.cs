using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly SignInManager<Hoteleria_App.Models.HotelUser> _signInManager;

    public LogoutModel(SignInManager<Hoteleria_App.Models.HotelUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
