namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for scoring and ranking systems
/// </summary>
public interface IScoringSystem
{
    /// <summary>
    /// Calculate the score for a match result
    /// </summary>
    /// <param name="matchResult">Result of the completed match</param>
    /// <returns>Scores for both players</returns>
    (int player1Score, int player2Score) CalculateMatchScore(MatchResult matchResult);

    /// <summary>
    /// Update tournament standings with a new match result
    /// </summary>
    /// <param name="matchResult">Result of the completed match</param>
    /// <param name="currentStandings">Current tournament standings</param>
    /// <returns>Updated standings</returns>
    Dictionary<string, TournamentStanding> UpdateStandings(MatchResult matchResult, Dictionary<string, TournamentStanding> currentStandings);

    /// <summary>
    /// Generate final rankings for completed tournament
    /// </summary>
    /// <param name="tournamentInfo">Complete tournament information</param>
    /// <returns>List of bots with their final rankings</returns>
    List<BotRanking> GenerateFinalRankings(TournamentInfo tournamentInfo);

    /// <summary>
    /// Calculate tournament statistics
    /// </summary>
    /// <param name="tournamentInfo">Complete tournament information</param>
    /// <returns>Tournament statistics summary</returns>
    TournamentStatistics CalculateStatistics(TournamentInfo tournamentInfo);

    /// <summary>
    /// Get current standings for an ongoing tournament
    /// </summary>
    /// <param name="tournamentInfo">Current tournament state</param>
    /// <returns>Current standings and rankings</returns>
    List<BotRanking> GetCurrentRankings(TournamentInfo tournamentInfo);
}

/// <summary>
/// Represents a bot's standing in the tournament
/// </summary>
public class TournamentStanding
{
    public required string BotName { get; init; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int TotalScore { get; set; }
    public int TotalOpponentScore { get; set; }
    public List<string> OpponentsPlayed { get; init; } = new();
    public bool IsEliminated { get; set; }
    public int EliminationRound { get; set; }
}

/// <summary>
/// Represents a bot's final ranking in the tournament
/// </summary>
public class BotRanking
{
    public required string BotName { get; init; }
    public int FinalPlacement { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public int TotalScore { get; init; }
    public int EliminationRound { get; init; }
    public string? EliminatedBy { get; init; }
    public TimeSpan TotalPlayTime { get; init; }
    public List<string> OpponentsDefeated { get; init; } = new();
}

/// <summary>
/// Represents tournament statistics and summary
/// </summary>
public class TournamentStatistics
{
    public int TotalMatches { get; init; }
    public int TotalRounds { get; init; }
    public TimeSpan TournamentDuration { get; init; }
    public TimeSpan AverageMatchDuration { get; init; }
    public int TotalErrors { get; init; }
    public int TotalTimeouts { get; init; }
    public string? MostActiveBot { get; init; }
    public string? HighestScoringBot { get; init; }
    public Dictionary<GameType, int> MatchesByGame { get; init; } = new();
    public Dictionary<string, int> ErrorsByBot { get; init; } = new();
}
