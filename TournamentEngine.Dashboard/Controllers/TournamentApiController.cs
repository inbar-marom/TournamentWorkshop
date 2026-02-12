using Microsoft.AspNetCore.Mvc;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Controllers;

[ApiController]
[Route("api/tournament")]
public class TournamentApiController : ControllerBase
{
    private readonly StateManagerService _stateManager;
    private readonly SeriesDashboardViewService _seriesDashboard;
    private readonly ILogger<TournamentApiController> _logger;

    public TournamentApiController(
        StateManagerService stateManager,
        SeriesDashboardViewService seriesDashboard,
        ILogger<TournamentApiController> logger)
    {
        _stateManager = stateManager;
        _seriesDashboard = seriesDashboard;
        _logger = logger;
    }

    /// <summary>
    /// Get current tournament state snapshot
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<TournamentStateDto>> GetCurrentState()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return Ok(state);
    }

    /// <summary>
    /// Get recent completed matches
    /// </summary>
    [HttpGet("matches/recent")]
    public ActionResult<List<MatchCompletedDto>> GetRecentMatches([FromQuery] int count = 20)
    {
        var matches = _stateManager.GetRecentMatches(count);
        return Ok(matches);
    }

    /// <summary>
    /// Get series dashboard layout view.
    /// </summary>
    [HttpGet("series-view")]
    public async Task<ActionResult<SeriesDashboardViewDto>> GetSeriesView()
    {
        var view = await _seriesDashboard.BuildSeriesViewAsync();
        return Ok(view);
    }

    /// <summary>
    /// Clear all tournament data from backend and reset state
    /// </summary>
    [HttpPost("clear")]
    public async Task<ActionResult> ClearAllData()
    {
        _logger.LogWarning("Clearing all tournament data - initiated from dashboard");
        await _stateManager.ClearStateAsync();
        return Ok(new { Message = "All tournament data cleared successfully" });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "Tournament Dashboard API",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}
