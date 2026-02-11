namespace TournamentEngine.Console.Utilities;

using System.Threading.Tasks;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;

/// <summary>
/// No-operation event publisher for console execution.
/// Used when dashboard is not available or not needed.
/// Implements ITournamentEventPublisher but performs no actual publishing.
/// </summary>
public class NoOpEventPublisher : ITournamentEventPublisher
{
    public Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        return Task.CompletedTask;
    }

    public Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        return Task.CompletedTask;
    }

    public Task PublishTournamentStartedAsync(TournamentStartedDto startEvent)
    {
        return Task.CompletedTask;
    }

    public Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        return Task.CompletedTask;
    }

    public Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        return Task.CompletedTask;
    }

    public Task UpdateCurrentStateAsync(TournamentStateDto state)
    {
        return Task.CompletedTask;
    }
}
