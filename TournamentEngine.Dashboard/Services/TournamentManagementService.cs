using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Api.Models;
using Microsoft.AspNetCore.SignalR;
using TournamentEngine.Dashboard.Hubs;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing tournament lifecycle operations (start, pause, resume, stop, clear, rerun).
/// Tracks management state and enforces valid state transitions.
/// Integrates with TournamentSeriesManager to orchestrate actual tournament execution.
/// </summary>
public class TournamentManagementService
{
    private readonly StateManagerService _stateManager;
    private readonly BotDashboardService _botDashboard;
    private readonly IHubContext<TournamentHub> _hubContext;
    private readonly TournamentSeriesManager? _seriesManager;
    private readonly ILogger<TournamentManagementService> _logger;
    private ManagementStateDto _managementState;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    
    // Tournament execution tracking
    private CancellationTokenSource? _executionCancellation;
    private Task? _executionTask;
    private bool _isPaused;

    public TournamentManagementService(
        StateManagerService stateManager,
        BotDashboardService botDashboard,
        IHubContext<TournamentHub> hubContext,
        TournamentSeriesManager seriesManager,
        ILogger<TournamentManagementService> logger)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _botDashboard = botDashboard ?? throw new ArgumentNullException(nameof(botDashboard));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _managementState = new ManagementStateDto
        {
            Status = ManagementRunState.NotStarted,
            Message = "Waiting for tournament start",
            BotsReady = false,
            BotCount = 0,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets current management state.
    /// </summary>
    public async Task<ManagementStateDto> GetStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            return new ManagementStateDto
            {
                Status = _managementState.Status,
                Message = _managementState.Message,
                BotsReady = _managementState.BotsReady,
                BotCount = _managementState.BotCount,
                LastAction = _managementState.LastAction,
                LastActionAt = _managementState.LastActionAt,
                LastUpdated = _managementState.LastUpdated
            };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Checks if bots are ready to start a tournament.
    /// </summary>
    public async Task<(bool ready, string message, int botCount)> CheckBotsReadyAsync()
    {
        try
        {
            var bots = await _botDashboard.GetAllBotsAsync();
            var validBots = bots.Where(b => b.Status == TournamentEngine.Api.Models.ValidationStatus.Valid).Count();

            var botCount = bots.Count;
            var allValid = validBots >= 2;
            var noValidationInProgress = !bots.Any(b => 
                b.Status == TournamentEngine.Api.Models.ValidationStatus.ValidationInProgress);

            var ready = allValid && noValidationInProgress;
            var message = !allValid ? "Need at least 2 valid bots"
                : !noValidationInProgress ? "Validation in progress, please wait"
                : "All bots ready";

            return (ready, message, botCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking bot readiness");
            return (false, $"Error checking bot readiness: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Starts a new tournament (must be in NotStarted or Stopped state).
    /// </summary>
    public async Task<Result> StartAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            // Validate state transition
            if (_managementState.Status != ManagementRunState.NotStarted 
                && _managementState.Status != ManagementRunState.Stopped)
            {
                return Result.Failure($"Cannot start from {_managementState.Status} state");
            }

            // Check bots ready
            var (ready, msg, botCount) = await CheckBotsReadyAsync();
            if (!ready)
            {
                return Result.Failure($"Bots not ready: {msg}");
            }

            // Update management state
            _managementState.Status = ManagementRunState.Running;
            _managementState.Message = "Tournament running";
            _managementState.BotsReady = true;
            _managementState.BotCount = botCount;
            _managementState.LastAction = "Start";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;
            _isPaused = false;

            _logger.LogInformation("Tournament started with {BotCount} bots", botCount);

            // Broadcast state to UI
            await BroadcastStateAsync();

            // Update the main dashboard state manager to show tournament is running
            var dashboardState = new DashboardStateDto
            {
                Status = TournamentStatus.InProgress,
                Message = $"Tournament running with {botCount} bots",
                LastUpdated = DateTime.UtcNow
            };
            await _stateManager.UpdateStateAsync(dashboardState);

            // Start tournament execution in background if series manager is available
            if (_seriesManager != null)
            {
                _executionCancellation = new CancellationTokenSource();
                _executionTask = ExecuteTournamentSeriesAsync(botCount, _executionCancellation.Token);
            }
            else
            {
                // Fallback: Start simulation mode with fake match events
                _executionCancellation = new CancellationTokenSource();
                _executionTask = SimulateTournamentAsync(botCount, _executionCancellation.Token);
            }

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Pauses a running tournament.
    /// </summary>
    public async Task<Result> PauseAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_managementState.Status != ManagementRunState.Running)
            {
                return Result.Failure($"Cannot pause from {_managementState.Status} state");
            }

            _isPaused = true;
            _managementState.Status = ManagementRunState.Paused;
            _managementState.Message = "Tournament paused";
            _managementState.LastAction = "Pause";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Tournament paused");
            await BroadcastStateAsync();

            // Update main dashboard state
            var dashboardState = new DashboardStateDto
            {
                Status = TournamentStatus.Paused,
                Message = "Tournament paused",
                LastUpdated = DateTime.UtcNow
            };
            await _stateManager.UpdateStateAsync(dashboardState);

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Resumes a paused tournament.
    /// </summary>
    public async Task<Result> ResumeAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_managementState.Status != ManagementRunState.Paused)
            {
                return Result.Failure($"Cannot resume from {_managementState.Status} state");
            }

            _isPaused = false;
            _managementState.Status = ManagementRunState.Running;
            _managementState.Message = "Tournament resumed";
            _managementState.LastAction = "Resume";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Tournament resumed");
            await BroadcastStateAsync();

            // Update main dashboard state
            var dashboardState = new DashboardStateDto
            {
                Status = TournamentStatus.InProgress,
                Message = "Tournament resumed",
                LastUpdated = DateTime.UtcNow
            };
            await _stateManager.UpdateStateAsync(dashboardState);

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Stops the current tournament.
    /// </summary>
    public async Task<Result> StopAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_managementState.Status == ManagementRunState.NotStarted)
            {
                return Result.Failure("No tournament to stop");
            }

            // Cancel execution task if running
            if (_executionCancellation != null && !_executionCancellation.Token.IsCancellationRequested)
            {
                _executionCancellation.Cancel();
            }

            _isPaused = false;
            _managementState.Status = ManagementRunState.Stopped;
            _managementState.Message = "Tournament stopped";
            _managementState.LastAction = "Stop";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Tournament stopped");
            await BroadcastStateAsync();

            // Update main dashboard state
            var dashboardState = new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Tournament stopped",
                LastUpdated = DateTime.UtcNow
            };
            await _stateManager.UpdateStateAsync(dashboardState);

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Clears all bot submissions and resets management state.
    /// </summary>
    public async Task<Result> ClearSubmissionsAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_managementState.Status == ManagementRunState.Running)
            {
                return Result.Failure("Cannot clear submissions while tournament is running");
            }

