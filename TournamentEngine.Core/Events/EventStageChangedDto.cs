namespace TournamentEngine.Core.Events;

using TournamentEngine.Core.Common;

/// <summary>
/// Event raised when an event transitions between stages (e.g., Group Stage to Playoff Groups)
/// </summary>
public class EventStageChangedDto
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
    /// Previous stage
    /// </summary>
    public EventStage PreviousStage { get; init; }

    /// <summary>
    /// New stage
    /// </summary>
    public required EventStage NewStage { get; init; }

    /// <summary>
    /// Playoff group (if transitioning to playoff stage)
    /// </summary>
    public GroupInfo? PlayoffGroup { get; init; }

    /// <summary>
    /// When the stage changed
    /// </summary>
    public DateTime ChangedAt { get; init; }
}
