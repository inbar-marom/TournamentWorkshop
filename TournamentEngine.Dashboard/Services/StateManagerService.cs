using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Manages current tournament state and provides thread-safe access for dashboard clients
/// </summary>
public class StateManagerService
{
    private readonly ILogger<StateManagerService> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly object _matchesLock = new();
    private DashboardStateDto? _currentState;
    /// <summary>
    /// Single source of truth for all matches.
    /// Structure: event -> group -> list of matches
    /// Keys are normalized (lowercase, trimmed)
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, List<RecentMatchDto>>> _matchesByEventAndGroup = 
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TeamStandingDto>> _standingsByEventId = new(StringComparer.OrdinalIgnoreCase);

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
            
            // Create a fresh copy
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
                GroupStandingsByEvent = currentState.GroupStandingsByEvent,
                RecentMatches = new List<RecentMatchDto>(),
                NextMatch = currentState.NextMatch,
                ScheduledStartTime = currentState.ScheduledStartTime,
                LastUpdated = DateTime.UtcNow
            };
            
            _logger.LogDebug("GetCurrentStateAsync: Status={Status}", stateCopy.Status);
            
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
            if (newState.RecentMatches != null && newState.RecentMatches.Count > 0)
            {
                lock (_matchesLock)
                {
                    ResetMatchStorage();
                    foreach (var match in newState.RecentMatches)
                    {
                        AddMatchToStorage(match);
                    }
                    // Clear the RecentMatches from external state since we manage it internally
                    newState.RecentMatches = new List<RecentMatchDto>();
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

            // Preserve GroupStandings if new state doesn't include them
            if (_currentState?.GroupStandings?.Count > 0)
            {
                if (newState.GroupStandings == null || newState.GroupStandings.Count == 0)
                {
                    newState.GroupStandings = _currentState.GroupStandings;
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
    /// Adds a completed match to the unified match storage
    /// </summary>
    public virtual void AddRecentMatch(MatchCompletedDto match)
    {
        // Convert MatchCompletedDto to RecentMatchDto for UI display
        var recentMatch = new RecentMatchDto
        {
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TournamentName = match.TournamentName,
            EventId = match.EventId,
            EventName = match.EventName,
            Bot1Name = match.Bot1Name,
            Bot2Name = match.Bot2Name,
            Outcome = match.Outcome,
            WinnerName = match.WinnerName,
            Bot1Score = match.Bot1Score,
            Bot2Score = match.Bot2Score,
            CompletedAt = match.CompletedAt,
            GameType = match.GameType,
            GroupId = match.GroupId,
            GroupLabel = match.GroupLabel
        };

        lock (_matchesLock)
        {
            AddMatchToStorage(recentMatch);
        }
    }

    /// <summary>
    /// Gets all tracked matches across all events and groups.
    /// </summary>
    public virtual List<RecentMatchDto> GetAllMatches()
    {
        lock (_matchesLock)
        {
            var allMatches = new List<RecentMatchDto>();
            foreach (var eventDict in _matchesByEventAndGroup.Values)
            {
                foreach (var matches in eventDict.Values)
                {
                    allMatches.AddRange(matches);
                }
            }
            return allMatches
                .OrderByDescending(m => m.CompletedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Gets matches filtered by event name (across all groups in that event).
    /// </summary>
    public virtual List<RecentMatchDto> GetMatchesByEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return new List<RecentMatchDto>();
        }

        lock (_matchesLock)
        {
            var normalizedEvent = NormalizeEventKey(eventName);
            if (!_matchesByEventAndGroup.TryGetValue(normalizedEvent, out var eventDict))
            {
                return new List<RecentMatchDto>();
            }

            var allEventMatches = new List<RecentMatchDto>();
            foreach (var matches in eventDict.Values)
            {
                allEventMatches.AddRange(matches);
            }

            return allEventMatches
                .OrderByDescending(m => m.CompletedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Gets matches filtered by event and group.
    /// </summary>
    public virtual List<RecentMatchDto> GetMatchesByEventAndGroup(string eventName, string groupLabel)
    {
        if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(groupLabel))
        {
            return new List<RecentMatchDto>();
        }

        lock (_matchesLock)
        {
            var normalizedEvent = NormalizeEventKey(eventName);
            var normalizedGroup = NormalizeGroupKey(groupLabel);

            if (!_matchesByEventAndGroup.TryGetValue(normalizedEvent, out var eventDict))
            {
                return new List<RecentMatchDto>();
            }

            if (!eventDict.TryGetValue(normalizedGroup, out var matches))
            {
                return new List<RecentMatchDto>();
            }

            return matches
                .OrderByDescending(m => m.CompletedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Gets groups for a specific event, including standings when available.
    /// </summary>
    public virtual List<GroupDto> GetGroupsByEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return new List<GroupDto>();
        }

        lock (_matchesLock)
        {
            var groupsFromState = _currentState?.GroupStandings?
                .Where(g => (g.EventName ?? string.Empty).Equals(eventName, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<GroupDto>();

            if (groupsFromState.Count > 0)
            {
                return groupsFromState;
            }

            var eventMatches = GetMatchesByEvent(eventName);
            return BuildGroupsFromMatches(eventName, eventMatches);
        }
    }

    /// <summary>
    /// Gets the latest known UTC activity time from state or match activity.
    /// </summary>
    public virtual DateTime GetLatestActivityUtc()
    {
        lock (_matchesLock)
        {
            var lastMatchTime = DateTime.MinValue;
            foreach (var eventDict in _matchesByEventAndGroup.Values)
            {
                foreach (var matches in eventDict.Values)
                {
                    if (matches.Count > 0)
                    {
                        var maxTime = matches.Max(m => m.CompletedAt);
                        if (maxTime > lastMatchTime)
                        {
                            lastMatchTime = maxTime;
                        }
                    }
                }
            }
            
            var stateTime = _currentState?.LastUpdated ?? DateTime.MinValue;
            return lastMatchTime > stateTime ? lastMatchTime : stateTime;
        }
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
            lock (_matchesLock)
            {
                ResetMatchStorage();
            }
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
        var totalMatches = GetAllMatches().Count;
        _logger.LogInformation("Match added: {Bot1} vs {Bot2}, Winner: {Winner}, Total matches: {Count}", 
            match.Bot1Name, match.Bot2Name, match.WinnerName ?? "Draw", totalMatches);
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

            // Store group standings by event index if we have groups and current event info
            if (standingsEvent.GroupStandings != null && standingsEvent.GroupStandings.Count > 0 && _currentState.CurrentEvent != null)
            {
                // Use TournamentNumber as the event index (0-based or 1-based depending on the system)
                int eventIndex = _currentState.CurrentEvent.TournamentNumber - 1; // Assuming 1-based, convert to 0-based
                if (eventIndex < 0) eventIndex = 0; // Fallback
                
                if (!_currentState.GroupStandingsByEvent.ContainsKey(eventIndex))
                {
                    _currentState.GroupStandingsByEvent[eventIndex] = new List<GroupDto>();
                }
                
                _currentState.GroupStandingsByEvent[eventIndex] = standingsEvent.GroupStandings;
            }

            // Also update current group standings for display
            if (standingsEvent.GroupStandings != null && standingsEvent.GroupStandings.Count > 0)
            {
                _currentState.GroupStandings = standingsEvent.GroupStandings;
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

            // Also set the tournament info at the top level
            _currentState.TournamentId = tournamentEvent.TournamentId;
            _currentState.TournamentName = tournamentEvent.TournamentName;
            _currentState.Status = TournamentStatus.InProgress;

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
                
                // Preserve existing Steps if new state doesn't have them
                if ((progressEvent.TournamentState.Steps == null || progressEvent.TournamentState.Steps.Count == 0)
                    && _currentState.TournamentState?.Steps != null && _currentState.TournamentState.Steps.Count > 0)
                {
                    progressEvent.TournamentState.Steps = _currentState.TournamentState.Steps;
                }
                
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

    private static string NormalizeEventKey(string eventName) => eventName.Trim().ToLowerInvariant();

    private static string NormalizeGroupKey(string groupLabel) => groupLabel.Trim().ToLowerInvariant();

    /// <summary>
    /// Adds a match to the unified match storage structure.
    /// Structure: event -> group -> list of matches
    /// </summary>
    private void AddMatchToStorage(RecentMatchDto match)
    {
        var eventName = string.IsNullOrWhiteSpace(match.EventName)
            ? match.GameType.ToString()
            : match.EventName;
        var groupLabel = match.GroupLabel ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(groupLabel))
        {
            _logger.LogWarning("Cannot add match without event and group: Event={Event}, Group={Group}", 
                eventName, groupLabel);
            return;
        }

        var normalizedEvent = NormalizeEventKey(eventName);
        var normalizedGroup = NormalizeGroupKey(groupLabel);

        // Ensure event dictionary exists
        if (!_matchesByEventAndGroup.TryGetValue(normalizedEvent, out var eventDict))
        {
            eventDict = new Dictionary<string, List<RecentMatchDto>>(StringComparer.OrdinalIgnoreCase);
            _matchesByEventAndGroup[normalizedEvent] = eventDict;
        }

        // Ensure group list exists
        if (!eventDict.TryGetValue(normalizedGroup, out var matches))
        {
            matches = new List<RecentMatchDto>();
            eventDict[normalizedGroup] = matches;
        }

        // Add match to the list
        matches.Add(match);
        _logger.LogDebug("Match added to storage: Event={Event}, Group={Group}, MatchId={MatchId}", 
            normalizedEvent, normalizedGroup, match.MatchId);
    }

    private void ResetMatchStorage()
    {
        _matchesByEventAndGroup.Clear();
    }

    private static List<GroupDto> BuildGroupsFromMatches(string eventName, List<RecentMatchDto> matches)
    {
        if (matches.Count == 0)
        {
            return new List<GroupDto>();
        }

        var groupedMatches = matches
            .GroupBy(m => string.IsNullOrWhiteSpace(m.GroupLabel) ? "Ungrouped" : m.GroupLabel)
            .ToList();

        var groups = new List<GroupDto>();

        foreach (var group in groupedMatches)
        {
            var teams = new Dictionary<string, (int wins, int losses, int draws, int points)>(StringComparer.OrdinalIgnoreCase);

            foreach (var match in group)
            {
                if (!teams.ContainsKey(match.Bot1Name))
                {
                    teams[match.Bot1Name] = (0, 0, 0, 0);
                }

                if (!teams.ContainsKey(match.Bot2Name))
                {
                    teams[match.Bot2Name] = (0, 0, 0, 0);
                }

                var bot1 = teams[match.Bot1Name];
                var bot2 = teams[match.Bot2Name];

                if (match.Bot1Score > match.Bot2Score)
                {
                    teams[match.Bot1Name] = (bot1.wins + 1, bot1.losses, bot1.draws, bot1.points + 3);
                    teams[match.Bot2Name] = (bot2.wins, bot2.losses + 1, bot2.draws, bot2.points);
                }
                else if (match.Bot2Score > match.Bot1Score)
                {
                    teams[match.Bot1Name] = (bot1.wins, bot1.losses + 1, bot1.draws, bot1.points);
                    teams[match.Bot2Name] = (bot2.wins + 1, bot2.losses, bot2.draws, bot2.points + 3);
                }
                else
                {
                    teams[match.Bot1Name] = (bot1.wins, bot1.losses, bot1.draws + 1, bot1.points + 1);
                    teams[match.Bot2Name] = (bot2.wins, bot2.losses, bot2.draws + 1, bot2.points + 1);
                }
            }

            var rankings = teams
                .Select(kvp => new BotRankingDto
                {
                    TeamName = kvp.Key,
                    Wins = kvp.Value.wins,
                    Losses = kvp.Value.losses,
                    Draws = kvp.Value.draws,
                    Points = kvp.Value.points
                })
                .OrderByDescending(item => item.Points)
                .ThenByDescending(item => item.Wins)
                .ThenBy(item => item.TeamName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = 0; index < rankings.Count; index++)
            {
                rankings[index].Rank = index + 1;
            }

            groups.Add(new GroupDto
            {
                GroupId = group.Key,
                GroupName = group.Key,
                EventId = eventName,
                EventName = eventName,
                Rankings = rankings
            });
        }

        return groups
            .OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
