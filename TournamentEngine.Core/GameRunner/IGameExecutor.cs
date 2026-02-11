namespace TournamentEngine.Core.GameRunner;

using Common;

/// <summary>
/// Interface for game-specific execution logic
/// </summary>
public interface IGameExecutor
{
    /// <summary>
    /// The game type this executor handles
    /// </summary>
    GameType GameType { get; }

    /// <summary>
    /// Execute a match between two bots
    /// </summary>
    /// <param name="bot1">First bot</param>
    /// <param name="bot2">Second bot</param>
    /// <param name="config">Tournament configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Match result</returns>
    Task<MatchResult> Execute(IBot bot1, IBot bot2, TournamentConfig config, CancellationToken cancellationToken);
}
