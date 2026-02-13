namespace TournamentEngine.Core.Events;

using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// Event raised when group standings are updated
/// </summary>
public class GroupStandingsUpdatedDto
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Event name
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Updated standings for this group
    /// </summary>
    public List<TeamStandingDto> Standings { get; init; } = new();

    /// <summary>
    /// Current stage of the event
    /// </summary>
    public EventStage Stage { get; init; }

    /// <summary>
    /// When the standings were updated
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
