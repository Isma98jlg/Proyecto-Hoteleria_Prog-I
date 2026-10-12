using Hoteleria_App.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Pages.Chat;

public class IndexModel : PageModel
{
    private readonly HoteleriaDbContext _context;

    public IndexModel(HoteleriaDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        // Placeholder - will be implemented with SignalR
        await Task.CompletedTask;
    }
}