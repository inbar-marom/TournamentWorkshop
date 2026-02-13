using Microsoft.AspNetCore.SignalR;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Dashboard.Hubs;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Implementation of ITournamentEventPublisher that uses SignalR to broadcast events to the dashboard.
/// </summary>
public class SignalREventPublisher : ITournamentEventPublisher
{
    private readonly IHubContext<TournamentHub> _hubContext;
    private const string ViewersGroup = "TournamentViewers";

    public SignalREventPublisher(IHubContext<TournamentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        await _hubContext.Clients.All.SendAsync("MatchCompleted", matchEvent);
    }

    public async Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        await _hubContext.Clients.All.SendAsync("StandingsUpdated", standingsEvent);
    }

    public async Task PublishEventStartedAsync(EventStartedEventDto startEvent)
    {
        await _hubContext.Clients.All.SendAsync("EventStarted", startEvent);
    }

    public async Task PublishEventCompletedAsync(EventCompletedEventDto completedEvent)
    {
        await _hubContext.Clients.All.SendAsync("EventCompleted", completedEvent);
    }

    public async Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        await _hubContext.Clients.All.SendAsync("RoundStarted", roundEvent);
    }

    public async Task UpdateCurrentStateAsync(DashboardStateDto state)
    {
        await _hubContext.Clients.All.SendAsync("CurrentState", state);
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedEventDto tournamentEvent)
    {
        await _hubContext.Clients.All.SendAsync("TournamentStarted", tournamentEvent);
    }

    public async Task PublishTournamentProgressUpdatedAsync(TournamentProgressUpdatedEventDto progressEvent)
    {
        await _hubContext.Clients.All.SendAsync("TournamentProgressUpdated", progressEvent);
    }

    public async Task PublishEventStepCompletedAsync(EventStepCompletedDto completedEvent)
    {
        await _hubContext.Clients.All.SendAsync("EventStepCompleted", completedEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedEventDto completedEvent)
    {
        await _hubContext.Clients.All.SendAsync("TournamentCompleted", completedEvent);
    }
}
