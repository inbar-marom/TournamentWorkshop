using Microsoft.AspNetCore.Mvc.RazorPages;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Pages;

/// <summary>
/// Page model for tournament management controls.
/// Provides state management and UI logic for starting, pausing, resuming, and stopping tournaments.
/// </summary>
public class ManageModel : PageModel
{
    private readonly ILogger<ManageModel> _logger;
    private readonly TournamentManagementService _managementService;

    public ManageModel(
        ILogger<ManageModel> logger,
        TournamentManagementService managementService)
    {
        _logger = logger;
        _managementService = managementService;
    }

    /// <summary>
    /// Handles GET requests to the management page.
    /// </summary>
    public async Task OnGetAsync()
    {
        _logger.LogInformation("Management page accessed");
        
        // Page loads and connects to SignalR hub to receive real-time state updates
        // Initial state is fetched via SignalR GetManagementState() method
    }
}
