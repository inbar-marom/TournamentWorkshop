using Microsoft.AspNetCore.Mvc.RazorPages;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Pages;

public class BotsPageModel : PageModel
{
    private readonly BotDashboardService _botDashboardService;
    private readonly ILogger<BotsPageModel> _logger;

    public BotsPageModel(BotDashboardService botDashboardService, ILogger<BotsPageModel> logger)
    {
        _botDashboardService = botDashboardService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            _logger.LogInformation("Loading Bot Submissions page");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bots page");
        }
    }
}
