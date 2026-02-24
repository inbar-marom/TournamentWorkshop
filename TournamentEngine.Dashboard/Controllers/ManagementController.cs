using Microsoft.AspNetCore.Mvc;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Api.Services;

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
        _logger.LogWarning("🎯 API ENDPOINT: Start tournament requested");
        var result = await _managementService.StartAsync();
        
        if (!result.IsSuccess)
        {
            _logger.LogError("❌ API ENDPOINT: Start failed: {Message}", result.Message);
            return BadRequest(new { error = result.Message });
        }

        _logger.LogWarning("✅ API ENDPOINT: Start succeeded");
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
    }

    /// <summary>
    /// Configure tournament settings (fast match threshold, scheduled start time)
    /// </summary>
    [HttpPost("configure")]
    public async Task<ActionResult<object>> ConfigureTournament([FromBody] ConfigurationRequest request)
    {
        _logger.LogWarning("⚙️ API ENDPOINT: Configuration requested: Threshold={Threshold}s, ScheduledStart={ScheduledStart}", 
            request.FastMatchThresholdSeconds, request.ScheduledStartTime);

        var result = await _managementService.SetFastMatchThresholdAsync(request.FastMatchThresholdSeconds ?? 5);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        if (request.ScheduledStartTime.HasValue)
        {
            _logger.LogWarning("⏰ Setting scheduled start time to: {Time} UTC", request.ScheduledStartTime);
            result = await _managementService.SetScheduledStartTimeAsync(request.ScheduledStartTime);
            if (!result.IsSuccess)
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        else
        {
            _logger.LogWarning("🚫 Clearing scheduled start time");
            result = await _managementService.SetScheduledStartTimeAsync(null);
        }

        _logger.LogWarning("✅ Configuration applied successfully");
        return Ok(new { success = true, message = "Configuration applied successfully" });
    }

    /// <summary>
    /// Toggle bot submission acceptance state (accepts new bots or rejects them)
    /// </summary>
    [HttpPost("toggle-bot-submissions")]
    public async Task<ActionResult<object>> ToggleBotSubmissions([FromBody] BotSubmissionToggleRequest request)
    {
        var botStorage = HttpContext.RequestServices.GetRequiredService<BotStorageService>();
        
        botStorage.SetPauseState(!request.AcceptBots);
        var isPaused = botStorage.IsPaused();
        
        _logger.LogInformation("Bot submission state toggled: AcceptingBots={AcceptingBots}", !isPaused);
        
        return Ok(new { success = true, acceptingBots = !isPaused, message = !isPaused ? "Now accepting bot submissions" : "Bot submissions stopped" });
    }

    /// <summary>
    /// Get current bot submission acceptance state
    /// </summary>
    [HttpGet("bot-submission-status")]
    public ActionResult<object> GetBotSubmissionStatus()
    {
        var botStorage = HttpContext.RequestServices.GetRequiredService<BotStorageService>();
        var isPaused = botStorage.IsPaused();
        
        return Ok(new { success = true, acceptingBots = !isPaused, isPaused });
    }
}

public class ConfigurationRequest
{
    public int? FastMatchThresholdSeconds { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
}

public class BotSubmissionToggleRequest
{
    public bool AcceptBots { get; set; }
}