using Microsoft.AspNetCore.Mvc;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Controllers;

[ApiController]
[Route("api/tournament")]
public class TournamentApiController : ControllerBase
{
    private readonly StateManagerService _stateManager;
    private readonly ILogger<TournamentApiController> _logger;

    public TournamentApiController(StateManagerService stateManager, ILogger<TournamentApiController> logger)
    {
        _stateManager = stateManager;
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
