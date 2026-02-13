namespace TournamentEngine.Core.Common;

using TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// Represents a group of teams within an event
/// </summary>
public class GroupInfo
{
    /// <summary>
    /// Group name (e.g., "Group A", "Group B", "Playoff Group")
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Current standings for teams in this group
    /// </summary>
    public List<TeamStandingDto> Standings { get; init; } = new();

    /// <summary>
    /// Whether this group is currently active (has ongoing matches)
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Stage this group belongs to
    /// </summary>
    public EventStage Stage { get; init; }

    /// <summary>
    /// Teams competing in this group
    /// </summary>
    public List<string> TeamNames { get; init; } = new();
}
