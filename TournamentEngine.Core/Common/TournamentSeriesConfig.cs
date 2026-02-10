namespace TournamentEngine.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Configuration for a tournament series.
/// 
/// THREAD SAFETY: This class is immutable after construction (all properties are init-only),
/// making it safe to share across threads without synchronization.
/// </summary>
public class TournamentSeriesConfig
{
    /// <summary>
    /// Game types for each tournament in order. Number of tournaments equals GameTypes.Count
    /// </summary>
    public required List<GameType> GameTypes { get; init; }

    /// <summary>
    /// Shared configuration for all tournaments (timeouts, parallel matches, etc.)
    /// </summary>
    public required TournamentConfig BaseConfig { get; init; }

    /// <summary>
    /// Whether to aggregate scores across tournaments (default: true)
    /// </summary>
    public bool AggregateScores { get; init; } = true;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (GameTypes == null || GameTypes.Count == 0)
            throw new ArgumentException("At least one game type is required", nameof(GameTypes));

        if (BaseConfig == null)
            throw new ArgumentNullException(nameof(BaseConfig));
    }
}
