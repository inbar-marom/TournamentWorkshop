namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents the current state of the tournament
/// </summary>
public class TournamentInfo
{
    public required string TournamentId { get; init; }
    public required GameType GameType { get; init; }
    public required TournamentState State { get; set; }
    public List<BotInfo> Bots { get; init; } = new();
    public List<MatchResult> MatchResults { get; init; } = new();
    public Dictionary<int, List<string>> Bracket { get; init; } = new();
    public string? Champion { get; set; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public int CurrentRound { get; set; }
    public int TotalRounds { get; init; }
}
