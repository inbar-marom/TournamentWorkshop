namespace TournamentEngine.Core.Events;

using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// Event raised when an individual event (game type) completes within a tournament
/// </summary>
public class EventCompletedDto
{
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Event name
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Type of game for this event
    /// </summary>
    public required GameType GameType { get; init; }

    /// <summary>
    /// Event index (0-based) within the tournament
    /// </summary>
    public int EventIndex { get; init; }

    /// <summary>
    /// Total number of events in the tournament
    /// </summary>
    public int TotalEvents { get; init; }

    /// <summary>
    /// Winner of this event
    /// </summary>
    public required string WinnerTeamName { get; init; }

    /// <summary>
    /// Final standings for this event
    /// </summary>
    public List<TeamStandingDto> FinalStandings { get; init; } = new();

    /// <summary>
    /// When the event completed
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Total matches played in this event
    /// </summary>
    public int TotalMatches { get; init; }
}
