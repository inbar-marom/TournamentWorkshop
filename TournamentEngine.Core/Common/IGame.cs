namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for specific game implementations
/// </summary>
public interface IGame
{
    /// <summary>
    /// The type of game this implementation handles
    /// </summary>
    GameType GameType { get; }

    /// <summary>
    /// Human-readable name of the game
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Maximum number of rounds for this game
    /// </summary>
    int MaxRounds { get; }

    /// <summary>
    /// Timeout per move for this game
    /// </summary>
    TimeSpan MoveTimeout { get; }

    /// <summary>
    /// Get the initial game state for a new match
    /// </summary>
    /// <returns>Initial game state</returns>
    GameState GetInitialState();

    /// <summary>
    /// Execute a complete match between two bots using this game's rules
    /// </summary>
    /// <param name="bot1">First bot</param>
    /// <param name="bot2">Second bot</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Complete match result</returns>
    Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, CancellationToken cancellationToken);

    /// <summary>
    /// Validate a move for this game
    /// </summary>
    /// <param name="move">Move to validate (type depends on game)</param>
    /// <param name="gameState">Current game state</param>
    /// <returns>True if move is valid</returns>
    bool IsValidMove(object move, GameState gameState);

    /// <summary>
    /// Get all valid moves for the current game state
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <returns>List of valid moves (type depends on game)</returns>
    List<object> GetValidMoves(GameState gameState);

    /// <summary>
    /// Apply a move to the game state and return new state
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <param name="move">Move to apply</param>
    /// <param name="player">Player making the move (1 or 2)</param>
    /// <returns>New game state after move</returns>
    GameState ApplyMove(GameState gameState, object move, int player);

    /// <summary>
    /// Determine if the game is over in the current state
    /// </summary>
    /// <param name="gameState">Current game state</param>
    /// <returns>True if game is complete</returns>
    bool IsGameOver(GameState gameState);

    /// <summary>
    /// Get the winner of a completed game
    /// </summary>
    /// <param name="gameState">Final game state</param>
    /// <returns>Winner name, or null if draw/no winner</returns>
    string? GetWinner(GameState gameState);

    /// <summary>
    /// Calculate scores for both players in the final state
    /// </summary>
    /// <param name="gameState">Final game state</param>
    /// <returns>Tuple of (player1Score, player2Score)</returns>
    (int player1Score, int player2Score) GetFinalScores(GameState gameState);
}
