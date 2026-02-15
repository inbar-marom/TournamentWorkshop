namespace UserBot.Core;

/// <summary>
/// Interface that all bot implementations must adhere to
/// </summary>
public interface IBot
{
    string TeamName { get; }
    GameType GameType { get; }
    
    /// <summary>
    /// Make a move for RPSLS game
    /// </summary>
    /// <param name="gameState">Current game state including move history</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Move string (Rock, Paper, Scissors, Lizard, Spock)</returns>
    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
    
    /// <summary>
    /// Allocate troops for Colonel Blotto game
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Array of 5 integers that sum to 100</returns>
    Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
    
    /// <summary>
    /// Make a decision for Penalty Kicks game
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Decision string</returns>
    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
    
    /// <summary>
    /// Make a move for Security game
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Security move string</returns>
    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
}
