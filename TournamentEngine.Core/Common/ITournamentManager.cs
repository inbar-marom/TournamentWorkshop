namespace TournamentEngine.Core.Common;

/// <summary>
/// High-level interface for tournament orchestration and management
/// </summary>
public interface ITournamentManager
{
    /// <summary>
    /// Run a complete tournament from start to finish
    /// </summary>
    /// <param name="bots">List of validated bots to participate</param>
    /// <param name="gameType">Type of game for the tournament</param>
    /// <param name="config">Tournament configuration settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final tournament information with champion</returns>
    Task<TournamentInfo> RunTournamentAsync(
        List<BotInfo> bots, 
        GameType gameType, 
        TournamentConfig config,
        CancellationToken cancellationToken = default,
        int eventNumber = 1,
        string? eventName = null);
}
