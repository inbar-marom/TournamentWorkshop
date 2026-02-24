using TournamentEngine.Core.Common;
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
    private readonly Dictionary<string, HashSet<string>> _eventKeysByAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TeamStandingDto>> _standingsByEventId = new(StringComparer.OrdinalIgnoreCase);

    // Read-through cache: avoids lock contention from many concurrent dashboard viewers
    private volatile DashboardStateDto? _readCache;
    private long _readCacheTimeTicks = DateTime.MinValue.Ticks;
    private static readonly long ReadCacheTtlTicks = TimeSpan.FromSeconds(2).Ticks;

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
    /// Gets the current tournament state snapshot.
    /// Uses a 2-second read-through cache to avoid lock contention with writers
    /// during heavy tournament activity (many concurrent dashboard viewers).
    /// </summary>
    public virtual async Task<DashboardStateDto> GetCurrentStateAsync()
    {
        // Fast path: return cached deep copy if fresh enough
        var cached = _readCache;
        if (cached != null && (DateTime.UtcNow.Ticks - Interlocked.Read(ref _readCacheTimeTicks)) < ReadCacheTtlTicks)
        {
            return cached;
        }

        await _stateLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            cached = _readCache;
            if (cached != null && (DateTime.UtcNow.Ticks - Interlocked.Read(ref _readCacheTimeTicks)) < ReadCacheTtlTicks)
            {
                return cached;
            }

            var currentState = _currentState ?? new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "No tournament data available",
                LastUpdated = DateTime.UtcNow
            };
            
            // Deep copy all mutable state to prevent concurrent modification exceptions
            var stateCopy = DeepCopyState(currentState);
            
            _readCache = stateCopy;
            Interlocked.Exchange(ref _readCacheTimeTicks, DateTime.UtcNow.Ticks);
            
            _logger.LogDebug("GetCurrentStateAsync: rebuilt cache, Status={Status}", stateCopy.Status);
            
            return stateCopy;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Creates a deep copy of DashboardStateDto so readers don't share mutable references with writers
    /// </summary>
    private static DashboardStateDto DeepCopyState(DashboardStateDto source)
    {
        return new DashboardStateDto
        {
            TournamentId = source.TournamentId,
            TournamentName = source.TournamentName,
            Champion = source.Champion,
            Status = source.Status,
            Message = source.Message,
            TournamentProgress = source.TournamentProgress == null ? null : new TournamentProgressDto
            {
                TournamentId = source.TournamentProgress.TournamentId,
                Events = source.TournamentProgress.Events?.Select(e => new EventInTournamentDto
                {
                    EventNumber = e.EventNumber,
                    GameType = e.GameType,
                    Status = e.Status,
                    Champion = e.Champion,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime
                }).ToList() ?? new List<EventInTournamentDto>(),
                CompletedCount = source.TournamentProgress.CompletedCount,
                TotalCount = source.TournamentProgress.TotalCount,
                CurrentEventIndex = source.TournamentProgress.CurrentEventIndex
            },
            TournamentState = source.TournamentState == null ? null : new TournamentStateDto
            {
                TournamentId = source.TournamentState.TournamentId,
                TournamentName = source.TournamentState.TournamentName,
                TotalSteps = source.TournamentState.TotalSteps,
                CurrentStepIndex = source.TournamentState.CurrentStepIndex,
                Status = source.TournamentState.Status,
                Steps = source.TournamentState.Steps?.Select(s => new EventStepDto
                {
                    StepIndex = s.StepIndex,
                    GameType = s.GameType,
                    Status = s.Status,
                    WinnerName = s.WinnerName,
                    EventId = s.EventId,
                    EventName = s.EventName,
                    TournamentId = s.TournamentId
                }).ToList() ?? new List<EventStepDto>(),
                LastUpdated = source.TournamentState.LastUpdated
            },
            CurrentEvent = source.CurrentEvent == null ? null : new CurrentEventDto
            {
                TournamentNumber = source.CurrentEvent.TournamentNumber,
                GameType = source.CurrentEvent.GameType,
                Stage = source.CurrentEvent.Stage,
                CurrentRound = source.CurrentEvent.CurrentRound,
                TotalRounds = source.CurrentEvent.TotalRounds,
                MatchesCompleted = source.CurrentEvent.MatchesCompleted,
                TotalMatches = source.CurrentEvent.TotalMatches,
                ProgressPercentage = source.CurrentEvent.ProgressPercentage
            },
            OverallLeaderboard = source.OverallLeaderboard?.Select(l => new TeamStandingDto
            {
                Rank = l.Rank,
                TeamName = l.TeamName,
                TotalPoints = l.TotalPoints,
                TournamentWins = l.TournamentWins,
                TotalWins = l.TotalWins,
                TotalLosses = l.TotalLosses,
                RankChange = l.RankChange
            }).ToList() ?? new List<TeamStandingDto>(),
            GroupStandings = source.GroupStandings?.Select(g => new GroupDto
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName,
                EventName = g.EventName,
                EventId = g.EventId,
                Rankings = g.Rankings?.Select(r => new BotRankingDto
                {
                    Rank = r.Rank,
                    TeamName = r.TeamName,
                    Wins = r.Wins,
                    Losses = r.Losses,
                    Draws = r.Draws,
                    Points = r.Points
                }).ToList() ?? new List<BotRankingDto>()
            }).ToList() ?? new List<GroupDto>(),
            RecentMatches = new List<RecentMatchDto>(),
            NextMatch = source.NextMatch,
            ScheduledStartTime = source.ScheduledStartTime,
            GroupStandingsByEvent = source.GroupStandingsByEvent?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Select(g => new GroupDto
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    EventName = g.EventName,
                    EventId = g.EventId,
                    Rankings = g.Rankings?.Select(r => new BotRankingDto
                    {
                        Rank = r.Rank,
                        TeamName = r.TeamName,
                        Wins = r.Wins,
                        Losses = r.Losses,
                        Draws = r.Draws,
                        Points = r.Points
                    }).ToList() ?? new List<BotRankingDto>()
                }).ToList() ?? new List<GroupDto>()
            ) ?? new Dictionary<int, List<GroupDto>>(),
            LastUpdated = source.LastUpdated
        };
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

            // Preserve GroupStandingsByEvent (historical group data per event index)
            if (_currentState?.GroupStandingsByEvent?.Count > 0)
            {
                if (newState.GroupStandingsByEvent == null || newState.GroupStandingsByEvent.Count == 0)
                {
                    newState.GroupStandingsByEvent = _currentState.GroupStandingsByEvent;
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
            // Invalidate read cache so next reader picks up fresh data
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
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
            var candidateKeys = ResolveEventStorageKeys(normalizedEvent);
            if (candidateKeys.Count == 0)
            {
                return new List<RecentMatchDto>();
            }

            var allEventMatches = new List<RecentMatchDto>();
            foreach (var eventKey in candidateKeys)
            {
                if (!_matchesByEventAndGroup.TryGetValue(eventKey, out var eventDict))
                {
                    continue;
                }

                foreach (var matches in eventDict.Values)
                {
                    allEventMatches.AddRange(matches);
                }
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

            var candidateEventKeys = ResolveEventStorageKeys(normalizedEvent);
            if (candidateEventKeys.Count == 0)
            {
                return new List<RecentMatchDto>();
            }

            var eventGroupMatches = new List<RecentMatchDto>();

            foreach (var eventKey in candidateEventKeys)
            {
                if (!_matchesByEventAndGroup.TryGetValue(eventKey, out var eventDict))
                {
                    continue;
                }

                if (eventDict.TryGetValue(normalizedGroup, out var matches))
                {
                    eventGroupMatches.AddRange(matches);
                }
            }

            return eventGroupMatches
                .OrderByDescending(m => m.CompletedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Gets groups for a specific event, including standings when available.
    /// Uses cached state to avoid lock contention during heavy tournament activity.
    /// </summary>
    public virtual async Task<List<GroupDto>> GetGroupsByEventAsync(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return new List<GroupDto>();
        }

        // Use the read-cached state copy (avoids holding _stateLock)
        var cachedState = await GetCurrentStateAsync();

        // Check groups from state's GroupStandings
        var groupsFromState = cachedState.GroupStandings?
            .Where(g => (g.EventName ?? string.Empty).Equals(eventName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (groupsFromState != null && groupsFromState.Count > 0)
        {
            return groupsFromState;
        }

        // Check GroupStandingsByEvent for historical events
        if (cachedState.GroupStandingsByEvent != null)
        {
            foreach (var kvp in cachedState.GroupStandingsByEvent)
            {
                var eventGroups = kvp.Value?
                    .Where(g => (g.EventName ?? string.Empty).Equals(eventName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (eventGroups != null && eventGroups.Count > 0)
                {
                    return eventGroups;
                }
            }
        }

        // Fallback: build groups from match data
        var eventMatches = GetMatchesByEvent(eventName);
        return BuildGroupsFromMatches(eventName, eventMatches);
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
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
            _logger.LogDebug("Dashboard state cleared");
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

            var resolvedEventNumber = eventStarted.EventNumber;
            if (resolvedEventNumber <= 0)
            {
                resolvedEventNumber = _currentState.TournamentState?.CurrentStepIndex
                    ?? _currentState.TournamentProgress?.CurrentEventIndex
                    ?? 1;
            }
            
            _currentState.CurrentEvent = new CurrentEventDto
            {
                TournamentNumber = resolvedEventNumber,
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
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
            
            _logger.LogDebug("Event started: {Id} - {Name} - {GameType}", 
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
        _logger.LogDebug("Match added: {Bot1} vs {Bot2}, Winner: {Winner}, Total matches: {Count}", 
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
                var eventIdentity = ResolveCurrentEventIdentity(eventIndex);
                var eventTaggedGroups = TagGroupsWithEventIdentity(standingsEvent.GroupStandings, eventIdentity.eventId, eventIdentity.eventName);
                
                if (!_currentState.GroupStandingsByEvent.ContainsKey(eventIndex))
                {
                    _currentState.GroupStandingsByEvent[eventIndex] = new List<GroupDto>();
                }
                
                // MERGE into existing groups (do not replace) so Group A-J are preserved
                // when "Final Group-finalStandings" standings arrive at the end of an event.
                var existingByEvent = _currentState.GroupStandingsByEvent[eventIndex];
                foreach (var newGroup in eventTaggedGroups)
                {
                    var idx = existingByEvent.FindIndex(g =>
                        g.GroupName.Equals(newGroup.GroupName, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                        existingByEvent[idx] = newGroup;
                    else
                        existingByEvent.Add(newGroup);
                }
            }

            // Also merge into current group standings for display (preserves all groups across all events)
            if (standingsEvent.GroupStandings != null && standingsEvent.GroupStandings.Count > 0)
            {
                var currentGroups = _currentState.GroupStandings ?? new List<GroupDto>();
                var eventIndex = Math.Max(0, (_currentState.CurrentEvent?.TournamentNumber ?? 1) - 1);
                var eventIdentity = ResolveCurrentEventIdentity(eventIndex);
                var eventTaggedGroups = TagGroupsWithEventIdentity(standingsEvent.GroupStandings, eventIdentity.eventId, eventIdentity.eventName);

                foreach (var newGroup in eventTaggedGroups)
                {
                    var idx = currentGroups.FindIndex(g =>
                        g.GroupName.Equals(newGroup.GroupName, StringComparison.OrdinalIgnoreCase) &&
                        (g.EventName ?? string.Empty).Equals(newGroup.EventName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                        currentGroups[idx] = newGroup;
                    else
                        currentGroups.Add(newGroup);
                }
                _currentState.GroupStandings = currentGroups;
            }

            var cumulativeLeaderboard = BuildCumulativeLeaderboard();
            if (cumulativeLeaderboard.Count > 0)
            {
                _currentState.OverallLeaderboard = cumulativeLeaderboard;
            }
            _currentState.LastUpdated = DateTime.UtcNow;
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
            _logger.LogDebug(
                "Standings updated for event {EventId}: {BotCount} bots. Aggregated leaderboard entries: {AggregatedCount}",
                eventId,
                standingsEvent.OverallStandings?.Count ?? 0,
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
                Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
                _logger.LogDebug("Event completed: {Champion} wins!", completedEvent.Champion);
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
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
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
                _logger.LogDebug("Tournament state updated: {TournamentName} - Step {CurrentStep}/{TotalSteps}",
                    progressEvent.TournamentState.TournamentName,
                    progressEvent.TournamentState.CurrentStepIndex,
                    progressEvent.TournamentState.TotalSteps);
            }
            _currentState.LastUpdated = DateTime.UtcNow;
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
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

            _logger.LogDebug("Event step completed: {StepIndex} - {WinnerName}", completedEvent.StepIndex, completedEvent.WinnerName);

            _currentState.TournamentState.LastUpdated = DateTime.UtcNow;
            _currentState.LastUpdated = DateTime.UtcNow;
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
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
            Interlocked.Exchange(ref _readCacheTimeTicks, 0L);
            
            _logger.LogDebug("Tournament completed: {TournamentName} - Champion: {Champion}", 
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
        var eventId = match.EventId;
        var groupLabel = match.GroupLabel ?? string.Empty;

        if ((string.IsNullOrWhiteSpace(eventName) && string.IsNullOrWhiteSpace(eventId)) || string.IsNullOrWhiteSpace(groupLabel))
        {
            _logger.LogWarning("Cannot add match without event and group: Event={Event}, Group={Group}", 
                eventName ?? eventId, groupLabel);
            return;
        }

        var normalizedEvent = !string.IsNullOrWhiteSpace(eventId)
            ? NormalizeEventKey(eventId)
            : NormalizeEventKey(eventName);
        var normalizedGroup = NormalizeGroupKey(groupLabel);

        RegisterEventAlias(normalizedEvent, normalizedEvent);
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            RegisterEventAlias(normalizedEvent, NormalizeEventKey(eventName));
        }

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

        var existingIndex = matches.FindIndex(existing => IsLikelySameMatch(existing, match));
        if (existingIndex >= 0)
        {
            matches[existingIndex] = MergeMatch(matches[existingIndex], match);
            _logger.LogDebug("Match merged in storage: Event={Event}, Group={Group}, MatchId={MatchId}",
                normalizedEvent, normalizedGroup, match.MatchId);
            return;
        }

        // Add match to the list
        matches.Add(match);
        _logger.LogDebug("Match added to storage: Event={Event}, Group={Group}, MatchId={MatchId}", 
            normalizedEvent, normalizedGroup, match.MatchId);
    }

    private static bool IsLikelySameMatch(RecentMatchDto left, RecentMatchDto right)
    {
        if (!string.IsNullOrWhiteSpace(left.MatchId) &&
            !string.IsNullOrWhiteSpace(right.MatchId) &&
            left.MatchId.Equals(right.MatchId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!left.Bot1Name.Equals(right.Bot1Name, StringComparison.OrdinalIgnoreCase) ||
            !left.Bot2Name.Equals(right.Bot2Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (left.Bot1Score != right.Bot1Score || left.Bot2Score != right.Bot2Score)
        {
            return false;
        }

        var timeDiff = (left.CompletedAt - right.CompletedAt).Duration();
        return timeDiff <= TimeSpan.FromSeconds(2);
    }

    private static RecentMatchDto MergeMatch(RecentMatchDto existing, RecentMatchDto incoming)
    {
        var incomingResolved = incoming.Outcome != MatchOutcome.Unknown;
        var existingResolved = existing.Outcome != MatchOutcome.Unknown;

        var primary = incomingResolved || !existingResolved ? incoming : existing;
        var secondary = ReferenceEquals(primary, incoming) ? existing : incoming;

        return new RecentMatchDto
        {
            MatchId = !string.IsNullOrWhiteSpace(primary.MatchId) ? primary.MatchId : secondary.MatchId,
            TournamentId = !string.IsNullOrWhiteSpace(primary.TournamentId) ? primary.TournamentId : secondary.TournamentId,
            TournamentName = !string.IsNullOrWhiteSpace(primary.TournamentName) ? primary.TournamentName : secondary.TournamentName,
            EventId = !string.IsNullOrWhiteSpace(primary.EventId) ? primary.EventId : secondary.EventId,
            EventName = !string.IsNullOrWhiteSpace(primary.EventName) ? primary.EventName : secondary.EventName,
            Bot1Name = !string.IsNullOrWhiteSpace(primary.Bot1Name) ? primary.Bot1Name : secondary.Bot1Name,
            Bot2Name = !string.IsNullOrWhiteSpace(primary.Bot2Name) ? primary.Bot2Name : secondary.Bot2Name,
            Outcome = primary.Outcome != MatchOutcome.Unknown ? primary.Outcome : secondary.Outcome,
            WinnerName = !string.IsNullOrWhiteSpace(primary.WinnerName) ? primary.WinnerName : secondary.WinnerName,
            Bot1Score = primary.Bot1Score,
            Bot2Score = primary.Bot2Score,
            CompletedAt = primary.CompletedAt >= secondary.CompletedAt ? primary.CompletedAt : secondary.CompletedAt,
            GameType = primary.GameType,
            GroupId = !string.IsNullOrWhiteSpace(primary.GroupId) ? primary.GroupId : secondary.GroupId,
            GroupLabel = !string.IsNullOrWhiteSpace(primary.GroupLabel) ? primary.GroupLabel : secondary.GroupLabel
        };
    }

    private void ResetMatchStorage()
    {
        _matchesByEventAndGroup.Clear();
        _eventKeysByAlias.Clear();
    }

    private HashSet<string> ResolveEventStorageKeys(string normalizedEventOrAlias)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_matchesByEventAndGroup.ContainsKey(normalizedEventOrAlias))
        {
            keys.Add(normalizedEventOrAlias);
        }

        if (_eventKeysByAlias.TryGetValue(normalizedEventOrAlias, out var aliasedKeys))
        {
            foreach (var key in aliasedKeys)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private void RegisterEventAlias(string eventStorageKey, string alias)
    {
        if (string.IsNullOrWhiteSpace(eventStorageKey) || string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        if (!_eventKeysByAlias.TryGetValue(alias, out var eventKeys))
        {
            eventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _eventKeysByAlias[alias] = eventKeys;
        }

        eventKeys.Add(eventStorageKey);
    }

    private (string eventId, string eventName) ResolveCurrentEventIdentity(int eventIndex)
    {
        var step = _currentState?.TournamentState?.Steps?
            .FirstOrDefault(s => s.StepIndex == eventIndex + 1);

        var resolvedEventId = !string.IsNullOrWhiteSpace(step?.EventId)
            ? step!.EventId!
            : _currentState?.TournamentId ?? string.Empty;

        var resolvedEventName = !string.IsNullOrWhiteSpace(step?.EventName)
            ? step!.EventName!
            : step?.GameType.ToString() ?? _currentState?.CurrentEvent?.GameType.ToString() ?? string.Empty;

        return (resolvedEventId, resolvedEventName);
    }

    private static List<GroupDto> TagGroupsWithEventIdentity(List<GroupDto> groups, string eventId, string eventName)
    {
        return groups.Select(group => new GroupDto
        {
            GroupId = group.GroupId,
            GroupName = group.GroupName,
            EventId = string.IsNullOrWhiteSpace(group.EventId) ? eventId : group.EventId,
            EventName = string.IsNullOrWhiteSpace(group.EventName) ? eventName : group.EventName,
            Rankings = group.Rankings?.Select(r => new BotRankingDto
            {
                Rank = r.Rank,
                TeamName = r.TeamName,
                Wins = r.Wins,
                Losses = r.Losses,
                Draws = r.Draws,
                Points = r.Points
            }).ToList() ?? new List<BotRankingDto>()
        }).ToList();
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
