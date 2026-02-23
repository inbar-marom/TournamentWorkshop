namespace TournamentEngine.Core.Tournament;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;

/// <summary>
/// High-level tournament manager that orchestrates entire tournament execution
/// Uses ITournamentEngine for low-level tournament operations
/// </summary>
public class TournamentManager : ITournamentManager
{
    // Default values (can be overridden via configuration)
    private static readonly TimeSpan FastMatchDurationThreshold = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FastMatchReportLeadTime = TimeSpan.FromSeconds(5);

    private readonly ITournamentEngine _engine;
    private readonly IGameRunner _gameRunner;
    private readonly IScoringSystem _scoringSystem;
    private readonly ITournamentEventPublisher? _eventPublisher;
    private static int _tournamentCounter = 0;
    
    /// <summary>
    /// Current fast match threshold in seconds. Can be updated before starting tournament.
    /// </summary>
    public int FastMatchThresholdSeconds { get; set; } = 10;

    public TournamentManager(ITournamentEngine engine, IGameRunner gameRunner, IScoringSystem scoringSystem, ITournamentEventPublisher? eventPublisher = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _gameRunner = gameRunner ?? throw new ArgumentNullException(nameof(gameRunner));
        _scoringSystem = scoringSystem ?? throw new ArgumentNullException(nameof(scoringSystem));
        _eventPublisher = eventPublisher;
    }

