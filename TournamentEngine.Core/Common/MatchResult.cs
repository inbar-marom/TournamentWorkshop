namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents the result of a completed match
/// </summary>
public class MatchResult
{
    public required string Bot1Name { get; init; }
    public required string Bot2Name { get; init; }
    public required GameType GameType { get; init; }
    public required MatchOutcome Outcome { get; init; }
    public string? WinnerName { get; init; }
    public int Bot1Score { get; init; }
    public int Bot2Score { get; init; }
    public List<string> MatchLog { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration { get; init; }
}
