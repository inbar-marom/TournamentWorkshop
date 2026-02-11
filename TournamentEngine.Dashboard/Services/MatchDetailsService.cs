using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing match details and statistics.
/// </summary>
public class MatchDetailsService
{
    private readonly StateManagerService _stateManager;
    private readonly MatchFeedService _matchFeed;

    public MatchDetailsService(StateManagerService stateManager, MatchFeedService matchFeed)
    {
        _stateManager = stateManager;
        _matchFeed = matchFeed;
    }

    /// <summary>
    /// Gets detailed information about a specific match.
    /// </summary>
    public async Task<MatchDetailDto?> GetMatchDetailsAsync(string matchId)
    {
        var match = await _matchFeed.GetMatchByIdAsync(matchId);
        if (match == null)
            return null;

        return new MatchDetailDto
        {
            MatchId = match.MatchId,
            Bot1 = match.Bot1Name,
            Bot2 = match.Bot2Name,
            Winner = match.WinnerName,
            Outcome = match.Outcome,
            CompletedAt = match.CompletedAt,
            CompletedAtFormatted = FormatRelativeTime(match.CompletedAt),
            GameType = match.GameType,
            Bot1Score = match.Bot1Score,
            Bot2Score = match.Bot2Score
        };
    }

    /// <summary>
    /// Gets match statistics for a team.
    /// </summary>
    public async Task<TeamMatchStatisticsDto> GetTeamMatchStatisticsAsync(string teamName)
    {
        var matches = await _matchFeed.GetMatchesForTeamAsync(teamName, 10000);
        var stats = new TeamMatchStatisticsDto { TeamName = teamName };

        if (matches.Count == 0)
            return stats;

        stats.TotalMatches = matches.Count;

        foreach (var match in matches)
        {
            if (match.WinnerName == teamName)
                stats.Wins++;
            else if (match.Outcome == MatchOutcome.Draw)
                stats.Draws++;
            else
                stats.Losses++;
        }

        stats.WinPercentage = stats.TotalMatches > 0 
            ? Math.Round((stats.Wins / (double)stats.TotalMatches) * 100, 2)
            : 0;

        return stats;
    }

    /// <summary>
    /// Gets recent matches for a specific team.
    /// </summary>
    public async Task<List<RecentMatchDto>> GetRecentMatchesForTeamAsync(string teamName, int count = 10)
    {
        var matches = await _matchFeed.GetMatchesForTeamAsync(teamName, count);
        return matches.OrderByDescending(m => m.CompletedAt).ToList();
    }

    /// <summary>
    /// Gets head-to-head history between two teams.
    /// </summary>
    public async Task<HeadToHeadHistoryDto> GetHeadToHeadHistoryAsync(string team1, string team2)
    {
        var team1Matches = await _matchFeed.GetMatchesForTeamAsync(team1, 10000);
        var history = new HeadToHeadHistoryDto { Team1 = team1, Team2 = team2 };

        var headToHead = team1Matches.Where(m =>
            (m.Bot1Name == team1 && m.Bot2Name == team2) ||
            (m.Bot1Name == team2 && m.Bot2Name == team1)).ToList();

        foreach (var match in headToHead)
        {
            if (match.WinnerName == team1)
                history.Team1Wins++;
            else if (match.WinnerName == team2)
                history.Team2Wins++;
            else
                history.Draws++;
        }

        history.TotalMatches = headToHead.Count;
        history.Matches = headToHead.OrderByDescending(m => m.CompletedAt).ToList();

        return history;
    }

    /// <summary>
    /// Gets match winning trend for a team over time.
    /// </summary>
    public async Task<List<MatchTrendDto>> GetMatchTrendAsync(string teamName, int matchCount = 20)
    {
        var matches = await GetRecentMatchesForTeamAsync(teamName, matchCount);
        var trend = new List<MatchTrendDto>();

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            bool isWin = match.WinnerName == teamName;

            trend.Add(new MatchTrendDto
            {
                MatchNumber = i + 1,
                IsWin = isWin,
                OpponentName = match.Bot1Name == teamName ? match.Bot2Name : match.Bot1Name,
                CompletedAt = match.CompletedAt,
                Outcome = match.Outcome
            });
        }

        return trend;
    }

    /// <summary>
    /// Gets performance comparison between two teams.
    /// </summary>
    public async Task<TeamComparisonDto> CompareTeamsAsync(string team1, string team2)
    {
        var team1Stats = await GetTeamMatchStatisticsAsync(team1);
        var team2Stats = await GetTeamMatchStatisticsAsync(team2);

        return new TeamComparisonDto
        {
            Team1Name = team1,
            Team1Wins = team1Stats.Wins,
            Team1WinPercentage = team1Stats.WinPercentage,
            Team1TotalMatches = team1Stats.TotalMatches,
            Team2Name = team2,
            Team2Wins = team2Stats.Wins,
            Team2WinPercentage = team2Stats.WinPercentage,
            Team2TotalMatches = team2Stats.TotalMatches
        };
    }

    /// <summary>
    /// Formats a DateTime as relative time (e.g., "5 minutes ago").
    /// </summary>
    private string FormatRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime.ToUniversalTime();

        if (span.TotalSeconds < 60)
            return $"{(int)span.TotalSeconds} seconds ago";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays} days ago";

        return dateTime.ToString("MMM dd, yyyy");
    }
}

/// <summary>
/// DTO for match details.
/// </summary>
public class MatchDetailDto
{
    public string MatchId { get; set; } = string.Empty;
    public string Bot1 { get; set; } = string.Empty;
    public string Bot2 { get; set; } = string.Empty;
    public string? Winner { get; set; }
    public MatchOutcome Outcome { get; set; }
    public DateTime CompletedAt { get; set; }
    public string CompletedAtFormatted { get; set; } = string.Empty;
    public GameType GameType { get; set; }
    public int Bot1Score { get; set; }
    public int Bot2Score { get; set; }
}

/// <summary>
/// DTO for team match statistics.
/// </summary>
public class TeamMatchStatisticsDto
{
    public string TeamName { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public double WinPercentage { get; set; }
}

/// <summary>
/// DTO for head-to-head history.
/// </summary>
public class HeadToHeadHistoryDto
{
    public string Team1 { get; set; } = string.Empty;
    public string Team2 { get; set; } = string.Empty;
    public int Team1Wins { get; set; }
    public int Team2Wins { get; set; }
    public int Draws { get; set; }
    public int TotalMatches { get; set; }
    public List<RecentMatchDto> Matches { get; set; } = new();
}

/// <summary>
/// DTO for match trend data.
/// </summary>
public class MatchTrendDto
{
    public int MatchNumber { get; set; }
    public bool IsWin { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public MatchOutcome Outcome { get; set; }
}

/// <summary>
/// DTO for team comparison.
/// </summary>
public class TeamComparisonDto
{
    public string Team1Name { get; set; } = string.Empty;
    public int Team1Wins { get; set; }
    public double Team1WinPercentage { get; set; }
    public int Team1TotalMatches { get; set; }

    public string Team2Name { get; set; } = string.Empty;
    public int Team2Wins { get; set; }
    public double Team2WinPercentage { get; set; }
    public int Team2TotalMatches { get; set; }
}
