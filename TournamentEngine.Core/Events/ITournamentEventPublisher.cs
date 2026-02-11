using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Core.Events;

/// <summary>
/// Interface for publishing tournament events to external listeners (like the dashboard)
/// </summary>
public interface ITournamentEventPublisher
{
    Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent);
    Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent);
    Task PublishTournamentStartedAsync(TournamentStartedDto startEvent);
    Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent);
    Task PublishRoundStartedAsync(RoundStartedDto roundEvent);
    Task UpdateCurrentStateAsync(TournamentStateDto state);
}