            // Clear bots from storage (this will need coordination with API)
            // For now, just reset management state
            _managementState.Status = ManagementRunState.NotStarted;
            _managementState.Message = "Submissions cleared";
            _managementState.BotCount = 0;
            _managementState.BotsReady = false;
            _managementState.LastAction = "Clear";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Bot submissions cleared");

            // Clear dashboard state
            await _stateManager.ClearStateAsync();
            await BroadcastStateAsync();

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Re-runs the last tournament configuration.
    /// </summary>
    public async Task<Result> RerunAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            // Check if we have valid bots to rerun
            var (ready, msg, botCount) = await CheckBotsReadyAsync();
            if (!ready)
            {
                return Result.Failure($"Cannot rerun: {msg}");
            }

            _managementState.Status = ManagementRunState.Running;
            _managementState.Message = "Tournament re-running";
            _managementState.BotCount = botCount;
            _managementState.BotsReady = true;
            _managementState.LastAction = "Rerun";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("Tournament re-running with {BotCount} bots", botCount);

            // Clear previous results but keep bots
            await _stateManager.ClearStateAsync();
            await BroadcastStateAsync();

            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Executes the tournament series with the given number of bots.
    /// Respects pause state and cancellation token.
    /// </summary>
    private async Task ExecuteTournamentSeriesAsync(int botCount, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting tournament series execution with {BotCount} bots", botCount);

            // Wait while paused
            while (_isPaused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Tournament execution cancelled before starting");
                return;
            }

            var bots = await _botDashboard.LoadValidBotInfosAsync(cancellationToken);
            _logger.LogInformation("Loaded {Count} valid bots for tournament", bots.Count);

            // TEMPORARY: Limit to 12 bots for faster testing (12 bots = 66 matches instead of 4,656!)
            if (bots.Count > 12)
            {
                bots = bots.Take(12).ToList();
                _logger.LogWarning("LIMITED to {Count} bots for faster testing", bots.Count);
            }

            if (bots.Count < 2)
            {
                _logger.LogError("No valid bots available for tournament execution");
                await _stateLock.WaitAsync();
                try
                {
                    _managementState.Status = ManagementRunState.Error;
                    _managementState.Message = "No valid bots available";
                    _managementState.LastUpdated = DateTime.UtcNow;
                    await BroadcastStateAsync();
                }
                finally
                {
                    _stateLock.Release();
                }
                return;
            }

            var initialLeaderboard = bots
                .Select(bot => bot.TeamName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(teamName => teamName, StringComparer.OrdinalIgnoreCase)
                .Select((teamName, index) => new TeamStandingDto
                {
                    Rank = index + 1,
                    TeamName = teamName,
                    TotalPoints = 0,
                    TournamentWins = 0,
                    TotalWins = 0,
                    TotalLosses = 0,
                    RankChange = 0
                })
                .ToList();

            await _stateManager.UpdateStateAsync(new DashboardStateDto
            {
                Status = TournamentStatus.InProgress,
                Message = "Tournament initialized - waiting for first matches",
                OverallLeaderboard = initialLeaderboard,
                LastUpdated = DateTime.UtcNow
            });

            // Create a minimal tournament series config with default settings
            var baseConfig = new TournamentConfig
            {
                ImportTimeout = TimeSpan.FromSeconds(10),
                MoveTimeout = TimeSpan.FromSeconds(2),

                MemoryLimitMB = 512,
                MaxRoundsRPSLS = 50
            };

            var seriesConfig = new TournamentSeriesConfig
            {
                GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame },
                BaseConfig = baseConfig,
                SeriesName = "Dashboard Tournament Series",
                AggregateScores = true
            };

            // Run the tournament series
            _logger.LogInformation("Starting tournament series with {BotCount} bots...", bots.Count);
            var result = await _seriesManager!.RunSeriesAsync(bots, seriesConfig, cancellationToken);

            _logger.LogInformation("Tournament series completed: Champion={Champion}, Tournaments={Count}, TotalMatches={TotalMatches}",
                result?.SeriesChampion ?? "N/A", 
                result?.Tournaments.Count ?? 0,
                result?.Tournaments.Sum(t => t.MatchResults.Count) ?? 0);

            if (result != null)
            {
                _logger.LogInformation("Tournament series completed successfully with champion: {Champion}", result.SeriesChampion);
                await _stateLock.WaitAsync();
                try
                {
                    _managementState.Status = ManagementRunState.Completed;
                    _managementState.Message = "Tournament series completed";
                    _managementState.LastUpdated = DateTime.UtcNow;
                    await BroadcastStateAsync();

                    // Build complete final dashboard state with all match results and standings
                    try
                    {
                        var allMatches = new List<RecentMatchDto>();
                        var allStandings = new List<TeamStandingDto>();

                        _logger.LogInformation("Building final state: {Tournaments} tournaments, {Standings} standings",
                            result.Tournaments.Count, result.SeriesStandings.Count);

                        // Collect all matches from all tournaments in the series
                        foreach (var tournament in result.Tournaments)
                        {
                            _logger.LogInformation("Tournament {Id}: {Matches} matches", tournament.TournamentId, tournament.MatchResults.Count);
                            foreach (var matchResult in tournament.MatchResults)
                            {
                                allMatches.Add(new RecentMatchDto
                                {
                                    MatchId = Guid.NewGuid().ToString(),
                                    TournamentId = tournament.TournamentId,
                                    TournamentName = $"{tournament.GameType} Tournament",
                                    Bot1Name = matchResult.Bot1Name,
                                    Bot2Name = matchResult.Bot2Name,
                                    Outcome = matchResult.Outcome,
                                    GameType = tournament.GameType,
                                    CompletedAt = DateTime.UtcNow,
                                    Bot1Score = matchResult.Bot1Score,
                                    Bot2Score = matchResult.Bot2Score,
                                    WinnerName = matchResult.WinnerName
                                });
                            }
                        }

                        // Build standings from series standings
                        var standingsList = result.SeriesStandings
                            .Select((standing, idx) => new TeamStandingDto
                            {
                                Rank = idx + 1,
                                TeamName = standing.BotName,
                                TotalPoints = standing.TotalSeriesScore,
                                TournamentWins = standing.TournamentsWon,
                                TotalWins = standing.TotalWins,
                                TotalLosses = standing.TotalLosses,
                                RankChange = 0
                            })
                            .ToList();

                        // Build tournament progress showing all events completed
                        var events = new List<EventInTournamentDto>();
                        for (int i = 0; i < result.Config.GameTypes.Count && i < result.Tournaments.Count; i++)
                        {
                            var gameType = result.Config.GameTypes[i];
                            var tournament = result.Tournaments[i];
                            events.Add(new EventInTournamentDto
                            {
                                EventNumber = i + 1,
                                GameType = gameType,
                                Status = EventItemStatus.Completed,
                                Champion = tournament.Champion,
                                StartTime = tournament.StartTime,
                                EndTime = tournament.EndTime ?? DateTime.UtcNow
                            });
                        }

                        var tournamentProgress = new TournamentProgressDto
                        {
                            TournamentId = result.SeriesId,
                            CompletedCount = result.TotalMatches,
                            TotalCount = result.TotalMatches,
                            CurrentEventIndex = result.Config.GameTypes.Count,
                            Events = events
                        };

                        var finalDashboardState = new DashboardStateDto
                        {
                            Status = TournamentStatus.Completed,
                            Message = $"Tournament series completed! Champion: {result.SeriesChampion}",
                            Champion = result.SeriesChampion,
                            TournamentProgress = tournamentProgress,
                            OverallLeaderboard = standingsList.Count > 0 ? standingsList : null,
                            RecentMatches = allMatches.Count > 0 ? allMatches.TakeLast(20).ToList() : new List<RecentMatchDto>(),
                            LastUpdated = DateTime.UtcNow
                        };

                        _logger.LogInformation("Updating state with final data: {Matches} matches, {Standings} standings, {Events} events",
                            allMatches.Count, standingsList.Count, events.Count);

                        await _stateManager.UpdateStateAsync(finalDashboardState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error building final tournament state");
                        throw;
                    }
                }
                finally
                {
                    _stateLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tournament series execution was cancelled");
            await _stateLock.WaitAsync();
            try
            {
                _managementState.Status = ManagementRunState.Stopped;
                _managementState.Message = "Tournament cancelled";
                _managementState.LastUpdated = DateTime.UtcNow;
                await BroadcastStateAsync();
                
                await _stateManager.UpdateStateAsync(new DashboardStateDto
                {
                    Status = TournamentStatus.Paused,
                    Message = "Tournament cancelled by user",
                    LastUpdated = DateTime.UtcNow
                });
            }
            finally
            {
                _stateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL ERROR executing tournament series: {Message}", ex.Message);
            await _stateLock.WaitAsync();
            try
            {
                _managementState.Status = ManagementRunState.Error;
                _managementState.Message = $"Tournament error: {ex.Message}";
                _managementState.LastUpdated = DateTime.UtcNow;
                await BroadcastStateAsync();
                
                // Also update the main dashboard state so UI shows error
                await _stateManager.UpdateStateAsync(new DashboardStateDto
                {
                    Status = TournamentStatus.Completed,
                    Message = $"Tournament failed: {ex.Message}",
                    LastUpdated = DateTime.UtcNow
                });
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }

    /// <summary>
    /// Broadcasts current management state to all connected clients via SignalR.
    /// </summary>
    private async Task BroadcastStateAsync()
    {
        try
        {
            var stateSnapshot = new ManagementStateDto
            {
                Status = _managementState.Status,
                Message = _managementState.Message,
                BotsReady = _managementState.BotsReady,
                BotCount = _managementState.BotCount,
                LastAction = _managementState.LastAction,
                LastActionAt = _managementState.LastActionAt,
                LastUpdated = _managementState.LastUpdated
            };

            await _hubContext.Clients.Group("TournamentViewers")
                .SendAsync("ManagementStateChanged", stateSnapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting management state");
        }
    }

    /// <summary>
    /// Simulates tournament progress with fake match events for testing/demo purposes.
    /// Creates complete tournament structure with events, matches, leaderboard, and standings.
    /// </summary>
    private async Task SimulateTournamentAsync(int botCount, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting tournament simulation with {BotCount} bots", botCount);
            var random = new Random();
            var tournamentId = Guid.NewGuid().ToString();
            var tournamentName = $"Productivity Tournament #{DateTime.UtcNow.ToString("MMdd")}";
            var botNames = Enumerable.Range(1, botCount).Select(i => $"Bot{i}").ToList();
            var botScores = new Dictionary<string, int>();
            var recentMatches = new List<RecentMatchDto>();
            
            // Initialize bot scores
            foreach (var bot in botNames)
            {
                botScores[bot] = 0;
            }

            var completedMatches = 0;
            var totalMatches = (botCount * (botCount - 1)) / 2; // Round-robin matches

            // Simulate tournament matches
            while (!cancellationToken.IsCancellationRequested && completedMatches < totalMatches)
            {
                // Respect pause state
                if (_isPaused)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                // Simulate a match taking 2-5 seconds
                await Task.Delay(random.Next(2000, 5000), cancellationToken);

                completedMatches++;

                // Pick two different bots
                var bot1 = botNames[random.Next(botNames.Count)];
                var bot2 = botNames[random.Next(botNames.Count)];
                
                if (bot1 == bot2)
                {
                    completedMatches--; // Retry this iteration
                    continue;
                }

                var winner = random.Next(0, 2) == 0 ? bot1 : bot2;
                var loser = winner == bot1 ? bot2 : bot1;
                
                // Update scores
                botScores[winner] += 3; // Win = 3 points
                botScores[loser] += 0;  // Loss = 0 points

                // Create match record
                var matchRecord = new RecentMatchDto
                {
                    MatchId = Guid.NewGuid().ToString(),
                    TournamentId = tournamentId,
                    TournamentName = tournamentName,
                    Bot1Name = bot1,
                    Bot2Name = bot2,
                    WinnerName = winner,
                    Outcome = winner == bot1 ? MatchOutcome.Player1Wins : MatchOutcome.Player2Wins,
                    Bot1Score = winner == bot1 ? 1 : 0,
                    Bot2Score = winner == bot2 ? 1 : 0,
                    CompletedAt = DateTime.UtcNow,
                    GameType = GameType.SecurityGame
                };
                recentMatches.Add(matchRecord);

                // Build leaderboard (top 5)
                var leaderboard = botScores
                    .OrderByDescending(x => x.Value)
                    .Take(5)
                    .Select((x, idx) => new TeamStandingDto
                    {
                        Rank = idx + 1,
                        TeamName = x.Key,
                        TotalPoints = x.Value,
                        TournamentWins = x.Value / 3, // Simplified: wins = points/3
                        TotalWins = x.Value / 3,
                        TotalLosses = completedMatches - (x.Value / 3),
                        RankChange = 0
                    })
                    .ToList();

                // Create an event (treat entire tournament as one event for simplicity)
                var currentEvent = new CurrentEventDto
                {
                    TournamentNumber = 1,
                    GameType = GameType.SecurityGame,
                    Stage = TournamentStage.GroupStage,
                    CurrentRound = 1,
                    TotalRounds = 1,
                    MatchesCompleted = completedMatches,
                    TotalMatches = totalMatches,
                    ProgressPercentage = (double)completedMatches / totalMatches * 100
                };

                // Update tournament progress
                var tournamentProgress = new TournamentProgressDto
                {
                    TournamentId = tournamentId,
                    CompletedCount = completedMatches,
                    TotalCount = totalMatches,
                    CurrentEventIndex = 0,
                    Events = new List<EventInTournamentDto>
                    {
                        new EventInTournamentDto
                        {
                            EventNumber = 1,
                            GameType = GameType.SecurityGame,
                            Status = EventItemStatus.InProgress,
                            Champion = leaderboard.FirstOrDefault()?.TeamName,
                            StartTime = DateTime.UtcNow.AddSeconds(-30),
                            EndTime = null
                        }
                    }
                };

                // Update dashboard state with full tournament data
                var dashboardState = new DashboardStateDto
                {
                    TournamentId = tournamentId,
                    TournamentName = tournamentName,
                    Champion = leaderboard.FirstOrDefault()?.TeamName,
                    Status = TournamentStatus.InProgress,
                    Message = $"Match {completedMatches}/{totalMatches} complete: {winner} vs {loser}",
                    TournamentProgress = tournamentProgress,
                    CurrentEvent = currentEvent,
                    OverallLeaderboard = leaderboard,
                    RecentMatches = recentMatches.TakeLast(10).ToList(),
                    LastUpdated = DateTime.UtcNow
                };

                await _stateManager.UpdateStateAsync(dashboardState);
                var currentState = await _stateManager.GetCurrentStateAsync();
                
                // Broadcast state update to all connected clients
                await _hubContext.Clients.All.SendAsync("CurrentState", currentState);
                
                _logger.LogInformation("Match {Completed}/{Total}: {Bot1} vs {Bot2} - {Winner} wins", 
                    completedMatches, totalMatches, bot1, bot2, winner);
            }

            // Tournament completed
            if (completedMatches >= totalMatches)
            {
                await _stateLock.WaitAsync();
                try
                {
                    _managementState.Status = ManagementRunState.Completed;
                    _managementState.Message = $"Tournament completed! {completedMatches} matches played";
                    _managementState.LastUpdated = DateTime.UtcNow;
                    await BroadcastStateAsync();

                    // Build final leaderboard and state
                    var finalLeaderboard = botScores
                        .OrderByDescending(x => x.Value)
                        .Select((x, idx) => new TeamStandingDto
                        {
                            Rank = idx + 1,
                            TeamName = x.Key,
                            TotalPoints = x.Value,
                            TournamentWins = x.Value / 3,
                            TotalWins = x.Value / 3,
                            TotalLosses = completedMatches - (x.Value / 3),
                            RankChange = 0
                        })
                        .ToList();

                    var champion = finalLeaderboard.FirstOrDefault()?.TeamName;

                    var finalState = new DashboardStateDto
                    {
                        TournamentId = tournamentId,
                        TournamentName = tournamentName,
                        Champion = champion,
                        Status = TournamentStatus.Completed,
                        Message = $"Tournament completed! {completedMatches} matches played. Champion: {champion}",
                        OverallLeaderboard = finalLeaderboard,
                        RecentMatches = recentMatches.TakeLast(20).ToList(),
                        TournamentProgress = new TournamentProgressDto
                        {
                            TournamentId = tournamentId,
                            CompletedCount = completedMatches,
                            TotalCount = totalMatches,
                            CurrentEventIndex = 0,
                            Events = new List<EventInTournamentDto>
                            {
                                new EventInTournamentDto
                                {
                                    EventNumber = 1,
                                    GameType = GameType.SecurityGame,
                                    Status = EventItemStatus.Completed,
                                    Champion = champion,
                                    StartTime = DateTime.UtcNow.AddSeconds(-30),
                                    EndTime = DateTime.UtcNow
                                }
                            }
                        },
                        LastUpdated = DateTime.UtcNow
                    };
                    await _stateManager.UpdateStateAsync(finalState);
                    var currentState = await _stateManager.GetCurrentStateAsync();
                    
                    // Broadcast final state to all clients
                    await _hubContext.Clients.All.SendAsync("CurrentState", currentState);
                    await _hubContext.Clients.All.SendAsync("TournamentCompleted", new { TournamentId = tournamentId, Champion = champion });
                    
                    _logger.LogInformation("Tournament simulation completed with {Matches} matches. Champion: {Champion}", 
                        completedMatches, champion);
                }
                finally
                {
                    _stateLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tournament simulation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tournament simulation");
            await _stateLock.WaitAsync();
            try
            {
                _managementState.Status = ManagementRunState.Error;
                _managementState.Message = $"Simulation error: {ex.Message}";
                _managementState.LastUpdated = DateTime.UtcNow;
                await BroadcastStateAsync();
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }

    /// <summary>
    /// Simple result type for operation outcomes.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;

        public static Result Success() => new() { IsSuccess = true, Message = "OK" };
        public static Result Failure(string message) => new() { IsSuccess = false, Message = message };
    }
}
