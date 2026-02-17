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
    private readonly ITournamentEngine _engine;
    private readonly IGameRunner _gameRunner;
    private readonly IScoringSystem _scoringSystem;
    private readonly ITournamentEventPublisher? _eventPublisher;
    private static int _tournamentCounter = 0;

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

            Console.WriteLine($"[TournamentManager] Starting execution of {matches.Count} matches with max concurrency {Environment.ProcessorCount * 2}");
            
            // Execute all matches in the current stage concurrently
            // THREAD SAFETY: All matches within a stage run in parallel via Task.WhenAll
            // The engine's RecordMatchResult() is thread-safe (uses lock)
            // We wait for ALL matches to complete before advancing to next stage
            // Throttle to prevent overwhelming the system (2x processor count is reasonable)
            var maxConcurrency = Environment.ProcessorCount * 2;
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
                        if (index < 5)
                        {
                            Console.WriteLine($"[TournamentManager] Starting match {index + 1}: {bot1.TeamName} vs {bot2.TeamName}");
                        }
                        
                        var result = await _gameRunner.ExecuteMatch(bot1, bot2, gameType, cancellationToken);
                        
                        if (index < 5)
                        {
                            Console.WriteLine($"[TournamentManager] Completed match {index + 1}");
                        }
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

            Console.WriteLine($"[TournamentManager] Created {tasks.Count} tasks, waiting for completion...");
            
            // Monitor task completion with timeout
            var completedCount = 0;
            var monitorTask = Task.Run(async () =>
            {
                while (completedCount < tasks.Count)
                {
                    await Task.Delay(5000); // Check every 5 seconds
                    var completed = tasks.Count(t => t.IsCompleted);
                    var faulted = tasks.Count(t => t.IsFaulted);
                    var canceled = tasks.Count(t => t.IsCanceled);
                    var running = tasks.Count - completed;
                    
                    if (completed != completedCount)
                    {
                        completedCount = completed;
                        Console.WriteLine($"[TournamentManager] Progress: {completed}/{tasks.Count} completed, {faulted} faulted, {canceled} canceled, {running} still running");
                        
                        if (faulted > 0)
                        {
                            var firstFaulted = tasks.FirstOrDefault(t => t.IsFaulted);
                            if (firstFaulted?.Exception != null)
                            {
                                Console.WriteLine($"[TournamentManager] First faulted task exception: {firstFaulted.Exception.InnerException?.Message ?? firstFaulted.Exception.Message}");
                            }
                        }
                    }
                }
            });
            
            // STAGE SYNCHRONIZATION: Wait for ALL matches in this stage to complete
            var results = await Task.WhenAll(tasks);
            Console.WriteLine($"[TournamentManager] All {results.Length} matches completed, recording results...");

            // Record all results and publish events concurrently
            // Recording is thread-safe (engine uses locks), and we publish all events in parallel
            var publishTasks = new List<Task>();
            
            foreach (var result in results)
            {
                _engine.RecordMatchResult(result);
                
                // Publish match completed event (fire in parallel, don't block)
                if (_eventPublisher != null)
                {
                    var groupLabel = _engine.GetMatchGroupLabel(result.Bot1Name, result.Bot2Name);
                    Console.WriteLine($"[TournamentManager] Match {result.Bot1Name} vs {result.Bot2Name} - GroupLabel: '{groupLabel}'");
                    
                    var matchEvent = new MatchCompletedDto
                    {
                        TournamentId = tournamentId,
                        TournamentName = tournamentName,
                        Bot1Name = result.Bot1Name,
                        Bot2Name = result.Bot2Name,
                        Outcome = result.Outcome,
                        GameType = gameType,
                        CompletedAt = DateTime.UtcNow,
                        Bot1Score = result.Bot1Score,
                        Bot2Score = result.Bot2Score,
                        MatchId = Guid.NewGuid().ToString(),
                        WinnerName = result.WinnerName,
                        GroupLabel = groupLabel
                    };
                    publishTasks.Add(_eventPublisher.PublishMatchCompletedAsync(matchEvent));
                }
            }
            
            // Wait for all event publishes to complete in parallel
            if (publishTasks.Count > 0)
            {
                await Task.WhenAll(publishTasks);
            }

            // Publish standings updated event after each round of matches
            if (_eventPublisher != null)
            {
                // Get current tournament info
                var currentTournamentInfo = _engine.GetTournamentInfo();
                var standings = CalculateStandingsFromMatches(currentTournamentInfo);
                
                var standingsEvent = new StandingsUpdatedDto
                {
                    TournamentId = tournamentId,
                    TournamentName = tournamentName,
                    OverallStandings = standings,
                    UpdatedAt = DateTime.UtcNow
                };
                await _eventPublisher.PublishStandingsUpdatedAsync(standingsEvent);
            }
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
