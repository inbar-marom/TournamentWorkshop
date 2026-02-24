using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.Utilities;

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
    private readonly TournamentSeriesManager _seriesManager;
    private readonly ITournamentManager _tournamentManager;
    private readonly ILogger<TournamentManagementService> _logger;
    private ManagementStateDto _managementState;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    
    // Tournament execution tracking
    private CancellationTokenSource? _executionCancellation;
    private Task? _executionTask;
    private bool _isPaused;
    
    // Scheduled start tracking
    private System.Threading.Timer? _scheduledStartTimer;
    private int _scheduledStartBotCount;

    public TournamentManagementService(
        StateManagerService stateManager,
        BotDashboardService botDashboard,
        TournamentSeriesManager seriesManager,
        ITournamentManager tournamentManager,
        ILogger<TournamentManagementService> logger)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _botDashboard = botDashboard ?? throw new ArgumentNullException(nameof(botDashboard));
        _seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
        _tournamentManager = tournamentManager ?? throw new ArgumentNullException(nameof(tournamentManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _managementState = new ManagementStateDto
        {
            Status = ManagementRunState.NotStarted,
            Message = "Waiting for tournament start",
            BotsReady = false,
            BotCount = 0,
            LastUpdated = DateTime.UtcNow,
            FastMatchThresholdSeconds = 5,
            ScheduledStartTime = null,
            CurrentIsraelTime = DateTime.UtcNow
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
                LastUpdated = _managementState.LastUpdated,
                FastMatchThresholdSeconds = _managementState.FastMatchThresholdSeconds,
                ScheduledStartTime = _managementState.ScheduledStartTime,
                CurrentIsraelTime = _managementState.CurrentIsraelTime
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
    /// Sets the fast match reporting delay threshold in seconds.
    /// </summary>
    public async Task<Result> SetFastMatchThresholdAsync(int thresholdSeconds)
    {
        if (thresholdSeconds < 1 || thresholdSeconds > 120)
            return Result.Failure("Threshold must be between 1 and 120 seconds");

        await _stateLock.WaitAsync();
        try
        {
            _managementState.FastMatchThresholdSeconds = thresholdSeconds;
            _managementState.LastAction = "Set Fast Match Threshold";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;
            
            // Update the TournamentManager instance directly
            if (_tournamentManager is TournamentManager manager)
            {
                manager.FastMatchThresholdSeconds = thresholdSeconds;
            }
            
            _logger.LogInformation("Fast match threshold set to {Threshold} seconds", thresholdSeconds);
            await BroadcastStateAsync();
            
            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Sets the scheduled start time for the tournament (UTC).
    /// If scheduledTime is null or in the past, tournament starts immediately.
    /// </summary>
    public async Task<Result> SetScheduledStartTimeAsync(DateTime? scheduledTime)
    {
        _logger.LogWarning("🔧 SetScheduledStartTimeAsync called with: {Time}", scheduledTime);
        await _stateLock.WaitAsync();
        try
        {
            if (scheduledTime.HasValue && scheduledTime < DateTime.UtcNow)
            {
                _logger.LogError("❌ Scheduled time {Time} is in the past (now: {Now})", scheduledTime, DateTime.UtcNow);
                return Result.Failure("Scheduled start time cannot be in the past");
            }

            _managementState.ScheduledStartTime = scheduledTime;
            _managementState.LastAction = scheduledTime.HasValue ? "Set Scheduled Start Time" : "Clear Scheduled Start Time";
            _managementState.LastActionAt = DateTime.UtcNow;
            _managementState.LastUpdated = DateTime.UtcNow;
            
            if (scheduledTime.HasValue)
            {
                _logger.LogWarning("✅ Tournament scheduled to start at {StartTime} UTC", scheduledTime);
                
                // Automatically initiate the scheduled start timer
                var delayMs = (int)(scheduledTime.Value - DateTime.UtcNow).TotalMilliseconds;
                _logger.LogWarning("🕐 AUTO-SCHEDULING: Tournament will start in {DelayMs}ms ({Seconds}s)", 
                    delayMs, delayMs / 1000);
                
                // Check bots ready for the scheduled start
                var (ready, msg, botCount) = await CheckBotsReadyAsync();
                if (!ready)
                {
                    _logger.LogError("❌ Cannot schedule tournament: {Msg}", msg);
                    _managementState.ScheduledStartTime = null; // Clear the scheduled time
                    await BroadcastStateAsync();
                    return Result.Failure($"Bots not ready: {msg}");
                }
                
                // Dispose any existing timer
                _scheduledStartTimer?.Dispose();
                
                // Store bot count for scheduled callback
                _scheduledStartBotCount = botCount;
                
                // Create a timer that fires once at the scheduled time
                _scheduledStartTimer = new System.Threading.Timer(
                    async _ => await OnScheduledStartTimeReached(),
                    null,
                    TimeSpan.FromMilliseconds(Math.Max(1, delayMs)),
                    Timeout.InfiniteTimeSpan);
                
                // Update state to show it's scheduled
                _managementState.Status = ManagementRunState.NotStarted; // Keep as NotStarted but scheduled
                _managementState.Message = $"Tournament scheduled to start at {scheduledTime.Value:HH:mm:ss} UTC";
                _managementState.BotsReady = true;
                _managementState.BotCount = botCount;
                
                _logger.LogWarning("⏱️ Auto-start timer created and activated!");
            }
            else
            {
                _logger.LogWarning("🚫 Tournament scheduled start time cleared");
                // Cancel the timer if clearing scheduled time
                _scheduledStartTimer?.Dispose();
                _scheduledStartTimer = null;
            }
            
            await BroadcastStateAsync();
            
            // Update dashboard state with new scheduled time
            var currentDashboardState = await _stateManager.GetCurrentStateAsync();
            currentDashboardState.ScheduledStartTime = scheduledTime;
            await _stateManager.UpdateStateAsync(currentDashboardState);
            
            return Result.Success();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Starts a new tournament (must be in NotStarted or Stopped state).
    /// </summary>
    public async Task<Result> StartAsync()
    {
        _logger.LogWarning("🏁 StartAsync() called");
        await _stateLock.WaitAsync();
        try
        {
            _logger.LogWarning("📊 Current state: {Status}", _managementState.Status);
            _logger.LogWarning("⏰ Scheduled start time: {Time}", _managementState.ScheduledStartTime);
            _logger.LogWarning("🕐 Current UTC time: {Time}", DateTime.UtcNow);
            
            // Validate state transition
            if (_managementState.Status != ManagementRunState.NotStarted 
                && _managementState.Status != ManagementRunState.Stopped)
            {
                _logger.LogError("❌ Cannot start from {Status} state", _managementState.Status);
                return Result.Failure($"Cannot start from {_managementState.Status} state");
            }

            // Check bots ready
            var (ready, msg, botCount) = await CheckBotsReadyAsync();
            _logger.LogWarning("🤖 Bots ready: {Ready}, Count: {Count}, Message: {Msg}", ready, botCount, msg);
            if (!ready)
            {
                return Result.Failure($"Bots not ready: {msg}");
            }

            // Check if tournament is scheduled for future time
            bool hasScheduledTime = _managementState.ScheduledStartTime.HasValue;
            bool isFutureTime = hasScheduledTime && _managementState.ScheduledStartTime > DateTime.UtcNow;
            _logger.LogWarning("🔍 Has scheduled time: {Has}, Is future: {IsFuture}", hasScheduledTime, isFutureTime);
            
            if (_managementState.ScheduledStartTime.HasValue && _managementState.ScheduledStartTime > DateTime.UtcNow)
            {
                // Wait until scheduled time (using Timer for reliability)
                var delayMs = (int)(_managementState.ScheduledStartTime.Value - DateTime.UtcNow).TotalMilliseconds;
                _logger.LogWarning("🕐 SCHEDULED START: Tournament will start in {DelayMs}ms ({Seconds}s) at {ScheduledTime} UTC", 
                    delayMs, delayMs / 1000, _managementState.ScheduledStartTime);
                
                // Dispose any existing timer
                _scheduledStartTimer?.Dispose();
                
                // Store bot count for scheduled callback
                _scheduledStartBotCount = botCount;
                
                // Create a timer that fires once at the scheduled time
                _scheduledStartTimer = new System.Threading.Timer(
                    async _ => await OnScheduledStartTimeReached(),
                    null,
                    TimeSpan.FromMilliseconds(Math.Max(1, delayMs)),
                    Timeout.InfiniteTimeSpan);
                
                _logger.LogWarning("⏱️ Timer created and started for scheduled tournament start");
                
                // Send signal that tournament is waiting
                _managementState.Status = ManagementRunState.Running;
                _managementState.Message = "Tournament scheduled, waiting to start...";
                _managementState.BotsReady = true;
                _managementState.BotCount = botCount;
                _managementState.LastAction = "Start Scheduled";
                _managementState.LastActionAt = DateTime.UtcNow;
                _managementState.LastUpdated = DateTime.UtcNow;
                _isPaused = false;
                
                await BroadcastStateAsync();
                return Result.Success();
            }

            // Start immediately
            return await ExecuteStartAsync(botCount);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Internal helper to execute tournament start (no lock needed, called from StartAsync or scheduled callback).
    /// </summary>
    private async Task<Result> ExecuteStartAsync(int botCount)
    {
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
            LastUpdated = DateTime.UtcNow,
            ScheduledStartTime = _managementState.ScheduledStartTime
        };
        await _stateManager.UpdateStateAsync(dashboardState);

        // Start tournament execution in background
        _executionCancellation = new CancellationTokenSource();
        _executionTask = ExecuteTournamentSeriesAsync(botCount, _executionCancellation.Token);

        return Result.Success();
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

            if (_tournamentManager is TournamentManager manager)
            {
                manager.FastMatchThresholdSeconds = _managementState.FastMatchThresholdSeconds;
                _logger.LogInformation(
                    "Applying fast match reporting delay threshold: {ThresholdSeconds}s to tournament manager",
                    manager.FastMatchThresholdSeconds);
            }


            if (bots.Count < 2)
            {
                _logger.LogError("No valid bots available for tournament execution (found {Count})", bots.Count);
                await _stateLock.WaitAsync();
                try
                {
                    _managementState.Status = ManagementRunState.Error;
                    _managementState.Message = $"No valid bots available (found {bots.Count}, need at least 2)";
                    _managementState.LastUpdated = DateTime.UtcNow;
                    await BroadcastStateAsync();
                }
                finally
                {
                    _stateLock.Release();
                }
                
                // CRITICAL: Also update the dashboard state so the UI shows the error
                // Without this, the dashboard stays at "RUNNING" forever with no data
                await _stateManager.UpdateStateAsync(new DashboardStateDto
                {
                    Status = TournamentStatus.Completed,
                    Message = $"Tournament failed: No valid bots available (found {bots.Count}, need at least 2)",
                    LastUpdated = DateTime.UtcNow
                });
                
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

            // Create tournament config with up to 10 groups while avoiding groups with fewer than 2 bots.
            // This means: group count <= 10 and group count <= bots.Count / 2.
            var computedGroupCount = Math.Clamp(
                Math.Min(10, bots.Count / 2),
                1,
                10);

            _logger.LogInformation(
                "Tournament grouping: {BotCount} bots -> {GroupCount} groups (min 2 bots/group), finalists/group={FinalistsPerGroup}",
                bots.Count,
                computedGroupCount,
                1);

            // Create a minimal tournament series config with default settings
            var cpuCount = Math.Max(2, Environment.ProcessorCount);
            _logger.LogInformation("Using {CpuCount} parallel matches (based on CPU count)", cpuCount);

            var baseConfig = new TournamentConfig
            {
                ImportTimeout = TimeSpan.FromSeconds(10),
                MoveTimeout = TimeSpan.FromSeconds(2),

                MemoryLimitMB = 512,
                MaxRoundsRPSLS = 10,
                MaxRoundsBlotto = 5,
                MaxRoundsPenaltyKicks = 10,
                MaxRoundsSecurityGame = 5,
                GroupCount = computedGroupCount,
                FinalistsPerGroup = 1,
                MaxParallelMatches = cpuCount
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
            var result = await _seriesManager.RunSeriesAsync(bots, seriesConfig, cancellationToken);

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
                            OverallLeaderboard = standingsList,
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
    /// Called by timer when scheduled start time is reached.
    /// </summary>
    private async Task OnScheduledStartTimeReached()
    {
        try
        {
            _logger.LogWarning("🚀 SCHEDULED START TIME REACHED! Starting tournament now...");
            
            // Acquire lock
            await _stateLock.WaitAsync();
            try
            {
                _logger.LogWarning("🔒 Lock acquired, calling ExecuteStartAsync with {BotCount} bots", _scheduledStartBotCount);
                var result = await ExecuteStartAsync(_scheduledStartBotCount);
                if (!result.IsSuccess)
                {
                    _logger.LogError("❌ Scheduled tournament start FAILED: {Message}", result.Message);
                }
                else
                {
                    _logger.LogWarning("✅ Scheduled tournament start SUCCEEDED!");
                }
            }
            finally
            {
                _stateLock.Release();
                _logger.LogWarning("🔓 Lock released after scheduled start");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 CRITICAL ERROR in scheduled tournament start callback");
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
                LastUpdated = _managementState.LastUpdated,
                FastMatchThresholdSeconds = _managementState.FastMatchThresholdSeconds,
                ScheduledStartTime = _managementState.ScheduledStartTime,
                CurrentIsraelTime = TimezoneHelper.GetNowIsrael()
            };

            // SignalR removed - management state updated via polling only
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting management state");
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
