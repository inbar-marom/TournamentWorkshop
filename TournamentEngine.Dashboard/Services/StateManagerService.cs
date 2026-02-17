using TournamentEngine.Core.Common.Dashboard;
using System.Collections.Concurrent;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Manages current tournament state and provides thread-safe access for dashboard clients
/// </summary>
public class StateManagerService
{
    private readonly ILogger<StateManagerService> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private DashboardStateDto? _currentState;
    private readonly ConcurrentQueue<RecentMatchDto> _recentMatches = new();
    private readonly Dictionary<string, List<TeamStandingDto>> _standingsByEventId = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxRecentMatches = 50;

    public StateManagerService(ILogger<StateManagerService> logger)
    {
        _logger = logger;
        _currentState = new DashboardStateDto
        {
            Status = TournamentStatus.NotStarted,
            Message = "Waiting for tournament to start...",
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the current tournament state snapshot
    /// </summary>
    public virtual async Task<DashboardStateDto> GetCurrentStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            // CRITICAL: Return a COPY of the state, not the shared instance
            // Otherwise concurrent requests modify the same object causing stale data
            var currentState = _currentState ?? new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "No tournament data available",
                LastUpdated = DateTime.UtcNow
            };
            
            // Create a fresh copy with recent matches populated
            var stateCopy = new DashboardStateDto
            {
                TournamentId = currentState.TournamentId,
                TournamentName = currentState.TournamentName,
                Champion = currentState.Champion,
                Status = currentState.Status,
                Message = currentState.Message,
                TournamentProgress = currentState.TournamentProgress,
                TournamentState = currentState.TournamentState,
                CurrentEvent = currentState.CurrentEvent,
                OverallLeaderboard = currentState.OverallLeaderboard,
                GroupStandings = currentState.GroupStandings,
                RecentMatches = GetRecentMatches(20),
                NextMatch = currentState.NextMatch,
                LastUpdated = DateTime.UtcNow
            };
            
            _logger.LogDebug("GetCurrentStateAsync: Status={Status}, RecentMatches={Count}", 
                stateCopy.Status, stateCopy.RecentMatches?.Count ?? 0);
            
            return stateCopy;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Updates the current tournament dashboard state
    /// </summary>
    public virtual async Task UpdateStateAsync(DashboardStateDto newState)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (newState.RecentMatches != null)
            {
                _recentMatches.Clear();
                foreach (var match in newState.RecentMatches)
                {
                    _recentMatches.Enqueue(match);
                }

                while (_recentMatches.Count > MaxRecentMatches)
                {
                    _recentMatches.TryDequeue(out _);
                }
            }

            if (_currentState?.TournamentState?.Steps?.Count > 0)
            {
                if (newState.TournamentState == null || newState.TournamentState.Steps.Count == 0)
                {
                    newState.TournamentState = _currentState.TournamentState;
                }
            }

            if (_currentState?.OverallLeaderboard?.Count > 0)
            {
                if (newState.OverallLeaderboard == null || newState.OverallLeaderboard.Count == 0)
                {
                    newState.OverallLeaderboard = _currentState.OverallLeaderboard;
                }
            }

            if (_standingsByEventId.Count > 0)
            {
                var cumulativeLeaderboard = BuildCumulativeLeaderboard();
                if (cumulativeLeaderboard.Count > 0)
                {
                    newState.OverallLeaderboard = cumulativeLeaderboard;
                }
            }

            _currentState = newState;
            _currentState.LastUpdated = DateTime.UtcNow;
            _logger.LogDebug("Dashboard state updated: {Status}", newState.Status);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Adds a completed match to recent matches queue
    /// </summary>
    public virtual void AddRecentMatch(MatchCompletedDto match)
    {
        // Convert MatchCompletedDto to RecentMatchDto
        var recentMatch = new RecentMatchDto
        {
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TournamentName = match.TournamentName,
            Bot1Name = match.Bot1Name,
            Bot2Name = match.Bot2Name,
            Outcome = match.Outcome,
            WinnerName = match.WinnerName,
            Bot1Score = match.Bot1Score,
            Bot2Score = match.Bot2Score,
            CompletedAt = match.CompletedAt,
            GameType = match.GameType,
            GroupLabel = match.GroupLabel
        };
        
        _recentMatches.Enqueue(recentMatch);
        
        // Keep only the most recent matches
        while (_recentMatches.Count > MaxRecentMatches)
        {
            _recentMatches.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets recent matches
    /// </summary>
    public virtual List<RecentMatchDto> GetRecentMatches(int count = 20)
    {
        return _recentMatches.TakeLast(count).Reverse().ToList();
    }

    /// <summary>
    /// Clears all state (for new tournament)
    /// </summary>
    public virtual async Task ClearStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState = new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "State cleared",
                LastUpdated = DateTime.UtcNow
            };
            _standingsByEventId.Clear();
            _recentMatches.Clear();
            _logger.LogInformation("Dashboard state cleared");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle EventStarted event (individual event/game type)
    /// </summary>
    public virtual async Task UpdateEventStartedAsync(EventStartedEventDto eventStarted)
    {
        await _stateLock.WaitAsync();
        try
        {
            // Update existing state instead of creating new one - preserves TournamentState
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.InProgress,
                LastUpdated = DateTime.UtcNow
            };
            
            _currentState.CurrentEvent = new CurrentEventDto
            {
                TournamentNumber = eventStarted.EventNumber,
                GameType = eventStarted.GameType,
                Stage = TournamentStage.GroupStage,
                CurrentRound = 1,
                TotalRounds = 1,
                MatchesCompleted = 0,
                TotalMatches = 0,
                ProgressPercentage = 0
            };
            
            _currentState.Status = TournamentStatus.InProgress;
            _currentState.Message = $"Event started: {eventStarted.GameType}";
            _currentState.LastUpdated = DateTime.UtcNow;
            
            _logger.LogInformation("Event started: {Id} - {Name} - {GameType}", 
                eventStarted.EventId, eventStarted.EventName, eventStarted.GameType);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle MatchCompleted event (async version)
    /// </summary>
    public virtual async Task AddMatchAsync(MatchCompletedDto match)
    {
        await Task.Run(() => AddRecentMatch(match));
        _logger.LogInformation("Match added: {Bot1} vs {Bot2}, Winner: {Winner}, Total matches: {Count}", 
            match.Bot1Name, match.Bot2Name, match.WinnerName ?? "Draw", _recentMatches.Count);
    }

    /// <summary>
    /// Handle StandingsUpdated event
    /// </summary>
    public virtual async Task UpdateStandingsAsync(StandingsUpdatedDto standingsEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            var eventId = string.IsNullOrWhiteSpace(standingsEvent.TournamentId)
                ? "unknown-event"
                : standingsEvent.TournamentId;

            if (standingsEvent.OverallStandings != null && standingsEvent.OverallStandings.Count > 0)
            {
                _standingsByEventId[eventId] = standingsEvent.OverallStandings
                    .Select(standing => new TeamStandingDto
                    {
                        TeamName = standing.TeamName,
                        TotalPoints = standing.TotalPoints,
                        TournamentWins = standing.TournamentWins,
                        TotalWins = standing.TotalWins,
                        TotalLosses = standing.TotalLosses,
                        RankChange = standing.RankChange
                    })
                    .ToList();
            }

            var cumulativeLeaderboard = BuildCumulativeLeaderboard();
            if (cumulativeLeaderboard.Count > 0)
            {
                _currentState.OverallLeaderboard = cumulativeLeaderboard;
            }
            _currentState.LastUpdated = DateTime.UtcNow;
            _logger.LogDebug(
                "Standings updated for event {EventId}: {BotCount} bots. Aggregated leaderboard entries: {AggregatedCount}",
                eventId,
                standingsEvent.OverallStandings.Count,
                _currentState.OverallLeaderboard.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle EventCompleted event (individual event/game type)
    /// </summary>
    public virtual async Task UpdateEventCompletedAsync(EventCompletedEventDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState != null)
            {
                // Clear current event when completed
                _currentState.CurrentEvent = null;
                _currentState.Message = $"Event completed! Champion: {completedEvent.Champion}";
                _currentState.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation("Event completed: {Champion} wins!", completedEvent.Champion);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle TournamentStarted event (whole tournament with multiple events)
    /// </summary>
    public virtual async Task UpdateTournamentStartedAsync(TournamentStartedEventDto tournamentEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            var currentStepIndex = tournamentEvent.Steps.FirstOrDefault(s => s.Status == EventStepStatus.InProgress)?.StepIndex ?? 1;

            _currentState.TournamentState = new TournamentStateDto
            {
                TournamentId = tournamentEvent.TournamentId,
                TournamentName = tournamentEvent.TournamentName,
                TotalSteps = tournamentEvent.TotalSteps,
                CurrentStepIndex = currentStepIndex,
                Status = TournamentStatus.InProgress,
                Steps = tournamentEvent.Steps,
                LastUpdated = DateTime.UtcNow
            };

            _standingsByEventId.Clear();

            _logger.LogInformation("Tournament started: {TournamentName} with {TotalSteps} steps", tournamentEvent.TournamentName, tournamentEvent.TotalSteps);
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle TournamentProgressUpdated event
    /// </summary>
    public virtual async Task UpdateTournamentProgressAsync(TournamentProgressUpdatedEventDto progressEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            if (progressEvent?.TournamentState != null)
            {
                progressEvent.TournamentState.LastUpdated = DateTime.UtcNow;
                _currentState.TournamentState = progressEvent.TournamentState;
                _logger.LogInformation("Tournament state updated: {TournamentName} - Step {CurrentStep}/{TotalSteps}",
                    progressEvent.TournamentState.TournamentName,
                    progressEvent.TournamentState.CurrentStepIndex,
                    progressEvent.TournamentState.TotalSteps);
            }
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle EventStepCompleted event
    /// </summary>
    public virtual async Task UpdateEventStepCompletedAsync(EventStepCompletedDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            if (_currentState.TournamentState == null)
            {
                _logger.LogWarning("EventStepCompleted received but TournamentState is null. Creating skeleton state for step {StepIndex}", completedEvent.StepIndex);
                _currentState.TournamentState = new TournamentStateDto
                {
                    TournamentId = completedEvent.TournamentId,
                    Status = TournamentStatus.InProgress,
                    LastUpdated = DateTime.UtcNow,
                    Steps = new List<EventStepDto>()
                };
            }

            var step = _currentState.TournamentState.Steps.SingleOrDefault(s => s.StepIndex == completedEvent.StepIndex);
            if (step == null)
            {
                step = new EventStepDto { StepIndex = completedEvent.StepIndex };
                _currentState.TournamentState.Steps.Add(step);
            }

            step.GameType = completedEvent.GameType;
            step.Status = EventStepStatus.Completed;
            step.WinnerName = completedEvent.WinnerName;
            step.EventId = completedEvent.EventId;
            step.EventName = completedEvent.EventName;
            step.TournamentId = completedEvent.TournamentId;

            _logger.LogInformation("Event step completed: {StepIndex} - {WinnerName}", completedEvent.StepIndex, completedEvent.WinnerName);

            _currentState.TournamentState.LastUpdated = DateTime.UtcNow;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle TournamentCompleted event (whole tournament)
    /// </summary>
    public virtual async Task UpdateTournamentCompletedAsync(TournamentCompletedEventDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            _currentState.TournamentState ??= new TournamentStateDto
            {
                TournamentId = completedEvent.TournamentId,
                TournamentName = completedEvent.TournamentName,
                Steps = new List<EventStepDto>()
            };

            _currentState.TournamentState.TournamentId = completedEvent.TournamentId;
            _currentState.TournamentState.TournamentName = completedEvent.TournamentName;
            _currentState.TournamentState.Status = TournamentStatus.Completed;
            _currentState.TournamentState.LastUpdated = DateTime.UtcNow;
            
            // Update overall message to reflect completion
            _currentState.Message = $"Tournament completed! Champion: {completedEvent.Champion}";
            _currentState.Status = TournamentStatus.Completed;
            _currentState.LastUpdated = DateTime.UtcNow;
            
            _logger.LogInformation("Tournament completed: {TournamentName} - Champion: {Champion}", 
                completedEvent.TournamentName, completedEvent.Champion);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private List<TeamStandingDto> BuildCumulativeLeaderboard()
    {
        var aggregate = new Dictionary<string, TeamStandingDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var standings in _standingsByEventId.Values)
        {
            foreach (var standing in standings)
            {
                if (!aggregate.TryGetValue(standing.TeamName, out var combined))
                {
                    combined = new TeamStandingDto
                    {
                        TeamName = standing.TeamName,
                        TotalPoints = 0,
                        TournamentWins = 0,
                        TotalWins = 0,
                        TotalLosses = 0,
                        RankChange = 0
                    };
                    aggregate[standing.TeamName] = combined;
                }

                combined.TotalPoints += standing.TotalPoints;
                combined.TournamentWins += standing.TournamentWins;
                combined.TotalWins += standing.TotalWins;
                combined.TotalLosses += standing.TotalLosses;
            }
        }

        var leaderboard = aggregate.Values
            .OrderByDescending(item => item.TotalPoints)
            .ThenByDescending(item => item.TotalWins)
            .ThenBy(item => item.TotalLosses)
            .ThenBy(item => item.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < leaderboard.Count; index++)
        {
            leaderboard[index].Rank = index + 1;
            leaderboard[index].RankChange = 0;
        }

        return leaderboard;
    }
}
