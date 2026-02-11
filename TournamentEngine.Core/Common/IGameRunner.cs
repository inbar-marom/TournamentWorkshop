namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for game execution engine that runs matches between bots
/// </summary>
public interface IGameRunner
{
    /// <summary>
    /// Execute a single match between two bots for a specific game type
    /// </summary>
    /// <param name="bot1">First bot</param>
    /// <param name="bot2">Second bot</param>
    /// <param name="gameType">Type of game to play</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Complete match result with winner, scores, and logs</returns>
    Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, GameType gameType, CancellationToken cancellationToken);

    /// <summary>
    /// Execute a single match between two bots using game-specific configuration
    /// </summary>
    /// <param name="bot1">First bot</param>
    /// <param name="bot2">Second bot</param>
    /// <param name="game">Game instance with specific rules</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Complete match result with winner, scores, and logs</returns>
    Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, IGame game, CancellationToken cancellationToken);

    /// <summary>
    /// Validate that a bot can execute for a specific game type
    /// </summary>
    /// <param name="bot">Bot to validate</param>
    /// <param name="gameType">Game type to validate against</param>
    /// <returns>True if bot is valid for the game</returns>
    Task<bool> ValidateBot(IBot bot, GameType gameType);
}
