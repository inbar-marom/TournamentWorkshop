namespace UserBot.Core;

/// <summary>
/// Represents the current state of a game including move history and other relevant information
/// </summary>
public class GameState
{
    /// <summary>
    /// The type of game being played
    /// </summary>
    public GameType GameType { get; set; }
    
    /// <summary>
    /// Current round number (1-based)
    /// </summary>
    public int RoundNumber { get; set; }
    
    /// <summary>
    /// Total number of rounds in the game
    /// </summary>
    public int TotalRounds { get; set; }
    
    /// <summary>
    /// History of moves made by this bot
    /// </summary>
    public List<string> MyMoveHistory { get; set; } = new();
    
    /// <summary>
    /// History of moves made by the opponent
    /// </summary>
    public List<string> OpponentMoveHistory { get; set; } = new();
    
    /// <summary>
    /// Current score for this bot
    /// </summary>
    public int MyScore { get; set; }
    
    /// <summary>
    /// Current score for the opponent
    /// </summary>
    public int OpponentScore { get; set; }
    
    /// <summary>
    /// Additional game-specific data
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
