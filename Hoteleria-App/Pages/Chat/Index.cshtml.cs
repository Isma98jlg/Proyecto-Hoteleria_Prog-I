using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hoteleria_App.Security;

namespace Hoteleria_App.Pages.Chat;

[Authorize(Policy = HotelPermissionPolicies.Ver)]
public class IndexModel : PageModel { }
