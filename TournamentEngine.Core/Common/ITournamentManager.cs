namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for tournament management and bracket progression
/// </summary>
public interface ITournamentManager
{
    /// <summary>
    /// Initialize a new tournament with the given bots and configuration
    /// </summary>
    /// <param name="bots">List of validated bots to participate</param>
    /// <param name="gameType">Type of game for the tournament</param>
    /// <param name="config">Tournament configuration settings</param>
    /// <returns>Tournament information with initial bracket</returns>
    TournamentInfo InitializeTournament(List<BotInfo> bots, GameType gameType, TournamentConfig config);

    /// <summary>
    /// Get the next matches to be played in the current round
    /// </summary>
    /// <returns>List of bot pairs for upcoming matches</returns>
    List<(IBot bot1, IBot bot2)> GetNextMatches();

    /// <summary>
    /// Record the result of a completed match and update tournament state
    /// </summary>
    /// <param name="matchResult">Result of the completed match</param>
    /// <returns>Updated tournament information</returns>
    TournamentInfo RecordMatchResult(MatchResult matchResult);

    /// <summary>
    /// Advance the tournament to the next round
    /// </summary>
    /// <returns>Updated tournament information after round advancement</returns>
    TournamentInfo AdvanceToNextRound();

    /// <summary>
    /// Check if the tournament is complete
    /// </summary>
    /// <returns>True if tournament has a champion</returns>
    bool IsTournamentComplete();

    /// <summary>
    /// Get the current tournament state and bracket information
    /// </summary>
    /// <returns>Complete tournament information</returns>
    TournamentInfo GetTournamentInfo();

    /// <summary>
    /// Get the current round number
    /// </summary>
    /// <returns>Current round (1-based)</returns>
    int GetCurrentRound();

    /// <summary>
    /// Get all remaining bots in the tournament
    /// </summary>
    /// <returns>List of bots still competing</returns>
    List<IBot> GetRemainingBots();

    /// <summary>
    /// Generate final tournament rankings
    /// </summary>
    /// <returns>List of bots ordered by tournament placement</returns>
    List<(IBot bot, int placement)> GetFinalRankings();
}
