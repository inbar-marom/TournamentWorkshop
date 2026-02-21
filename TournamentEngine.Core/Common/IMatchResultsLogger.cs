namespace TournamentEngine.Core.Common;

/// <summary>
/// Appends match results to a shared sink for monitoring and audit.
/// </summary>
public interface IMatchResultsLogger
{
    void StartTournamentRun(string tournamentId, GameType gameType);
    void AppendMatchResult(MatchResult matchResult, string groupLabel);
}
