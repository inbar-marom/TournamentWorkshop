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
    private readonly ITournamentEventPublisher? _eventPublisher;

    public TournamentManager(ITournamentEngine engine, IGameRunner gameRunner, ITournamentEventPublisher? eventPublisher = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _gameRunner = gameRunner ?? throw new ArgumentNullException(nameof(gameRunner));
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

        // Publish tournament started event
        if (_eventPublisher != null)
        {
            var startedEvent = new TournamentStartedDto
            {
                GameType = gameType,
                TotalBots = bots.Count,
                StartedAt = DateTime.UtcNow,
                TotalGroups = 1,
                TournamentNumber = 1
            };
            await _eventPublisher.PublishTournamentStartedAsync(startedEvent);
        }

        // Main tournament loop - runs until engine signals completion
        while (!_engine.IsTournamentComplete())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get next batch of matches from the engine
            var matches = _engine.GetNextMatches();
            
            if (matches.Count == 0)
            {
                // No matches available - let engine advance to next phase/round
                _engine.AdvanceToNextRound();
                
                // Publish round started event
                if (_eventPublisher != null)
                {
                    var roundEvent = new RoundStartedDto
                    {
                        RoundNumber = _engine.GetCurrentRound(),
                        TotalMatches = 0,
                        StartedAt = DateTime.UtcNow,
                        Stage = "Regular"
                    };
                    await _eventPublisher.PublishRoundStartedAsync(roundEvent);
                }
                continue;
            }

            // Execute all matches in the current batch
            if (config.MaxParallelMatches > 1)
            {
                // Parallel execution with degree-of-parallelism cap
                using var semaphore = new SemaphoreSlim(config.MaxParallelMatches);
                var tasks = new List<Task<MatchResult>>(matches.Count);

                foreach (var (bot1, bot2) in matches)
                {
                    tasks.Add(ExecuteMatchWithThrottleAsync(
                        semaphore, bot1, bot2, gameType, cancellationToken));
                }

                // Wait for all matches to complete (preserves original order)
                var results = await Task.WhenAll(tasks);

                // Record results in original match order
                foreach (var result in results)
                {
                    _engine.RecordMatchResult(result);
                    
                    // Publish match completed event
                    if (_eventPublisher != null)
                    {
                        var matchEvent = new MatchCompletedDto
                        {
                            Bot1Name = result.Bot1Name,
                            Bot2Name = result.Bot2Name,
                            Outcome = result.Outcome,
                            GameType = gameType,
                            CompletedAt = DateTime.UtcNow,
                            Bot1Score = result.Bot1Score,
                            Bot2Score = result.Bot2Score,
                            MatchId = Guid.NewGuid().ToString(),
                            WinnerName = result.WinnerName
                        };
                        await _eventPublisher.PublishMatchCompletedAsync(matchEvent);
                    }
                }
            }
            else
            {
                // Sequential execution (default) - deterministic order
                foreach (var (bot1, bot2) in matches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var matchResult = await _gameRunner.ExecuteMatch(
                        bot1, bot2, gameType, cancellationToken);

                    _engine.RecordMatchResult(matchResult);
                    
                    // Publish match completed event
                    if (_eventPublisher != null)
                    {
                        var matchEvent = new MatchCompletedDto
                        {
                            Bot1Name = matchResult.Bot1Name,
                            Bot2Name = matchResult.Bot2Name,
                            Outcome = matchResult.Outcome,
                            GameType = gameType,
                            CompletedAt = DateTime.UtcNow,
                            Bot1Score = matchResult.Bot1Score,
                            Bot2Score = matchResult.Bot2Score,
                            MatchId = Guid.NewGuid().ToString(),
                            WinnerName = matchResult.WinnerName
                        };
                        await _eventPublisher.PublishMatchCompletedAsync(matchEvent);
                    }
                }
            }

            // Publish standings updated event after each round of matches
            if (_eventPublisher != null)
            {
                var standingsEvent = new StandingsUpdatedDto
                {
                    OverallStandings = new List<TeamStandingDto>(),
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

            var completedEvent = new TournamentCompletedDto
            {
                Champion = finalTournamentInfo.Champion ?? "Unknown",
                GameType = gameType,
                TotalMatches = finalTournamentInfo.MatchResults?.Count ?? 0,
                CompletedAt = DateTime.UtcNow,
                TournamentNumber = 1,
                TournamentId = finalTournamentInfo.TournamentId ?? Guid.NewGuid().ToString(),
                Duration = duration
            };
            await _eventPublisher.PublishTournamentCompletedAsync(completedEvent);
        }

        return finalTournamentInfo;
    }

    private async Task<MatchResult> ExecuteMatchWithThrottleAsync(
        SemaphoreSlim semaphore,
        IBot bot1,
        IBot bot2,
        GameType gameType,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await _gameRunner.ExecuteMatch(bot1, bot2, gameType, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
