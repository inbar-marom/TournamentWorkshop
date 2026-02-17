namespace TournamentEngine.Core.Common;

/// <summary>
/// Low-level interface for tournament execution and state management
/// </summary>
public interface ITournamentEngine
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

    /// <summary>
    /// Get the event log showing all tournament actions
    /// </summary>
    /// <returns>Read-only list of timestamped event messages</returns>
    IReadOnlyList<string> GetEventLog();

    /// <summary>
    /// Gets the group label for a match based on the participating bots.
    /// Returns labels like "Group #1", "Group #2", or "Final Group".
    /// </summary>
    /// <param name="bot1Name">Name of the first bot</param>
    /// <param name="bot2Name">Name of the second bot</param>
    /// <returns>Label describing which group/stage the match belongs to</returns>
    string GetMatchGroupLabel(string bot1Name, string bot2Name);
}
