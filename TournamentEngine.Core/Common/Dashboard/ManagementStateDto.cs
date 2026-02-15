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
