namespace TournamentEngine.Core.Tournament;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.BotLoader;

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
    private readonly ITournamentEventPublisher? _eventPublisher;
    private readonly IBotLoader? _botLoader;
    private readonly IMatchResultsLogger? _matchResultsLogger;
    
    // Hold the current running series for dashboard queries
    private TournamentSeriesInfo? _currentSeries;
    private readonly object _seriesLock = new();

    public TournamentSeriesManager(
        ITournamentManager tournamentManager,
        IScoringSystem scoringSystem,
        ITournamentEventPublisher? eventPublisher = null,
        IBotLoader? botLoader = null,
        IMatchResultsLogger? matchResultsLogger = null)
    {
        _tournamentManager = tournamentManager ?? throw new ArgumentNullException(nameof(tournamentManager));
        _scoringSystem = scoringSystem ?? throw new ArgumentNullException(nameof(scoringSystem));
        _eventPublisher = eventPublisher;
        _botLoader = botLoader;
        _matchResultsLogger = matchResultsLogger;
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

        // Make series available to dashboard immediately
        SetCurrentSeries(seriesInfo);

        var runGameType = config.GameTypes.FirstOrDefault();
        _matchResultsLogger?.StartTournamentRun(seriesInfo.SeriesId, runGameType);

        var seriesName = config.SeriesName ?? "Tournament Series";
        var steps = config.GameTypes
            .Select((gameType, index) => new EventStepDto
            {
                StepIndex = index + 1,
                GameType = gameType,
                Status = index == 0 ? EventStepStatus.InProgress : EventStepStatus.NotStarted
            })
            .ToList();

        if (_eventPublisher != null)
        {
            var tournamentStartedEvent = new TournamentStartedEventDto
            {
                TournamentId = seriesInfo.SeriesId,
                TournamentName = seriesName,
                TotalSteps = steps.Count,
                Steps = CloneSteps(steps),
                StartedAt = seriesInfo.StartTime
            };

            await _eventPublisher.PublishTournamentStartedAsync(tournamentStartedEvent);

            await _eventPublisher.PublishTournamentProgressUpdatedAsync(new TournamentProgressUpdatedEventDto
            {
                TournamentState = CreateTournamentState(seriesInfo.SeriesId, seriesName, steps, 1, TournamentStatus.InProgress),
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Run each tournament in the series SEQUENTIALLY
        // THREAD SAFETY: Sequential execution means no concurrent access to seriesInfo.Tournaments
        // If changing to parallel execution (e.g., Task.WhenAll), you must:
        // - Use a thread-safe collection or lock around Tournaments.Add()
        // - Ensure aggregation methods only run after all tournaments complete
        for (int i = 0; i < config.GameTypes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gameType = config.GameTypes[i];
            var tournamentName = $"{gameType} Tournament #{i + 1}";
            var tournamentInfo = await _tournamentManager.RunTournamentAsync(
                bots, gameType, config.BaseConfig, cancellationToken);

            seriesInfo.Tournaments.Add(tournamentInfo);

            // Update current series for dashboard queries
            SetCurrentSeries(seriesInfo);

            // Reload bots between events to reset memory tracking
            // Skip reload after the last event
            if (i + 1 < config.GameTypes.Count && _botLoader != null)
            {
                bots = await _botLoader.ReloadAllBotsAsync(bots, config.BaseConfig, cancellationToken);
            }

            if (_eventPublisher != null)
            {
                var step = steps[i];
                step.Status = EventStepStatus.Completed;
                step.WinnerName = tournamentInfo.Champion;
                step.EventId = tournamentInfo.TournamentId;
                step.EventName = tournamentName;

                var stepCompletedEvent = new EventStepCompletedDto
                {
                    TournamentId = seriesInfo.SeriesId,
                    StepIndex = step.StepIndex,
                    GameType = step.GameType,
                    WinnerName = step.WinnerName,
                    EventId = step.EventId,
                    EventName = step.EventName,
                    CompletedAt = DateTime.UtcNow
                };

                await _eventPublisher.PublishEventStepCompletedAsync(stepCompletedEvent);

                if (i + 1 < steps.Count)
                {
                    steps[i + 1].Status = EventStepStatus.InProgress;
                }

                var currentStepIndex = Math.Min(i + 2, steps.Count);
                var status = i + 1 == steps.Count ? TournamentStatus.Completed : TournamentStatus.InProgress;

                await _eventPublisher.PublishTournamentProgressUpdatedAsync(new TournamentProgressUpdatedEventDto
                {
                    TournamentState = CreateTournamentState(seriesInfo.SeriesId, seriesName, steps, currentStepIndex, status),
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Calculate series standings and champion
        CalculateSeriesStandings(seriesInfo);

        // Calculate series statistics
        CalculateSeriesStatistics(seriesInfo);

        seriesInfo.EndTime = DateTime.UtcNow;

        if (_eventPublisher != null)
        {
            var completedEvent = new TournamentCompletedEventDto
            {
                TournamentId = seriesInfo.SeriesId,
                TournamentName = seriesName,
                Champion = seriesInfo.SeriesChampion ?? "Unknown",
                CompletedAt = seriesInfo.EndTime.Value
            };

            await _eventPublisher.PublishTournamentCompletedAsync(completedEvent);
        }

        return seriesInfo;
    }

    private static TournamentStateDto CreateTournamentState(
        string seriesId,
        string seriesName,
        List<EventStepDto> steps,
        int currentStepIndex,
        TournamentStatus status)
    {
        return new TournamentStateDto
        {
            TournamentId = seriesId,
            TournamentName = seriesName,
            TotalSteps = steps.Count,
            CurrentStepIndex = currentStepIndex,
            Status = status,
            Steps = CloneSteps(steps),
            LastUpdated = DateTime.UtcNow
        };
    }

    private static List<EventStepDto> CloneSteps(List<EventStepDto> steps)
    {
        return steps
            .Select(step => new EventStepDto
            {
                StepIndex = step.StepIndex,
                GameType = step.GameType,
                Status = step.Status,
                WinnerName = step.WinnerName,
                EventId = step.EventId,
                EventName = step.EventName
            })
            .ToList();
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

    /// <summary>
    /// Get all matches from the current or completed tournament series.
    /// </summary>
    public async Task<List<RecentMatchDto>> GetAllMatchesAsync()
    {
        lock (_seriesLock)
        {
            if (_currentSeries == null)
                return new List<RecentMatchDto>();

            var matches = new List<RecentMatchDto>();
            
            foreach (var tournament in _currentSeries.Tournaments)
            {
                foreach (var result in tournament.MatchResults)
                {
                    matches.Add(new RecentMatchDto
                    {
                        MatchId = Guid.NewGuid().ToString(),
                        Bot1Name = result.Bot1Name,
                        Bot2Name = result.Bot2Name,
                        Bot1Score = result.Bot1Score,
                        Bot2Score = result.Bot2Score,
                        Outcome = result.Outcome,
                        WinnerName = result.WinnerName,
                        GameType = tournament.GameType,
                        TournamentName = tournament.GameType.ToString(),
                        EventName = tournament.GameType.ToString(),
                        EventId = tournament.TournamentId,
                        GroupLabel = result.GroupLabel ?? "",
                        CompletedAt = result.EndTime
                    });
                }
            }

            return matches.OrderByDescending(m => m.CompletedAt).ToList();
        }
    }

    /// <summary>
    /// Get group standings for a specific event (tournament/game type).
    /// </summary>
    public async Task<List<GroupDto>> GetGroupStandingsByEventAsync(string eventName)
    {
        lock (_seriesLock)
        {
            if (_currentSeries == null)
                return new List<GroupDto>();

            // For now, return an empty list as TournamentInfo doesn't expose Group structure
            // The group data is available within the tournament engine internals
            // but not exposed via the public TournamentInfo API
            return new List<GroupDto>();
        }
    }

    /// <summary>
    /// Get the current tournament state.
    /// </summary>
    public async Task<DashboardStateDto> GetDashboardStateAsync()
    {
        lock (_seriesLock)
        {
            if (_currentSeries == null)
            {
                return new DashboardStateDto
                {
                    Status = TournamentStatus.NotStarted,
                    Message = "No tournament running"
                };
            }

            var state = new DashboardStateDto
            {
                TournamentId = _currentSeries.SeriesId,
                TournamentName = _currentSeries.Config.SeriesName ?? "Tournament Series",
                Champion = _currentSeries.SeriesChampion,
                Status = _currentSeries.EndTime == null ? TournamentStatus.InProgress : TournamentStatus.Completed,
                Message = $"Series in progress - {_currentSeries.TotalMatches} matches completed",
                OverallLeaderboard = _currentSeries.SeriesStandings
                    .OrderByDescending(s => s.TotalSeriesScore)
                    .Select((s, index) => new TeamStandingDto
                    {
                        TeamName = s.BotName,
                        Rank = index + 1
                    })
                    .ToList(),
                LastUpdated = DateTime.UtcNow
            };

            return state;
        }
    }

    internal void SetCurrentSeries(TournamentSeriesInfo series)
    {
        lock (_seriesLock)
        {
            _currentSeries = series;
        }
    }
}
