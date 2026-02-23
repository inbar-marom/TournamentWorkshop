using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Tournament event publisher that updates StateManagerService as the single source of truth.
/// </summary>
public class StateTrackingTournamentEventPublisher : ITournamentEventPublisher
{
    private readonly StateManagerService _stateManager;
    private readonly ILogger<StateTrackingTournamentEventPublisher> _logger;

    public StateTrackingTournamentEventPublisher(
        StateManagerService stateManager,
        ILogger<StateTrackingTournamentEventPublisher> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        _stateManager.AddRecentMatch(matchEvent);
        return Task.CompletedTask;
    }

    public Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
        => _stateManager.UpdateStandingsAsync(standingsEvent);

    public Task PublishEventStartedAsync(EventStartedEventDto startEvent)
        => _stateManager.UpdateEventStartedAsync(startEvent);

    public Task PublishEventCompletedAsync(EventCompletedEventDto completedEvent)
        => _stateManager.UpdateEventCompletedAsync(completedEvent);

    public Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
        => Task.CompletedTask;

    public Task UpdateCurrentStateAsync(DashboardStateDto state)
        => _stateManager.UpdateStateAsync(state);

    public Task PublishTournamentStartedAsync(TournamentStartedEventDto tournamentEvent)
        => _stateManager.UpdateTournamentStartedAsync(tournamentEvent);

    public Task PublishTournamentProgressUpdatedAsync(TournamentProgressUpdatedEventDto progressEvent)
        => _stateManager.UpdateTournamentProgressAsync(progressEvent);

    public Task PublishEventStepCompletedAsync(EventStepCompletedDto completedEvent)
        => _stateManager.UpdateEventStepCompletedAsync(completedEvent);

    public Task PublishTournamentCompletedAsync(TournamentCompletedEventDto completedEvent)
        => _stateManager.UpdateTournamentCompletedAsync(completedEvent);
}
