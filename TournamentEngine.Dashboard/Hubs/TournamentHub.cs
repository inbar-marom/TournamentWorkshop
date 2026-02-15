namespace TournamentEngine.Dashboard.Hubs;

using Microsoft.AspNetCore.SignalR;
using TournamentEngine.Api.Models;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// SignalR hub for real-time tournament updates.
/// Broadcasts tournament state changes to connected browser clients.
/// </summary>
public class TournamentHub : Hub
{
    private readonly StateManagerService? _stateManager;
    private readonly TournamentManagementService? _managementService;
    private readonly ILogger<TournamentHub> _logger;

    public TournamentHub(
        StateManagerService stateManager, 
        TournamentManagementService? managementService,
        ILogger<TournamentHub> logger)
    {
        _stateManager = stateManager;
        _managementService = managementService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Add client to the viewers group
        await Groups.AddToGroupAsync(Context.ConnectionId, "TournamentViewers");
        
        // Send current tournament state to new client
        if (_stateManager != null)
        {
            var currentState = await _stateManager.GetCurrentStateAsync();
            await Clients.Caller.SendAsync("CurrentState", currentState);
        }

        // Send current management state to new client
        if (_managementService != null)
        {
            var managementState = await _managementService.GetStateAsync();
            await Clients.Caller.SendAsync("ManagementStateChanged", managementState);
        }
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client subscribes to tournament updates.
    /// </summary>
    public async Task SubscribeToUpdates()
    {
        _logger.LogInformation("Client {ConnectionId} subscribed to tournament updates", Context.ConnectionId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "TournamentViewers");
        
        if (_stateManager != null)
        {
            // Send acknowledgment with current state
            var currentState = await _stateManager.GetCurrentStateAsync();
            
            // If tournament state is missing but tournament exists, reconstruct tournament state from tournament data
            if (currentState.TournamentState == null && !string.IsNullOrEmpty(currentState.TournamentId))
            {
                _logger.LogInformation("Tournament state missing - reconstructing from current tournament state");
                // The tournament state should have enough info to show the current step
                // The TournamentStarted and progress events will repopulate the full state as clients continue
            }
            
            await Clients.Caller.SendAsync("SubscriptionConfirmed", new
            {
                Message = "Successfully subscribed to tournament updates",
                Timestamp = DateTime.UtcNow,
                CurrentState = currentState
            });
        }
    }

    /// <summary>
    /// Client requests current tournament state snapshot.
    /// </summary>
    public async Task GetCurrentState()
    {
        _logger.LogInformation("Client {ConnectionId} requested current state", Context.ConnectionId);
        
        if (_stateManager != null)
        {
            var state = await _stateManager.GetCurrentStateAsync();
            await Clients.Caller.SendAsync("CurrentState", state);
        }
    }

    /// <summary>
    /// Client requests recent matches.
    /// </summary>
    public async Task GetRecentMatches(int count = 20)
    {
        _logger.LogInformation("Client {ConnectionId} requested recent matches", Context.ConnectionId);
        
        if (_stateManager != null)
        {
            var matches = _stateManager.GetRecentMatches(count);
            await Clients.Caller.SendAsync("RecentMatches", matches);
        }
    }

    /// <summary>
    /// Ping endpoint for connection testing.
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    /// <summary>
    /// Receive EventStarted event from ConsoleEventPublisher
    /// </summary>
    public async Task EventStarted(EventStartedEventDto eventStarted)
    {
        _logger.LogInformation("Event started: {EventId} - {BotCount} bots, Game: {GameType}",
            eventStarted.EventId, eventStarted.TotalBots, eventStarted.GameType);

        await _stateManager.UpdateEventStartedAsync(eventStarted);

        await Clients.Group("TournamentViewers").SendAsync("EventStarted", eventStarted);
    }

    /// <summary>
    /// Receive MatchCompleted event from ConsoleEventPublisher
    /// </summary>
    public async Task MatchCompleted(MatchCompletedDto matchEvent)
    {
        _logger.LogInformation("Match completed: {Bot1} vs {Bot2} - Winner: {Winner}", 
            matchEvent.Bot1Name, matchEvent.Bot2Name, matchEvent.WinnerName ?? "Draw");
        
        // Update state manager
        await _stateManager.AddMatchAsync(matchEvent);
        
        // Broadcast to all viewers
        await Clients.Group("TournamentViewers").SendAsync("MatchCompleted", matchEvent);
    }

    /// <summary>
    /// Receive StandingsUpdated event from ConsoleEventPublisher
    /// </summary>
    public async Task StandingsUpdated(StandingsUpdatedDto standingsEvent)
    {
        _logger.LogInformation("Standings updated: {BotCount} bots", standingsEvent.OverallStandings.Count);
        
        // Update state manager
        await _stateManager.UpdateStandingsAsync(standingsEvent);
        
        // Broadcast to all viewers
        await Clients.Group("TournamentViewers").SendAsync("StandingsUpdated", standingsEvent);
    }

    /// <summary>
    /// Receive RoundStarted event from ConsoleEventPublisher
    /// </summary>
    public async Task RoundStarted(RoundStartedDto roundEvent)
    {
        _logger.LogInformation("Round started: {Stage} - Round {RoundNumber}", 
            roundEvent.Stage, roundEvent.RoundNumber);
        
        // Broadcast to all viewers
        await Clients.Group("TournamentViewers").SendAsync("RoundStarted", roundEvent);
    }

    /// <summary>
    /// Receive EventCompleted event from ConsoleEventPublisher
    /// </summary>
    public async Task EventCompleted(EventCompletedEventDto completedEvent)
    {
        _logger.LogInformation("Event completed: Winner: {Champion} - {TotalMatches} matches played", 
            completedEvent.Champion, completedEvent.TotalMatches);
        
        // Update state manager
        await _stateManager.UpdateEventCompletedAsync(completedEvent);
        
        // Broadcast to all viewers
        await Clients.Group("TournamentViewers").SendAsync("EventCompleted", completedEvent);
    }

    /// <summary>
    /// Receive TournamentStarted event (whole tournament) from ConsoleEventPublisher
    /// </summary>
    public async Task TournamentStarted(TournamentStartedEventDto tournamentEvent)
    {
        _logger.LogInformation("Tournament started: {TournamentName} with {TotalSteps} steps",
            tournamentEvent.TournamentName, tournamentEvent.TotalSteps);

        await _stateManager.UpdateTournamentStartedAsync(tournamentEvent);

        await Clients.Group("TournamentViewers").SendAsync("TournamentStarted", tournamentEvent);
    }

    /// <summary>
    /// Backward-compatible alias for TournamentStarted.
    /// </summary>
    public async Task SeriesStarted(TournamentStartedEventDto tournamentEvent)
    {
        await TournamentStarted(tournamentEvent);
    }

    /// <summary>
    /// Receive TournamentProgressUpdated event from ConsoleEventPublisher
    /// </summary>
    public async Task TournamentProgressUpdated(TournamentProgressUpdatedEventDto progressEvent)
    {
        _logger.LogInformation("Tournament progress updated: Step {StepIndex}/{TotalSteps}",
            progressEvent.TournamentState.CurrentStepIndex, progressEvent.TournamentState.TotalSteps);

        await _stateManager.UpdateTournamentProgressAsync(progressEvent);

        await Clients.Group("TournamentViewers").SendAsync("TournamentProgressUpdated", progressEvent);
        await Clients.Group("TournamentViewers").SendAsync("SeriesProgressUpdated", progressEvent);
    }

    /// <summary>
    /// Backward-compatible alias for TournamentProgressUpdated.
    /// </summary>
    public async Task SeriesProgressUpdated(TournamentProgressUpdatedEventDto progressEvent)
    {
        await TournamentProgressUpdated(progressEvent);
    }

    /// <summary>
    /// Receive EventStepCompleted event from ConsoleEventPublisher
    /// </summary>
    public async Task EventStepCompleted(EventStepCompletedDto completedEvent)
    {
        _logger.LogInformation("Event step completed: Step {StepIndex} Winner {Winner}",
            completedEvent.StepIndex, completedEvent.WinnerName ?? "Unknown");

        await _stateManager.UpdateEventStepCompletedAsync(completedEvent);

        await Clients.Group("TournamentViewers").SendAsync("EventStepCompleted", completedEvent);
    }

    /// <summary>
    /// Receive TournamentCompleted event (whole tournament) from ConsoleEventPublisher
    /// </summary>
    public async Task TournamentCompleted(TournamentCompletedEventDto completedEvent)
    {
        _logger.LogInformation("Tournament completed: {TournamentName} Champion {Champion}",
            completedEvent.TournamentName, completedEvent.Champion);

        await _stateManager.UpdateTournamentCompletedAsync(completedEvent);

        await Clients.Group("TournamentViewers").SendAsync("TournamentCompleted", completedEvent);
    }

    /// <summary>
    /// Update current state (for real-time state sync)
    /// </summary>
    public async Task CurrentState(DashboardStateDto state)
    {
        _logger.LogInformation("Received dashboard state update: {Status} - {Message}", state.Status, state.Message);
        
        // Update the state manager
        await _stateManager.UpdateStateAsync(state);
        
        // Broadcast to all connected viewers
        await Clients.Group("TournamentViewers").SendAsync("CurrentState", state);
    }

    /// <summary>
    /// Publish a state update from tournament engine (simulator or real engine).
    /// This updates the state and broadcasts to all connected clients.
    /// </summary>
    public async Task PublishStateUpdate(DashboardStateDto state)
    {
        _logger.LogInformation("Received dashboard state update: {Status} - {Message}", state.Status, state.Message);
        
        // Update the state manager
        await _stateManager.UpdateStateAsync(state);
        
        // Broadcast to all connected viewers
        await Clients.Group("TournamentViewers").SendAsync("CurrentState", state);
        
        // Also broadcast standings update if available
        if (state.OverallLeaderboard?.Count > 0)
        {
            await Clients.Group("TournamentViewers").SendAsync("StandingsUpdated", new
            {
                OverallStandings = state.OverallLeaderboard,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Publish a match completion event from tournament engine.
    /// </summary>
    public async Task PublishMatchCompleted(RecentMatchDto match)
    {
        _logger.LogInformation("Match completed: {Bot1} vs {Bot2} - Winner: {Winner}", 
            match.Bot1Name, match.Bot2Name, match.WinnerName ?? "Draw");
        
        // Broadcast match completion to all viewers
        await Clients.Group("TournamentViewers").SendAsync("MatchCompleted", match);
    }

    #region Bot Dashboard Events

    /// <summary>
    /// Broadcasts a bot submission event to all connected clients.
    /// </summary>
    public async Task BroadcastBotSubmitted(BotDashboardDto botDto)
    {
        if (botDto == null)
            throw new ArgumentNullException(nameof(botDto));

        _logger.LogInformation("Broadcasting bot submission for team: {TeamName}", botDto.TeamName);
        await Clients.All.SendAsync("BotSubmitted", botDto);
    }

    /// <summary>
    /// Broadcasts a bot validation completion event to all connected clients.
    /// </summary>
    public async Task BroadcastBotValidated(BotDashboardDto botDto)
    {
        if (botDto == null)
            throw new ArgumentNullException(nameof(botDto));

        _logger.LogInformation("Broadcasting bot validation for team: {TeamName}, Status: {Status}", 
            botDto.TeamName, botDto.Status);
        await Clients.All.SendAsync("BotValidated", botDto);
    }

    /// <summary>
    /// Broadcasts a bot deletion event to all connected clients.
    /// </summary>
    public async Task BroadcastBotDeleted(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            throw new ArgumentNullException(nameof(teamName));

        _logger.LogInformation("Broadcasting bot deletion for team: {TeamName}", teamName);
        await Clients.All.SendAsync("BotDeleted", teamName);
    }

    /// <summary>
    /// Broadcasts an updated bot list to all connected clients.
    /// </summary>
    public async Task BroadcastBotListUpdated(List<BotDashboardDto> bots)
    {
        if (bots == null)
            throw new ArgumentNullException(nameof(bots));

        _logger.LogInformation("Broadcasting updated bot list with {Count} bots", bots.Count);
        await Clients.All.SendAsync("BotListUpdated", bots);
    }

    /// <summary>
    /// Broadcasts validation progress for a bot to all connected clients.
    /// </summary>
    public async Task BroadcastValidationProgress(string teamName, string message)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            throw new ArgumentNullException(nameof(teamName));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        _logger.LogInformation("Broadcasting validation progress for team {TeamName}: {Message}", 
            teamName, message);
        
        await Clients.All.SendAsync("ValidationProgress", new
        {
            teamName = teamName,
            message = message,
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    /// <summary>
    /// Gets current management state and bot readiness (callable by clients).
    /// </summary>
    public async Task GetManagementState()
    {
        if (_managementService != null)
        {
            var state = await _managementService.GetStateAsync();
            var (ready, message, botCount) = await _managementService.CheckBotsReadyAsync();
            
            await Clients.Caller.SendAsync("ManagementStateResponse", new
            {
                state,
                readiness = new { ready, message, botCount }
            });
        }
    }

    /// <summary>
    /// Start a new tournament (called by clients via SignalR).
    /// </summary>
    public async Task StartTournament()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested tournament start");
        var result = await _managementService.StartAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("TournamentStarted");
        }
    }

    /// <summary>
    /// Pause the current tournament (called by clients via SignalR).
    /// </summary>
    public async Task PauseTournament()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested tournament pause");
        var result = await _managementService.PauseAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("TournamentPaused");
        }
    }

    /// <summary>
    /// Resume a paused tournament (called by clients via SignalR).
    /// </summary>
    public async Task ResumeTournament()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested tournament resume");
        var result = await _managementService.ResumeAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("TournamentResumed");
        }
    }

    /// <summary>
    /// Stop the current tournament (called by clients via SignalR).
    /// </summary>
    public async Task StopTournament()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested tournament stop");
        var result = await _managementService.StopAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("TournamentStopped");
        }
    }

    /// <summary>
    /// Clear all bot submissions and reset state (called by clients via SignalR).
    /// </summary>
    public async Task ClearSubmissions()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested clear submissions");
        var result = await _managementService.ClearSubmissionsAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("SubmissionsCleared");
        }
    }

    /// <summary>
    /// Rerun the last tournament (called by clients via SignalR).
    /// </summary>
    public async Task RerunTournament()
    {
        if (_managementService == null)
        {
            await Clients.Caller.SendAsync("Error", "Management service not available");
            return;
        }

        _logger.LogInformation("Client requested tournament rerun");
        var result = await _managementService.RerunAsync();
        
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
        else
        {
            await Clients.All.SendAsync("TournamentRerun");
        }
    }
}
