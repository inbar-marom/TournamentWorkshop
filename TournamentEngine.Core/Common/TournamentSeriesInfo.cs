namespace TournamentEngine.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Complete tournament series results and metadata
/// </summary>
public class TournamentSeriesInfo
{
    /// <summary>
    /// Unique identifier for the series
    /// </summary>
    public required string SeriesId { get; init; }

    /// <summary>
    /// Individual tournament results in execution order
    /// </summary>
    public List<TournamentInfo> Tournaments { get; init; } = new();

    /// <summary>
    /// Aggregated standings across all tournaments
    /// </summary>
    public List<SeriesStanding> SeriesStandings { get; init; } = new();

    /// <summary>
    /// Bot with the highest total series score
    /// </summary>
    public string? SeriesChampion { get; set; }

    /// <summary>
    /// Series start time
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Series end time (null if in progress)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Configuration used for the series
    /// </summary>
    public required TournamentSeriesConfig Config { get; init; }
}
