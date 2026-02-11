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
        _logger.LogInformation("Publishing match completed: {Bot1} vs {Bot2} - Winner: {Winner}",
            matchEvent.Bot1Name, matchEvent.Bot2Name, matchEvent.WinnerName ?? "Draw");

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
        _logger.LogInformation("Publishing standings update");

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("StandingsUpdated", new
            {
                OverallStandings = standingsEvent.OverallStandings,
                Timestamp = standingsEvent.UpdatedAt
            });
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedDto startEvent)
    {
        _logger.LogInformation("Publishing tournament started: {GameType} with {TotalBots} bots",
            startEvent.GameType, startEvent.TotalBots);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("TournamentStarted", startEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        _logger.LogInformation("Publishing tournament completed: Champion = {Champion}",
            completedEvent.Champion);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("TournamentCompleted", completedEvent);
    }

    public async Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        _logger.LogInformation("Publishing round started: Round {RoundNumber}",
            roundEvent.RoundNumber);

        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("RoundStarted", roundEvent);
    }

    public async Task UpdateCurrentStateAsync(TournamentStateDto state)
    {
        _logger.LogInformation("Updating current state: {Status} - {Message}",
            state.Status, state.Message);

        // Update the state manager
        await _stateManager.UpdateStateAsync(state);

        // Broadcast to all connected viewers
        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("CurrentState", state);
    }
}
