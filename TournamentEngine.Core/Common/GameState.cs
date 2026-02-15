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
    public List<RoundHistory> RoundHistory { get; init; } = new();
}

/// <summary>
/// Represents history of a single round in a game
/// </summary>
public class RoundHistory
{
    public int Round { get; init; }
    public string? MyMove { get; init; }
    public string? OpponentMove { get; init; }
    public string? Result { get; init; } // "Win", "Loss", "Draw"
    public string? Role { get; init; } // For games with roles (e.g., "Shooter", "Goalkeeper")
}
