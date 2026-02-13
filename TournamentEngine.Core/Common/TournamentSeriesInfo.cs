namespace TournamentEngine.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Complete tournament series results and metadata.
/// NOTE: In the new terminology, "Series" is being renamed to "Tournament" (the whole event).
/// This class will eventually be replaced by a new Tournament InfoDto structure.
/// For now, it remains as TournamentSeriesInfo for backward compatibility.
/// </summary>
public class TournamentSeriesInfo
{
    /// <summary>
    /// Unique identifier for the series (tournament in new terminology)
    /// </summary>
    public required string SeriesId { get; init; }

    /// <summary>
    /// Individual tournament results in execution order
    /// NOTE: In new terminology, these are "Events" (individual game types)
    /// </summary>
    public List<TournamentInfo> Tournaments { get; init; } = new();

    /// <summary>
    /// Aggregated standings across all tournaments (events in new terminology)
    /// </summary>
    public List<SeriesStanding> SeriesStandings { get; init; } = new();

    /// <summary>
    /// Bot with the highest total series score (tournament champion in new terminology)
    /// </summary>
    public string? SeriesChampion { get; set; }

    /// <summary>
    /// Series start time (tournament start time in new terminology)
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Series end time (tournament end time in new terminology) - null if in progress
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Configuration used for the series (tournament configuration in new terminology)
    /// </summary>
    public required TournamentSeriesConfig Config { get; init; }

    /// <summary>
    /// Total number of matches across all tournaments (events in new terminology)
    /// </summary>
    public int TotalMatches { get; set; }

    /// <summary>
    /// Breakdown of matches by game type
    /// </summary>
    public Dictionary<GameType, int> MatchesByGameType { get; init; } = new();
}
