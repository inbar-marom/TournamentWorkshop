namespace TournamentEngine.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Configuration for a tournament series
/// </summary>
public class TournamentSeriesConfig
{
    /// <summary>
    /// Game types for each tournament in order. Cycles through if TournamentsCount > GameTypes.Count
    /// </summary>
    public required List<GameType> GameTypes { get; init; }

    /// <summary>
    /// Shared configuration for all tournaments (timeouts, parallel matches, etc.)
    /// </summary>
    public required TournamentConfig BaseConfig { get; init; }

    /// <summary>
    /// Number of tournaments to run in the series
    /// </summary>
    public required int TournamentsCount { get; init; }

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

        if (TournamentsCount <= 0)
            throw new ArgumentException("Tournament count must be greater than zero", nameof(TournamentsCount));

        if (BaseConfig == null)
            throw new ArgumentNullException(nameof(BaseConfig));
    }

    /// <summary>
    /// Gets the game type for a specific tournament index (cycles through GameTypes)
    /// </summary>
    public GameType GetGameTypeForTournament(int tournamentIndex)
    {
        if (tournamentIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(tournamentIndex), "Tournament index cannot be negative");

        if (GameTypes == null || GameTypes.Count == 0)
            throw new InvalidOperationException("No game types configured");

        return GameTypes[tournamentIndex % GameTypes.Count];
    }
}
