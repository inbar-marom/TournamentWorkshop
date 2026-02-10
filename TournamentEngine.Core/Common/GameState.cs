namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents the current state of a game
/// </summary>
public class GameState
{
    public Dictionary<string, object> State { get; init; } = new();
    public List<string> MoveHistory { get; init; } = new();
    public int CurrentRound { get; init; }
    public int MaxRounds { get; init; }
    public bool IsGameOver { get; init; }
    public string? Winner { get; init; }
}
