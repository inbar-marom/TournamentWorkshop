namespace TournamentEngine.Core.Common;

using System.Collections.Generic;

/// <summary>
/// Bot's performance across an entire tournament series
/// </summary>
public class SeriesStanding
{
    /// <summary>
    /// Bot name
    /// </summary>
    public required string BotName { get; init; }

    /// <summary>
    /// Total score summed across all tournaments in the series
    /// </summary>
    public int TotalSeriesScore { get; set; }

    /// <summary>
    /// Number of tournaments won (1st place finishes)
    /// </summary>
    public int TournamentsWon { get; set; }

    /// <summary>
    /// Total match wins across all tournaments
    /// </summary>
    public int TotalWins { get; set; }

    /// <summary>
    /// Total match losses across all tournaments
    /// </summary>
    public int TotalLosses { get; set; }

    /// <summary>
    /// Score breakdown by game type
    /// </summary>
    public Dictionary<GameType, int> ScoresByGame { get; init; } = new();

    /// <summary>
    /// Placement in each tournament (1 = champion, 2 = runner-up, etc.)
    /// Ordered by tournament sequence
    /// </summary>
    public List<int> TournamentPlacements { get; init; } = new();
}
