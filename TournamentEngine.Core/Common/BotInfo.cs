namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a bot in the tournament bracket
/// </summary>
public class BotInfo
{
    public required string TeamName { get; init; }
    public required GameType GameType { get; init; }
    public required string FilePath { get; init; }
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public DateTime LoadTime { get; init; }
    
    /// <summary>
    /// The loaded bot instance, ready to execute.
    /// Null if the bot failed to load (check IsValid and ValidationErrors).
    /// Populated by IBotLoader during bot loading phase.
    /// </summary>
    public IBot? BotInstance { get; init; }
}
