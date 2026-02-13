namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a single event (game type) within a tournament.
/// Previously known as "Tournament" - now renamed to "Event" to avoid confusion.
/// </summary>
public class EventInfo
{
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Event name (e.g., "RPSLS Event 1")
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Type of game being played in this event
    /// </summary>
    public required GameType GameType { get; init; }

    /// <summary>
    /// Current status of the event
    /// </summary>
    public EventStatus Status { get; set; }

    /// <summary>
    /// Current stage of the event (Group Stage or Playoff Groups)
    /// </summary>
    public EventStage Stage { get; set; }

    /// <summary>
    /// Groups competing in this event
    /// </summary>
    public List<GroupInfo> Groups { get; init; } = new();

    /// <summary>
    /// List of bots competing in this event
    /// </summary>
    public List<BotInfo> Bots { get; init; } = new();

    /// <summary>
    /// All match results for this event
    /// </summary>
    public List<MatchResult> MatchResults { get; init; } = new();

    /// <summary>
    /// Bracket structure for playoff rounds
    /// </summary>
    public Dictionary<int, List<string>> Bracket { get; init; } = new();

    /// <summary>
    /// Winner of this event (null if not completed)
    /// </summary>
    public string? WinnerTeamName { get; set; }

    /// <summary>
    /// Event start time
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Event end time (null if in progress)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Current round number
    /// </summary>
    public int CurrentRound { get; set; }

    /// <summary>
    /// Total number of rounds in this event
    /// </summary>
    public int TotalRounds { get; init; }

    /// <summary>
    /// Event index within the tournament (0-based)
    /// </summary>
    public int EventIndex { get; set; }
}

/// <summary>
/// Status of an event within a tournament
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Event has not started yet
    /// </summary>
    Pending,

    /// <summary>
    /// Event is currently running
    /// </summary>
    InProgress,

    /// <summary>
    /// Event has completed
    /// </summary>
    Completed
}

/// <summary>
/// Stage of an event (group play vs playoff)
/// </summary>
public enum EventStage
{
    /// <summary>
    /// Initial group stage with multiple groups
    /// </summary>
    GroupStage,

    /// <summary>
    /// Playoff groups with top teams from group stage
    /// </summary>
    PlayoffGroups
}
