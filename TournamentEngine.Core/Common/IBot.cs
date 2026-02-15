namespace TournamentEngine.Core.Common;

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
    /// Make a decision for Penalty Kicks game (9 rounds)
    /// Your role (Shooter or Goalkeeper) is in gameState.State["Role"]
    /// - As Shooter: Score 1 point by choosing different direction than goalkeeper
    /// - As Goalkeeper: Score 2 points by matching the shooter's direction (save)
    /// </summary>
    /// <param name="gameState">Current game state with Role information</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Decision string: "Left", "Center", or "Right"</returns>
    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
    
    /// <summary>
    /// Make a move for Security Game (5 rounds)
    /// Your role (Attacker or Defender) is in gameState.State["Role"]
    /// Targets with values [10, 20, 30] are in gameState.State["TargetValues"]
    /// Total defense units (30) is in gameState.State["TotalDefenseUnits"]
    /// 
    /// As Attacker: Return target index to attack ("0", "1", or "2")
    /// As Defender: Return defense allocation as comma-separated values ("d0,d1,d2" where sum = 30)
    /// 
    /// Scoring per round:
    /// - If defense = 0 on target: attacker gets full value, defender gets 0
    /// - If 0 < defense < value: attacker gets (value - defense), defender gets defense
    /// - If defense >= value: attacker gets 0, defender gets full value
    /// </summary>
    /// <param name="gameState">Current game state with Role and target information</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Attacker: "0"-"2" (target index), Defender: "d0,d1,d2" (allocations summing to 30)</returns>
    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
}
