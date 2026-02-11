using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for providing chart data for dashboard visualizations.
/// </summary>
public class ChartsService
{
    private readonly StateManagerService _stateManager;
    private readonly MatchFeedService _matchFeed;

    public ChartsService(StateManagerService stateManager, MatchFeedService matchFeed)
    {
        _stateManager = stateManager;
        _matchFeed = matchFeed;
    }

    /// <summary>
    /// Gets win/loss ratio chart data for all teams.
    /// </summary>
    public async Task<WinRatioChartDataDto> GetWinRatioChartDataAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var data = new WinRatioChartDataDto();

        if (state?.OverallLeaderboard == null || state.OverallLeaderboard.Count == 0)
            return data;

        foreach (var team in state.OverallLeaderboard.OrderByDescending(t => t.TotalPoints))
        {
            data.TeamNames.Add(team.TeamName);
            data.WinCounts.Add(team.TotalWins);
            data.LossCounts.Add(team.TotalLosses);

            int total = team.TotalWins + team.TotalLosses;
            double percentage = total > 0 ? (team.TotalWins / (double)total) * 100 : 0;
            data.WinPercentages.Add(Math.Round(percentage, 2));
        }

        return data;
    }

    /// <summary>
    /// Gets points distribution chart data.
    /// </summary>
    public async Task<PointsDistributionChartDataDto> GetPointsDistributionChartDataAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var data = new PointsDistributionChartDataDto();

        if (state?.OverallLeaderboard == null || state.OverallLeaderboard.Count == 0)
            return data;

        // Sort by points descending for better chart visualization
        foreach (var team in state.OverallLeaderboard.OrderByDescending(t => t.TotalPoints))
        {
            data.TeamNames.Add(team.TeamName);
            data.Points.Add(team.TotalPoints);
        }

        return data;
    }

    /// <summary>
    /// Gets match history trend data over time.
    /// </summary>
    public async Task<MatchHistoryTrendDataDto> GetMatchHistoryTrendDataAsync()
    {
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);
        var data = new MatchHistoryTrendDataDto();

        if (matches.Count == 0)
            return data;

        // Group matches by hour
        var groupedByHour = matches
            .GroupBy(m => m.CompletedAt.ToUniversalTime().AddMinutes(-m.CompletedAt.Minute).AddSeconds(-m.CompletedAt.Second))
            .OrderBy(g => g.Key);

        foreach (var hourGroup in groupedByHour)
        {
            data.TimeLabels.Add(hourGroup.Key.ToString("HH:mm"));
            data.TotalMatchesByTime.Add(hourGroup.Count());
        }

        return data;
    }

    /// <summary>
    /// Gets game type distribution chart data.
    /// </summary>
    public async Task<GameTypeDistributionChartDataDto> GetGameTypeDistributionChartDataAsync()
    {
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);
        var data = new GameTypeDistributionChartDataDto();

        if (matches.Count == 0)
            return data;

        var groupedByType = matches.GroupBy(m => m.GameType);

        foreach (var typeGroup in groupedByType)
        {
            data.GameTypeLabels.Add(typeGroup.Key.ToString());
            data.MatchCounts.Add(typeGroup.Count());
        }

        return data;
    }

    /// <summary>
    /// Gets match outcome distribution (wins, losses, draws).
    /// </summary>
    public async Task<MatchOutcomeDistributionChartDataDto> GetMatchOutcomeDistributionChartDataAsync()
    {
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);
        var data = new MatchOutcomeDistributionChartDataDto();

        if (matches.Count == 0)
            return data;

        int wins = matches.Count(m => m.Outcome == MatchOutcome.Player1Wins || m.Outcome == MatchOutcome.Player2Wins);
        int draws = matches.Count(m => m.Outcome == MatchOutcome.Draw);

        data.OutcomeLabels.AddRange(new[] { "Wins", "Draws" });
        data.OutcomeCounts.AddRange(new[] { wins, draws });

        return data;
    }

    /// <summary>
    /// Gets head-to-head chart data between two teams.
    /// </summary>
    public async Task<HeadToHeadChartDataDto> GetTeamHeadToHeadChartAsync(string team1, string team2)
    {
        var data = new HeadToHeadChartDataDto();
        var matches = await _matchFeed.GetMatchesForTeamAsync(team1, 1000);

        var headToHead = matches.Where(m => 
            (m.Bot1Name == team1 && m.Bot2Name == team2) ||
            (m.Bot1Name == team2 && m.Bot2Name == team1)).ToList();

        foreach (var match in headToHead)
        {
            if (match.WinnerName == team1)
                data.TeamAWins++;
            else if (match.WinnerName == team2)
                data.TeamBWins++;
            else
                data.Draws++;
        }

        data.TotalMatches = headToHead.Count;
        return data;
    }
}

/// <summary>
/// DTO for win ratio chart data.
/// </summary>
public class WinRatioChartDataDto
{
    public List<string> TeamNames { get; set; } = new();
    public List<int> WinCounts { get; set; } = new();
    public List<int> LossCounts { get; set; } = new();
    public List<double> WinPercentages { get; set; } = new();
}

/// <summary>
/// DTO for points distribution chart data.
/// </summary>
public class PointsDistributionChartDataDto
{
    public List<string> TeamNames { get; set; } = new();
    public List<int> Points { get; set; } = new();
}

/// <summary>
/// DTO for match history trend data.
/// </summary>
public class MatchHistoryTrendDataDto
{
    public List<string> TimeLabels { get; set; } = new();
    public List<int> TotalMatchesByTime { get; set; } = new();
}

/// <summary>
/// DTO for game type distribution chart data.
/// </summary>
public class GameTypeDistributionChartDataDto
{
    public List<string> GameTypeLabels { get; set; } = new();
    public List<int> MatchCounts { get; set; } = new();
}

/// <summary>
/// DTO for match outcome distribution chart data.
/// </summary>
public class MatchOutcomeDistributionChartDataDto
{
    public List<string> OutcomeLabels { get; set; } = new();
    public List<int> OutcomeCounts { get; set; } = new();
}

/// <summary>
/// DTO for head-to-head chart data.
/// </summary>
public class HeadToHeadChartDataDto
{
    public int TeamAWins { get; set; }
    public int TeamBWins { get; set; }
    public int Draws { get; set; }
    public int TotalMatches { get; set; }
}
