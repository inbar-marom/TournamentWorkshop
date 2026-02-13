namespace TournamentEngine.Core.Events;

using TournamentEngine.Core.Common;

/// <summary>
/// Event raised when an individual event (game type) starts within a tournament
/// </summary>
public class EventStartedDto
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
    /// Initial groups for this event
    /// </summary>
    public List<GroupInfo> Groups { get; init; } = new();

    /// <summary>
    /// Teams competing in this event
    /// </summary>
    public List<string> TeamNames { get; init; } = new();

    /// <summary>
    /// When the event started
    /// </summary>
    public DateTime StartedAt { get; init; }
}