    public async Task<TournamentInfo> RunTournamentAsync(
        List<BotInfo> bots, 
        GameType gameType, 
        TournamentConfig config, 
        CancellationToken cancellationToken = default)
    {
        if (bots == null || bots.Count < 2)
            throw new ArgumentException("At least 2 bots are required for a tournament", nameof(bots));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Initialize tournament
        _engine.InitializeTournament(bots, gameType, config);

        // Generate tournament ID and name once for this tournament
        var tournamentId = Interlocked.Increment(ref _tournamentCounter).ToString();
        var tournamentName = $"{gameType} Tournament #{tournamentId} - {DateTime.UtcNow:MMM dd, yyyy HH:mm}";

        // Publish tournament started event
        if (_eventPublisher != null)
        {
            var startedEvent = new EventStartedEventDto
            {
                EventId = tournamentId,
                EventName = tournamentName,
                GameType = gameType,
                TotalBots = bots.Count,
                StartedAt = DateTime.UtcNow,
                TotalGroups = Math.Max(1, bots.Count / 10),
                EventNumber = 1
            };
            await _eventPublisher.PublishEventStartedAsync(startedEvent);
        }

        // Main tournament loop - runs until engine signals completion
        while (!_engine.IsTournamentComplete())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get next batch of matches from the engine
            var matches = _engine.GetNextMatches();
            Console.WriteLine($"[TournamentManager] Retrieved {matches.Count} matches");
            
            if (matches.Count == 0)
            {
                // No matches available - let engine advance to next phase/round
                Console.WriteLine($"[TournamentManager] No matches, advancing to next round");
                _engine.AdvanceToNextRound();
                
                // Publish round started event
                if (_eventPublisher != null)
                {
                    var roundEvent = new RoundStartedDto
                    {
                        TournamentId = tournamentId,
                        TournamentName = tournamentName,
                        RoundNumber = _engine.GetCurrentRound(),
                        TotalMatches = 0,
                        StartedAt = DateTime.UtcNow,
                        Stage = "Regular"
                    };
                    await _eventPublisher.PublishRoundStartedAsync(roundEvent);
                }
                continue;
            }

            // Execute all matches in the current stage concurrently
            // THREAD SAFETY: All matches within a stage run in parallel via Task.WhenAll
            // The engine's RecordMatchResult() is thread-safe (uses lock)
            // We wait for ALL matches to complete before advancing to next stage
            // Use MaxParallelMatches from config, or default to 15
            var maxConcurrency = config?.MaxParallelMatches ?? 15;
            Console.WriteLine($"[TournamentManager] Starting execution of {matches.Count} matches with max concurrency {maxConcurrency}");
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            
            Console.WriteLine($"[TournamentManager] Creating {matches.Count} tasks...");
            var taskList = new List<Task<MatchResult>>();
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var index = i;
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var (bot1, bot2) = match;                       
                        var result = await _gameRunner.ExecuteMatch(bot1, bot2, gameType, cancellationToken);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TournamentManager] ERROR in match {index + 1}: {ex.GetType().Name}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"[TournamentManager] Inner exception: {ex.InnerException.Message}");
                        }
                        throw;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);
                taskList.Add(task);
            }
            var tasks = taskList;

            Console.WriteLine($"[TournamentManager] Created {tasks.Count} tasks, streaming completion updates...");

            var taskIndexByTask = tasks
                .Select((task, index) => new { task, index })
                .ToDictionary(item => item.task, item => item.index);
            var pendingTasks = new List<Task<MatchResult>>(tasks);
            var processedCount = 0;
            var completedResults = new SortedDictionary<int, MatchResult>();
            var nextResultIndexToRecord = 0;

            // STAGE SYNCHRONIZATION: still wait until all tasks complete before advancing rounds,
            // but stream each completed match immediately as it finishes.
            while (pendingTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(pendingTasks);
                pendingTasks.Remove(completedTask);

                var result = await completedTask;
                var resultIndex = taskIndexByTask[completedTask];
                processedCount++;

                if (_eventPublisher != null)
                {
                    await ApplyFastMatchReportingDelayAsync(result, FastMatchThresholdSeconds, cancellationToken);

                    var groupLabel = _engine.GetMatchGroupLabel(result.Bot1Name, result.Bot2Name);
                    Console.WriteLine($"[TournamentManager] Match {processedCount}/{tasks.Count}: {result.Bot1Name} vs {result.Bot2Name} - GroupLabel: '{groupLabel}'");

                    var completedAt = result.EndTime.Kind == DateTimeKind.Utc
                        ? result.EndTime
                        : result.EndTime.ToUniversalTime();

                    var matchEvent = new MatchCompletedDto
                    {
                        TournamentId = tournamentId,
                        TournamentName = tournamentName,
                        EventId = gameType.ToString(),
                        EventName = gameType.ToString(),
                        Bot1Name = result.Bot1Name,
                        Bot2Name = result.Bot2Name,
                        Outcome = result.Outcome,
                        GameType = gameType,
                        CompletedAt = completedAt,
                        Bot1Score = result.Bot1Score,
                        Bot2Score = result.Bot2Score,
                        MatchId = Guid.NewGuid().ToString(),
                        WinnerName = result.WinnerName,
                        GroupLabel = groupLabel
                    };
                    await _eventPublisher.PublishMatchCompletedAsync(matchEvent);
                }

                completedResults[resultIndex] = result;

                while (completedResults.TryGetValue(nextResultIndexToRecord, out var inOrderResult))
                {
                    completedResults.Remove(nextResultIndexToRecord);
                    nextResultIndexToRecord++;

                    _engine.RecordMatchResult(inOrderResult);

                    if (_eventPublisher != null)
                    {
                        var currentTournamentInfo = _engine.GetTournamentInfo();
                        var standings = CalculateStandingsFromMatches(currentTournamentInfo);
                        var groupStandings = BuildCurrentGroupStandings(gameType);

                        var standingsEvent = new StandingsUpdatedDto
                        {
                            TournamentId = tournamentId,
                            TournamentName = tournamentName,
                            OverallStandings = standings,
                            GroupStandings = groupStandings,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _eventPublisher.PublishStandingsUpdatedAsync(standingsEvent);
                    }
                }
            }

            Console.WriteLine($"[TournamentManager] All {tasks.Count} matches completed and streamed");
        }

        var finalTournamentInfo = _engine.GetTournamentInfo();

        // Publish tournament completed event
        if (_eventPublisher != null)
        {
            var duration = finalTournamentInfo.EndTime.HasValue
                ? finalTournamentInfo.EndTime.Value - finalTournamentInfo.StartTime
                : TimeSpan.Zero;

            var completedEvent = new EventCompletedEventDto
            {
                EventId = tournamentId,
                EventName = tournamentName,
                Champion = finalTournamentInfo.Champion ?? "Unknown",
                GameType = gameType,
                TotalMatches = finalTournamentInfo.MatchResults?.Count ?? 0,
                CompletedAt = DateTime.UtcNow,
                EventNumber = 1,
                Duration = duration
            };
            await _eventPublisher.PublishEventCompletedAsync(completedEvent);
        }

        return finalTournamentInfo;
    }

    private static async Task ApplyFastMatchReportingDelayAsync(MatchResult result, int thresholdSeconds, CancellationToken cancellationToken)
    {
        var threshold = TimeSpan.FromSeconds(thresholdSeconds);
        if (result.Duration >= threshold)
            return;

        // Add randomization: ±1 second around the delay
        // So if threshold is 10s and delay would be 5s, we get 9-11s with randomness
        var randomVariance = Random.Shared.Next(-1000, 1001); // -1000 to +1000 ms
        var delayWithVariance = threshold - result.Duration - FastMatchReportLeadTime;
        delayWithVariance = delayWithVariance.Add(TimeSpan.FromMilliseconds(randomVariance));
        
        if (delayWithVariance <= TimeSpan.Zero)
            return;

        await Task.Delay(delayWithVariance, cancellationToken);
    }

    private List<GroupDto> BuildCurrentGroupStandings(GameType gameType)
    {
        if (_engine is not GroupStageTournamentEngine groupEngine)
            return new List<GroupDto>();

        try
        {
            var eventName = gameType.ToString();
            var summary = groupEngine.GetCurrentPhaseSummary();
            return summary.Groups
                .Select(group =>
                {
                    var ordered = group.Standings
                        .OrderByDescending(s => s.Points)
                        .ThenByDescending(s => s.Wins)
                        .ThenBy(s => s.BotName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var rankings = ordered
                        .Select((standing, index) => new BotRankingDto
                        {
                            Rank = index + 1,
                            TeamName = standing.BotName,
                            Wins = standing.Wins,
                            Losses = standing.Losses,
                            Draws = standing.Draws,
                            Points = standing.Points
                        })
                        .ToList();

                    return new GroupDto
                    {
                        GroupName = NormalizeGroupName(group.GroupId),
                        EventName = eventName,
                        Rankings = rankings
                    };
                })
                .ToList();
        }
        catch
        {
            return new List<GroupDto>();
        }
    }

    private static string NormalizeGroupName(string groupId)
    {
        if (string.Equals(groupId, "Final-Group", StringComparison.OrdinalIgnoreCase))
            return "Final Group-finalStandings";

        if (string.Equals(groupId, "Tiebreaker", StringComparison.OrdinalIgnoreCase))
            return "Tiebreaker";

        if (groupId.StartsWith("Group-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(groupId.Substring("Group-".Length), out var groupNumber))
        {
            return $"Group {ConvertGroupNumberToLabel(groupNumber)}";
        }

        return groupId;
    }

    private static string ConvertGroupNumberToLabel(int groupNumber)
    {
        if (groupNumber <= 0)
            return groupNumber.ToString();

        var label = string.Empty;
        var value = groupNumber;
        while (value > 0)
        {
            value--;
            label = (char)('A' + (value % 26)) + label;
            value /= 26;
        }

        return label;
    }

    /// <summary>
    /// Calculate current standings from match results using tournament scoring (3 pts for win, 1 for draw, 0 for loss)
    /// </summary>
    private List<TeamStandingDto> CalculateStandingsFromMatches(TournamentInfo tournamentInfo)
    {
        // Aggregate tournament points from all matches using scoring system
        var botScores = new Dictionary<string, (int wins, int tournamentPoints)>();

        // Initialize with all bots
        foreach (var bot in tournamentInfo.Bots)
        {
            botScores[bot.TeamName] = (0, 0);
        }

        // Calculate wins and tournament points from match results
        foreach (var match in tournamentInfo.MatchResults ?? new List<MatchResult>())
        {
            // Use scoring system to calculate tournament points (3 for win, 1 for draw, 0 for loss)
            var (player1Points, player2Points) = _scoringSystem.CalculateMatchScore(match);

            // Handle Bot1
            if (!botScores.ContainsKey(match.Bot1Name))
                botScores[match.Bot1Name] = (0, 0);
            
            var bot1Stats = botScores[match.Bot1Name];
            bot1Stats.tournamentPoints += player1Points;
            if (match.Outcome == MatchOutcome.Player1Wins || match.Outcome == MatchOutcome.Player2Error)
                bot1Stats.wins++;
            botScores[match.Bot1Name] = bot1Stats;

            // Handle Bot2
            if (!botScores.ContainsKey(match.Bot2Name))
                botScores[match.Bot2Name] = (0, 0);
            
            var bot2Stats = botScores[match.Bot2Name];
            bot2Stats.tournamentPoints += player2Points;
            if (match.Outcome == MatchOutcome.Player2Wins || match.Outcome == MatchOutcome.Player1Error)
                bot2Stats.wins++;
            botScores[match.Bot2Name] = bot2Stats;
        }

        // Sort by tournament points first, then by wins
        var sortedStandings = botScores
            .OrderByDescending(kv => kv.Value.tournamentPoints)
            .ThenByDescending(kv => kv.Value.wins)
            .Select((kv, index) => new TeamStandingDto
            {
                Rank = index + 1,
                TeamName = kv.Key,
                TotalPoints = kv.Value.tournamentPoints
            })
            .ToList();

        return sortedStandings;
    }
}
