namespace TournamentEngine.Core.Common.Dashboard;

using TournamentEngine.Core.Common;

/// <summary>
/// DTO representing complete tournament dashboard state snapshot.
/// </summary>
public class TournamentStateDto
{
    public string? TournamentId { get; set; }
    public string? TournamentName { get; set; }
    public string? Champion { get; set; }
    public TournamentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public SeriesProgressDto? SeriesProgress { get; set; }
    public CurrentTournamentDto? CurrentTournament { get; set; }
    public List<TeamStandingDto> OverallLeaderboard { get; set; } = new();
    public List<GroupDto> GroupStandings { get; set; } = new();
    public List<RecentMatchDto> RecentMatches { get; set; } = new();
    public NextMatchDto? NextMatch { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Tournament status enum.
/// </summary>
public enum TournamentStatus
{
    NotStarted,
    InProgress,
    Paused,
    Completed
}

/// <summary>
/// Series progress information.
/// </summary>
public class SeriesProgressDto
{
    public string SeriesId { get; set; } = string.Empty;
    public List<TournamentInSeriesDto> Tournaments { get; set; } = new();
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public int CurrentTournamentIndex { get; set; }
}

/// <summary>
/// Individual tournament within a series.
/// </summary>
public class TournamentInSeriesDto
{
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public TournamentItemStatus Status { get; set; }
    public string? Champion { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// Status of individual tournament in series.
/// </summary>
public enum TournamentItemStatus
{
    Pending,
    InProgress,
    Completed
}

/// <summary>
/// Current tournament details.
/// </summary>
public class CurrentTournamentDto
{
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public TournamentStage Stage { get; set; }
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public int MatchesCompleted { get; set; }
    public int TotalMatches { get; set; }
    public double ProgressPercentage { get; set; }
}

/// <summary>
/// Tournament stage enum.
/// </summary>
public enum TournamentStage
{
    GroupStage,
    Finals
}

/// <summary>
/// Team standing in overall series leaderboard.
/// </summary>
public class TeamStandingDto
{
    public int Rank { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int TournamentWins { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int RankChange { get; set; } // +N or -N since last update
}

/// <summary>
/// Group standings with all bots in group.
/// </summary>
public class GroupDto
{
    public string GroupName { get; set; } = string.Empty;
    public List<BotRankingDto> Rankings { get; set; } = new();
}

/// <summary>
/// Bot ranking within a group.
/// </summary>
public class BotRankingDto
{
    public int Rank { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Points { get; set; }
}

/// <summary>
/// Recent match result.
/// </summary>
public class RecentMatchDto
{
    public string MatchId { get; set; } = string.Empty;
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public string Bot1Name { get; set; } = string.Empty;
    public string Bot2Name { get; set; } = string.Empty;
    public MatchOutcome Outcome { get; set; }
    public string? WinnerName { get; set; }
    public int Bot1Score { get; set; }
    public int Bot2Score { get; set; }
    public DateTime CompletedAt { get; set; }
    public GameType GameType { get; set; }
}

/// <summary>
/// Match completed event (identical structure to RecentMatchDto).
/// </summary>
public class MatchCompletedDto
{
    public string MatchId { get; set; } = string.Empty;
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public string Bot1Name { get; set; } = string.Empty;
    public string Bot2Name { get; set; } = string.Empty;
    public MatchOutcome Outcome { get; set; }
    public string? WinnerName { get; set; }
    public int Bot1Score { get; set; }
    public int Bot2Score { get; set; }
    public DateTime CompletedAt { get; set; }
    public GameType GameType { get; set; }
}

/// <summary>
/// Next upcoming match preview.
/// </summary>
public class NextMatchDto
{
    public string Bot1Name { get; set; } = string.Empty;
    public string Bot2Name { get; set; } = string.Empty;
    public GameType GameType { get; set; }
    public int EstimatedSecondsUntilStart { get; set; }
}

/// <summary>
/// Round started event.
/// </summary>
public class RoundStartedDto
{
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<NextMatchDto> UpcomingMatches { get; set; } = new();
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Standings updated event.
/// </summary>
public class StandingsUpdatedDto
{
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public List<TeamStandingDto> OverallStandings { get; set; } = new();
    public List<GroupDto>? GroupStandings { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Tournament started event.
/// </summary>
public class TournamentStartedDto
{
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public int TotalBots { get; set; }
    public int TotalGroups { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Tournament completed event.
/// </summary>
public class TournamentCompletedDto
{
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public string Champion { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; }
}
