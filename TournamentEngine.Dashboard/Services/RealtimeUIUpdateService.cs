using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for handling real-time UI updates via SignalR events.
/// Phase 4: Basic UI - Real-time Update Handler
/// </summary>
public class RealtimeUIUpdateService
{
    private readonly StateManagerService _stateManager;
    private DashboardStateDto _currentState;

    public event EventHandler<StateUpdateEventArgs>? StateUpdateReceived;
    public event EventHandler<MatchUpdateEventArgs>? MatchUpdateReceived;
    public event EventHandler<StandingsUpdateEventArgs>? StandingsUpdateReceived;

    public RealtimeUIUpdateService(StateManagerService stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _currentState = new DashboardStateDto();
    }

    /// <summary>
    /// Initialize the current state.
    /// </summary>
    public async Task InitializeAsync()
    {
        _currentState = await _stateManager.GetCurrentStateAsync();
    }

    /// <summary>
    /// Handle event started event.
    /// </summary>
    public async Task OnTournamentStartedAsync(EventStartedEventDto evt)
    {
        _currentState.Status = TournamentStatus.InProgress;
        _currentState.LastUpdated = DateTime.UtcNow;

        if (_currentState.CurrentEvent == null)
        {
            _currentState.CurrentEvent = new CurrentEventDto
            {
                TournamentNumber = evt.EventNumber,
                GameType = evt.GameType,
                Stage = TournamentStage.GroupStage,
                MatchesCompleted = 0,
                TotalMatches = 0,
                CurrentRound = 1,
                TotalRounds = 1,
                ProgressPercentage = 0.0
            };
        }

        await SaveStateAsync();
        OnStateUpdated(new StateUpdateEventArgs
        {
            PreviousStatus = TournamentStatus.NotStarted,
            NewStatus = TournamentStatus.InProgress,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Handle round started event.
    /// </summary>
    public async Task OnRoundStartedAsync(RoundStartedDto evt)
    {
        if (_currentState.CurrentEvent != null)
        {
            _currentState.CurrentEvent.CurrentRound = evt.RoundNumber;
            _currentState.CurrentEvent.ProgressPercentage = 
                GetProgressPercentage(_currentState.CurrentEvent);
        }

        _currentState.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync();
    }

    /// <summary>
    /// Handle match completed event and update match feed.
    /// </summary>
    public async Task OnMatchCompletedAsync(MatchCompletedDto evt)
    {
        // Convert MatchCompletedDto to RecentMatchDto
        var matchDto = new RecentMatchDto
        {
            MatchId = evt.MatchId,
            Bot1Name = evt.Bot1Name,
            Bot2Name = evt.Bot2Name,
            Outcome = evt.Outcome,
            WinnerName = evt.WinnerName,
            Bot1Score = evt.Bot1Score,
            Bot2Score = evt.Bot2Score,
            CompletedAt = evt.CompletedAt,
            GameType = evt.GameType
        };

        // Add to recent matches
        _currentState.RecentMatches.Add(matchDto);

        // Keep only last 50 matches
        if (_currentState.RecentMatches.Count > 50)
        {
            _currentState.RecentMatches = _currentState.RecentMatches
                .OrderByDescending(m => m.CompletedAt)
                .Take(50)
                .ToList();
        }

        // Update event progress
        if (_currentState.CurrentEvent != null)
        {
            _currentState.CurrentEvent.MatchesCompleted++;
            _currentState.CurrentEvent.ProgressPercentage = 
                GetProgressPercentage(_currentState.CurrentEvent);
        }

        _currentState.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync();

        OnMatchUpdated(new MatchUpdateEventArgs
        {
            Match = matchDto,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Handle standings updated event.
    /// </summary>
    public async Task OnStandingsUpdatedAsync(StandingsUpdatedDto evt)
    {
        // Update leaderboard
        if (evt.OverallStandings != null && evt.OverallStandings.Any())
        {
            _currentState.OverallLeaderboard = evt.OverallStandings;
        }

        // Update group standings
        if (evt.GroupStandings != null && evt.GroupStandings.Any())
        {
            _currentState.GroupStandings = evt.GroupStandings;
        }

        _currentState.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync();

        OnStandingsUpdated(new StandingsUpdateEventArgs
        {
            Standings = evt.OverallStandings,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Handle event completed event.
    /// </summary>
    public async Task OnTournamentCompletedAsync(EventCompletedEventDto evt)
    {
        _currentState.Status = TournamentStatus.Completed;
        
        if (_currentState.CurrentEvent != null)
        {
            _currentState.CurrentEvent.ProgressPercentage = 100.0;
        }

        _currentState.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync();

        OnStateUpdated(new StateUpdateEventArgs
        {
            PreviousStatus = TournamentStatus.InProgress,
            NewStatus = TournamentStatus.Completed,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get current UI state.
    /// </summary>
    public DashboardStateDto GetCurrentState()
    {
        return _currentState;
    }

    /// <summary>
    /// Update current state from server.
    /// </summary>
    public async Task RefreshStateAsync()
    {
        _currentState = await _stateManager.GetCurrentStateAsync();
        _currentState.LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if state has changed since last update.
    /// </summary>
    public bool HasStateChanged(DashboardStateDto previousState)
    {
        return previousState.Status != _currentState.Status ||
               previousState.LastUpdated != _currentState.LastUpdated;
    }

    /// <summary>
    /// Get list of changes between two states.
    /// </summary>
    public List<string> GetStateChanges(DashboardStateDto previousState)
    {
        var changes = new List<string>();

        if (previousState.Status != _currentState.Status)
            changes.Add($"Status changed: {previousState.Status} → {_currentState.Status}");

        if (previousState.OverallLeaderboard?.Count != _currentState.OverallLeaderboard?.Count)
            changes.Add("Leaderboard updated");

        if (previousState.RecentMatches?.Count != _currentState.RecentMatches?.Count)
            changes.Add("Match feed updated");

        if (previousState.CurrentEvent?.MatchesCompleted != _currentState.CurrentEvent?.MatchesCompleted)
            changes.Add("Event progress updated");

        return changes;
    }

    private async Task SaveStateAsync()
    {
        await _stateManager.UpdateStateAsync(_currentState);
    }

    private double GetProgressPercentage(CurrentEventDto tournament)
    {
        if (tournament.TotalMatches == 0)
            return 0.0;

        return (tournament.MatchesCompleted * 100.0) / tournament.TotalMatches;
    }

    protected virtual void OnStateUpdated(StateUpdateEventArgs e)
    {
        StateUpdateReceived?.Invoke(this, e);
    }

    protected virtual void OnMatchUpdated(MatchUpdateEventArgs e)
    {
        MatchUpdateReceived?.Invoke(this, e);
    }

    protected virtual void OnStandingsUpdated(StandingsUpdateEventArgs e)
    {
        StandingsUpdateReceived?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for state updates.
/// </summary>
public class StateUpdateEventArgs : EventArgs
{
    public TournamentStatus PreviousStatus { get; set; }
    public TournamentStatus NewStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Event arguments for match updates.
/// </summary>
public class MatchUpdateEventArgs : EventArgs
{
    public RecentMatchDto Match { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Event arguments for standings updates.
/// </summary>
public class StandingsUpdateEventArgs : EventArgs
{
    public List<TeamStandingDto> Standings { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}
