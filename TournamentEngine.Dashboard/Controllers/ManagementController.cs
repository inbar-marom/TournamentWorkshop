using Microsoft.AspNetCore.Mvc;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Controllers;

[ApiController]
[Route("api/management")]
public class ManagementController : ControllerBase
{
    private readonly TournamentManagementService _managementService;
    private readonly ILogger<ManagementController> _logger;

    public ManagementController(
        TournamentManagementService managementService,
        ILogger<ManagementController> logger)
    {
        _managementService = managementService;
        _logger = logger;
    }

    /// <summary>
    /// Get current tournament management state
    /// </summary>
    [HttpGet("state")]
    public async Task<ActionResult<ManagementStateDto>> GetManagementState()
    {
        var state = await _managementService.GetStateAsync();
        return Ok(state);
    }

    /// <summary>
    /// Start a new tournament
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<object>> StartTournament()
    {
        _logger.LogInformation("Start tournament requested");
        var result = await _managementService.StartAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Tournament started" });
    }

    /// <summary>
    /// Pause the current tournament
    /// </summary>
    [HttpPost("pause")]
    public async Task<ActionResult<object>> PauseTournament()
    {
        _logger.LogInformation("Pause tournament requested");
        var result = await _managementService.PauseAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Tournament paused" });
    }

    /// <summary>
    /// Resume a paused tournament
    /// </summary>
    [HttpPost("resume")]
    public async Task<ActionResult<object>> ResumeTournament()
    {
        _logger.LogInformation("Resume tournament requested");
        var result = await _managementService.ResumeAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Tournament resumed" });
    }

    /// <summary>
    /// Stop the current tournament
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<object>> StopTournament()
    {
        _logger.LogInformation("Stop tournament requested");
        var result = await _managementService.StopAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Tournament stopped" });
    }

    /// <summary>
    /// Clear all bot submissions and reset state
    /// </summary>
    [HttpPost("clear")]
    public async Task<ActionResult<object>> ClearSubmissions()
    {
        _logger.LogInformation("Clear submissions requested");
        var result = await _managementService.ClearSubmissionsAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Submissions cleared" });
    }

    /// <summary>
    /// Rerun the last tournament
    /// </summary>
    [HttpPost("rerun")]
    public async Task<ActionResult<object>> RerunTournament()
    {
        _logger.LogInformation("Rerun tournament requested");
        var result = await _managementService.RerunAsync();
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new { message = "Tournament rerun started" });
    }

    /// <summary>
    /// Check bot readiness status
    /// </summary>
    [HttpGet("bot-readiness")]
    public async Task<ActionResult<object>> CheckBotReadiness()
    {
        var (ready, message, botCount) = await _managementService.CheckBotsReadyAsync();
        
        return Ok(new { 
            ready = ready,
            message = message,
            botCount = botCount
        });
    }}