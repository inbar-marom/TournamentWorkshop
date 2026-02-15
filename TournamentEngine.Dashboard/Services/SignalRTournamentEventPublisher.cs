namespace TournamentEngine.Dashboard.Services;

using Microsoft.AspNetCore.SignalR;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Dashboard.Hubs;

/// <summary>
/// Publishes tournament events to SignalR Hub for real-time dashboard updates.
/// Used by TournamentManager to stream live tournament data to connected clients.
/// </summary>
public class SignalRTournamentEventPublisher : ITournamentEventPublisher
{
    private readonly IHubContext<TournamentHub> _hubContext;
    private readonly StateManagerService _stateManager;
    private readonly ILogger<SignalRTournamentEventPublisher> _logger;

    public SignalRTournamentEventPublisher(
        IHubContext<TournamentHub> hubContext,
        StateManagerService stateManager,
        ILogger<SignalRTournamentEventPublisher> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        _logger.LogDebug("Publishing match completed: {Bot1} vs {Bot2} - Winner: {Winner}",
            matchEvent.Bot1Name, matchEvent.Bot2Name, matchEvent.WinnerName ?? "Draw");

        // Add to state manager for persistence
        await _stateManager.AddMatchAsync(matchEvent);

        // Convert to RecentMatchDto for hub
        var recentMatch = new RecentMatchDto
        {
            MatchId = matchEvent.MatchId,
            Bot1Name = matchEvent.Bot1Name,
            Bot2Name = matchEvent.Bot2Name,
            Outcome = matchEvent.Outcome,
            WinnerName = matchEvent.WinnerName,
            Bot1Score = matchEvent.Bot1Score,
            Bot2Score = matchEvent.Bot2Score,
            CompletedAt = matchEvent.CompletedAt,
            GameType = matchEvent.GameType
        };

        // Broadcast to all connected clients
        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("MatchCompleted", recentMatch);
    }

    public async Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        _logger.LogDebug("Publishing standings update");

        await _stateManager.UpdateStandingsAsync(standingsEvent);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("StandingsUpdated", new
            {
                OverallStandings = standingsEvent.OverallStandings,
                Timestamp = standingsEvent.UpdatedAt
            });
    }

    public async Task PublishEventStartedAsync(EventStartedEventDto startEvent)
    {
        _logger.LogInformation("Publishing event started: {GameType} with {TotalBots} bots",
            startEvent.GameType, startEvent.TotalBots);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("EventStarted", startEvent);
    }

    public async Task PublishEventCompletedAsync(EventCompletedEventDto completedEvent)
    {
        _logger.LogInformation("Publishing event completed: Champion = {Champion}",
            completedEvent.Champion);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("EventCompleted", completedEvent);
    }

    public async Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        _logger.LogInformation("Publishing round started: Round {RoundNumber}",
            roundEvent.RoundNumber);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("RoundStarted", roundEvent);
    }

    public async Task UpdateCurrentStateAsync(DashboardStateDto state)
    {
        _logger.LogInformation("Updating current dashboard state: {Status} - {Message}",
            state.Status, state.Message);

        // Update the state manager
        await _stateManager.UpdateStateAsync(state);

        var currentState = await _stateManager.GetCurrentStateAsync();

        // Broadcast to all connected viewers
        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("CurrentState", currentState);
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedEventDto tournamentEvent)
    {
        _logger.LogInformation("Publishing tournament started: {TournamentName} with {TotalSteps} steps",
            tournamentEvent.TournamentName, tournamentEvent.TotalSteps);

        await _stateManager.UpdateTournamentStartedAsync(tournamentEvent);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("TournamentStarted", tournamentEvent);
    }

    public async Task PublishTournamentProgressUpdatedAsync(TournamentProgressUpdatedEventDto progressEvent)
    {
        _logger.LogInformation("Publishing tournament progress update: Step {StepIndex}/{TotalSteps}",
            progressEvent.TournamentState.CurrentStepIndex, progressEvent.TournamentState.TotalSteps);

        await _stateManager.UpdateTournamentProgressAsync(progressEvent);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("TournamentProgressUpdated", progressEvent);
    }

    public async Task PublishEventStepCompletedAsync(EventStepCompletedDto completedEvent)
    {
        _logger.LogInformation("Publishing event step completed: Step {StepIndex} - Winner {Winner}",
            completedEvent.StepIndex, completedEvent.WinnerName ?? "Unknown");

        await _stateManager.UpdateEventStepCompletedAsync(completedEvent);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("EventStepCompleted", completedEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedEventDto completedEvent)
    {
        _logger.LogInformation("Publishing tournament completed: {TournamentName} - Champion {Champion}",
            completedEvent.TournamentName, completedEvent.Champion);

        await _stateManager.UpdateTournamentCompletedAsync(completedEvent);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("SeriesCompleted", completedEvent);
    }
}
