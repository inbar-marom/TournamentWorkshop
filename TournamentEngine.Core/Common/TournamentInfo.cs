namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents the current state of the tournament
/// </summary>
public class TournamentInfo
{
    public required string TournamentId { get; init; }
    public required GameType GameType { get; init; }
    public required TournamentState State { get; init; }
    public List<BotInfo> Bots { get; init; } = new();
    public List<MatchResult> MatchResults { get; init; } = new();
    public Dictionary<int, List<string>> Bracket { get; init; } = new();
    public string? Champion { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int CurrentRound { get; init; }
    public int TotalRounds { get; init; }
}
