namespace TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// Management state snapshot for tournament control actions.
/// </summary>
public class ManagementStateDto
{
    public ManagementRunState Status { get; set; } = ManagementRunState.NotStarted;
    public string Message { get; set; } = string.Empty;
    public bool BotsReady { get; set; }
    public int BotCount { get; set; }
    public string? LastAction { get; set; }
    public DateTime? LastActionAt { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Scheduled start time for the tournament (UTC). If null, tournament can start immediately.
    /// </summary>
    public DateTime? ScheduledStartTime { get; set; }
    
    /// <summary>
    /// Fast match reporting delay threshold in seconds (configurable).
    /// Default: 10 seconds. Can be set via management page.
    /// </summary>
    public int FastMatchThresholdSeconds { get; set; } = 10;
    
    /// <summary>
    /// Current time in Israel timezone (for display on dashboard).
    /// </summary>
    public DateTime CurrentIsraelTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Current management run state for the tournament.
/// </summary>
public enum ManagementRunState
{
    NotStarted,
    Running,
    Paused,
    Stopped,
    Completed,
    Error
}
