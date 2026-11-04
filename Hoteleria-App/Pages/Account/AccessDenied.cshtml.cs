using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hoteleria_App.Pages.Account;

[AllowAnonymous]
public class AccessDeniedModel : PageModel { }
