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
    private TournamentStateDto? _currentState;
    private readonly ConcurrentQueue<RecentMatchDto> _recentMatches = new();
    private const int MaxRecentMatches = 50;

    public StateManagerService(ILogger<StateManagerService> logger)
    {
        _logger = logger;
        _currentState = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted,
            Message = "Waiting for tournament to start...",
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the current tournament state snapshot
    /// </summary>
    public virtual async Task<TournamentStateDto> GetCurrentStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            var state = _currentState ?? new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "No tournament data available",
                LastUpdated = DateTime.UtcNow
            };
            
            // Populate recent matches from the queue
            state.RecentMatches = GetRecentMatches(20);
            
            return state;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Updates the current tournament state
    /// </summary>
    public virtual async Task UpdateStateAsync(TournamentStateDto newState)
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

            _currentState = newState;
            _currentState.LastUpdated = DateTime.UtcNow;
            _logger.LogInformation("Tournament state updated: {Status}", newState.Status);
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
            GameType = match.GameType
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
            _currentState = new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "State cleared",
                LastUpdated = DateTime.UtcNow
            };
            _recentMatches.Clear();
            _logger.LogInformation("Tournament state cleared");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle TournamentStarted event
    /// </summary>
    public virtual async Task UpdateTournamentStartedAsync(TournamentStartedDto tournamentEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState = new TournamentStateDto
            {
                TournamentId = tournamentEvent.TournamentId,
                TournamentName = tournamentEvent.TournamentName,
                Status = TournamentStatus.InProgress,
                Message = $"Tournament started: {tournamentEvent.GameType}",
                LastUpdated = DateTime.UtcNow
            };
            _logger.LogInformation("Tournament started: {Id} - {Name} - {GameType}", 
                tournamentEvent.TournamentId, tournamentEvent.TournamentName, tournamentEvent.GameType);
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
        _logger.LogDebug("Match added: {Bot1} vs {Bot2}", match.Bot1Name, match.Bot2Name);
    }

    /// <summary>
    /// Handle StandingsUpdated event
    /// </summary>
    public virtual async Task UpdateStandingsAsync(StandingsUpdatedDto standingsEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState != null)
            {
                _currentState.OverallLeaderboard = standingsEvent.OverallStandings;
                _currentState.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation("Standings updated: {BotCount} bots", standingsEvent.OverallStandings.Count);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle TournamentCompleted event
    /// </summary>
    public virtual async Task UpdateTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState != null)
            {
                _currentState.Status = TournamentStatus.Completed;
                _currentState.Message = $"Tournament completed! Champion: {completedEvent.Champion}";
                _currentState.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation("Tournament completed: {Champion} wins!", completedEvent.Champion);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle SeriesStarted event
    /// </summary>
    public virtual async Task UpdateSeriesStartedAsync(SeriesStartedDto seriesEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            var currentStepIndex = seriesEvent.Steps.FirstOrDefault(s => s.Status == SeriesStepStatus.Running)?.StepIndex ?? 1;

            _currentState.SeriesState = new SeriesStateDto
            {
                SeriesId = seriesEvent.SeriesId,
                SeriesName = seriesEvent.SeriesName,
                TotalSteps = seriesEvent.TotalSteps,
                CurrentStepIndex = currentStepIndex,
                Status = SeriesStatus.InProgress,
                Steps = seriesEvent.Steps,
                LastUpdated = DateTime.UtcNow
            };

            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle SeriesProgressUpdated event
    /// </summary>
    public virtual async Task UpdateSeriesProgressAsync(SeriesProgressUpdatedDto progressEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            progressEvent.SeriesState.LastUpdated = DateTime.UtcNow;
            _currentState.SeriesState = progressEvent.SeriesState;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle SeriesStepCompleted event
    /// </summary>
    public virtual async Task UpdateSeriesStepCompletedAsync(SeriesStepCompletedDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            _currentState.SeriesState ??= new SeriesStateDto
            {
                SeriesId = completedEvent.SeriesId,
                Status = SeriesStatus.InProgress,
                LastUpdated = DateTime.UtcNow
            };

            var step = _currentState.SeriesState.Steps.SingleOrDefault(s => s.StepIndex == completedEvent.StepIndex);
            if (step == null)
            {
                step = new SeriesStepDto { StepIndex = completedEvent.StepIndex };
                _currentState.SeriesState.Steps.Add(step);
            }

            step.GameType = completedEvent.GameType;
            step.Status = SeriesStepStatus.Completed;
            step.WinnerName = completedEvent.WinnerName;
            step.TournamentId = completedEvent.TournamentId;
            step.TournamentName = completedEvent.TournamentName;

            _currentState.SeriesState.LastUpdated = DateTime.UtcNow;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Handle SeriesCompleted event
    /// </summary>
    public virtual async Task UpdateSeriesCompletedAsync(SeriesCompletedDto completedEvent)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentState ??= new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "Waiting for tournament to start...",
                LastUpdated = DateTime.UtcNow
            };

            _currentState.SeriesState ??= new SeriesStateDto
            {
                SeriesId = completedEvent.SeriesId,
                SeriesName = completedEvent.SeriesName
            };

            _currentState.SeriesState.SeriesId = completedEvent.SeriesId;
            _currentState.SeriesState.SeriesName = completedEvent.SeriesName;
            _currentState.SeriesState.Status = SeriesStatus.Completed;
            _currentState.SeriesState.LastUpdated = DateTime.UtcNow;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
