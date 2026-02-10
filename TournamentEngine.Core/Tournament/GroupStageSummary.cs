namespace TournamentEngine.Core.Tournament;

/// <summary>
/// Summary for the current tournament phase
/// </summary>
public class GroupStageSummary
{
    public required string PhaseId { get; init; }
    public List<GroupSummary> Groups { get; init; } = new();
    public List<string> PhaseWinners { get; init; } = new();
}

/// <summary>
/// Summary for a single group in a phase
/// </summary>
public class GroupSummary
{
    public required string GroupId { get; init; }
    public List<GroupStanding> Standings { get; init; } = new();
    public List<(string bot1, string bot2, string result)> Matches { get; init; } = new();
    public string Winner { get; init; } = string.Empty;
}
