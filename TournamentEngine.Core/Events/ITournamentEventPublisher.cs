using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Core.Events;

/// <summary>
/// Interface for publishing tournament events to external listeners (like the dashboard)
/// </summary>
public interface ITournamentEventPublisher
{
    Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent);
    Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent);
    Task PublishEventStartedAsync(EventStartedEventDto startEvent);
    Task PublishEventCompletedAsync(EventCompletedEventDto completedEvent);
    Task PublishRoundStartedAsync(RoundStartedDto roundEvent);
    Task UpdateCurrentStateAsync(DashboardStateDto state);
    Task PublishTournamentStartedAsync(TournamentStartedEventDto tournamentEvent);
    Task PublishTournamentProgressUpdatedAsync(TournamentProgressUpdatedEventDto progressEvent);
    Task PublishEventStepCompletedAsync(EventStepCompletedDto completedEvent);
    Task PublishTournamentCompletedAsync(TournamentCompletedEventDto completedEvent);
}
