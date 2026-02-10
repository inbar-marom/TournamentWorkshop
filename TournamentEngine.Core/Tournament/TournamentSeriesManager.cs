namespace TournamentEngine.Core.Tournament;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

/// <summary>
/// Orchestrates multiple tournaments in a series.
/// 
/// THREAD SAFETY:
/// This class runs tournaments SEQUENTIALLY (one after another), which is inherently thread-safe
/// because there is no shared mutable state between tournament executions. Each tournament
/// gets its own TournamentInfo instance, and aggregation happens only after all tournaments complete.
/// 
/// IMPORTANT - If you modify this to run tournaments in PARALLEL:
/// 1. You MUST add synchronization around seriesInfo.Tournaments.Add() (use lock or concurrent collection)
/// 2. You MUST ensure CalculateSeriesStandings() and CalculateSeriesStatistics() only run after ALL tournaments complete
/// 3. Consider using Task.WhenAll() instead of sequential await
/// 4. The underlying ITournamentManager and IScoringSystem are thread-safe for concurrent tournament execution,
///    but this class currently does not take advantage of that capability.
/// 
/// Note: Parallel MATCH execution within each tournament is already supported via BaseConfig.MaxParallelMatches.
///       This comment is about parallel TOURNAMENT execution at the series level.
/// </summary>
public class TournamentSeriesManager
{
    private readonly ITournamentManager _tournamentManager;
    private readonly IScoringSystem _scoringSystem;

    public TournamentSeriesManager(ITournamentManager tournamentManager, IScoringSystem scoringSystem)
    {
        _tournamentManager = tournamentManager ?? throw new ArgumentNullException(nameof(tournamentManager));
        _scoringSystem = scoringSystem ?? throw new ArgumentNullException(nameof(scoringSystem));
    }

    public async Task<TournamentSeriesInfo> RunSeriesAsync(
        List<BotInfo> bots,
        TournamentSeriesConfig config,
        CancellationToken cancellationToken = default)
    {
        if (bots == null || bots.Count < 2)
            throw new ArgumentException("At least 2 bots are required for a series", nameof(bots));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        config.Validate();

        // Initialize series
        var seriesInfo = new TournamentSeriesInfo
        {
            SeriesId = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow,
            Config = config
        };

        // Run each tournament in the series SEQUENTIALLY
        // THREAD SAFETY: Sequential execution means no concurrent access to seriesInfo.Tournaments
        // If changing to parallel execution (e.g., Task.WhenAll), you must:
        // - Use a thread-safe collection or lock around Tournaments.Add()
        // - Ensure aggregation methods only run after all tournaments complete
        for (int i = 0; i < config.GameTypes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gameType = config.GameTypes[i];
            var tournamentInfo = await _tournamentManager.RunTournamentAsync(
                bots, gameType, config.BaseConfig, cancellationToken);

            seriesInfo.Tournaments.Add(tournamentInfo);
        }

        // Calculate series standings and champion
        CalculateSeriesStandings(seriesInfo);

        // Calculate series statistics
        CalculateSeriesStatistics(seriesInfo);

        seriesInfo.EndTime = DateTime.UtcNow;

        return seriesInfo;
    }

    private void CalculateSeriesStandings(TournamentSeriesInfo seriesInfo)
    {
        // THREAD SAFETY: This method assumes all tournaments have completed and seriesInfo.Tournaments
        // is not being modified concurrently. Safe because called after sequential tournament execution.
        // If tournaments run in parallel, ensure this is only called after Task.WhenAll() completes.
        
        // Get all unique bot names
        var botNames = seriesInfo.Tournaments
            .SelectMany(t => t.Bots.Select(b => b.TeamName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var standingsDict = new Dictionary<string, SeriesStanding>(StringComparer.OrdinalIgnoreCase);

        foreach (var botName in botNames)
        {
            standingsDict[botName] = new SeriesStanding { BotName = botName };
        }

        // Aggregate scores from all tournaments
        foreach (var tournament in seriesInfo.Tournaments)
        {
            var tournamentRankings = _scoringSystem.GetCurrentRankings(tournament);

            foreach (var ranking in tournamentRankings)
            {
                if (!standingsDict.TryGetValue(ranking.BotName, out var standing))
                    continue;

                standing.TotalSeriesScore += ranking.TotalScore;
                standing.TotalWins += ranking.Wins;
                standing.TotalLosses += ranking.Losses;

                // Track score by game type
                if (!standing.ScoresByGame.ContainsKey(tournament.GameType))
                    standing.ScoresByGame[tournament.GameType] = 0;
                standing.ScoresByGame[tournament.GameType] += ranking.TotalScore;

                // Track tournament placement
                standing.TournamentPlacements.Add(ranking.FinalPlacement);

                // Count tournament wins (1st place)
                if (ranking.FinalPlacement == 1)
                    standing.TournamentsWon++;
            }
        }

        // Sort standings by total series score (descending)
        seriesInfo.SeriesStandings.AddRange(
            standingsDict.Values
                .OrderByDescending(s => s.TotalSeriesScore)
                .ThenByDescending(s => s.TotalWins)
                .ThenBy(s => s.TotalLosses)
                .ThenByDescending(s => s.TournamentsWon)
                .ThenBy(s => s.BotName, StringComparer.OrdinalIgnoreCase)
        );

        // Determine series champion
        if (seriesInfo.SeriesStandings.Count > 0)
        {
            seriesInfo.SeriesChampion = seriesInfo.SeriesStandings[0].BotName;
        }
    }

    private void CalculateSeriesStatistics(TournamentSeriesInfo seriesInfo)
    {
        // THREAD SAFETY: This method assumes all tournaments have completed and seriesInfo.Tournaments
        // is not being modified concurrently. Safe because called after sequential tournament execution.
        // If tournaments run in parallel, ensure this is only called after Task.WhenAll() completes.
        
        // Calculate total matches
        seriesInfo.TotalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);

        // Calculate matches by game type
        foreach (var tournament in seriesInfo.Tournaments)
        {
            if (!seriesInfo.MatchesByGameType.ContainsKey(tournament.GameType))
                seriesInfo.MatchesByGameType[tournament.GameType] = 0;

            seriesInfo.MatchesByGameType[tournament.GameType] += tournament.MatchResults.Count;
        }
    }
}
