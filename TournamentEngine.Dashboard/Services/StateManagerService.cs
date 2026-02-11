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
    private readonly ConcurrentQueue<MatchCompletedDto> _recentMatches = new();
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
            return _currentState ?? new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "No tournament data available",
                LastUpdated = DateTime.UtcNow
            };
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
        _recentMatches.Enqueue(match);
        
        // Keep only the most recent matches
        while (_recentMatches.Count > MaxRecentMatches)
        {
            _recentMatches.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets recent matches
    /// </summary>
    public virtual List<MatchCompletedDto> GetRecentMatches(int count = 20)
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
            _recentMatches.Clear ();
            _logger.LogInformation("Tournament state cleared");
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
