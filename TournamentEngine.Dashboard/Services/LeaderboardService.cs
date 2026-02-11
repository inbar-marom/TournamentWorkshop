using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for formatting and managing leaderboard data for UI display.
/// Phase 4: Basic UI - Leaderboard Component Service
/// </summary>
public class LeaderboardService
{
    private readonly StateManagerService _stateManager;

    public LeaderboardService(StateManagerService stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    /// <summary>
    /// Get formatted leaderboard data for display.
    /// </summary>
    public async Task<List<TeamStandingDto>> GetLeaderboardDataAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state.OverallLeaderboard ?? new List<TeamStandingDto>();
    }

    /// <summary>
    /// Get leaderboard with rank changes highlighted.
    /// </summary>
    public async Task<List<TeamStandingDto>> GetLeaderboardWithRanksAsync()
    {
        var leaderboard = await GetLeaderboardDataAsync();
        
        // Filter teams with rank changes for highlighting
        return leaderboard
            .Where(team => team.RankChange != 0)
            .ToList();
    }

    /// <summary>
    /// Get top N teams from leaderboard.
    /// </summary>
    public async Task<List<TeamStandingDto>> GetTopTeamsAsync(int count = 10)
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return leaderboard
            .OrderBy(t => t.Rank)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Check if leaderboard has valid data.
    /// </summary>
    public async Task<bool> HasLeaderboardDataAsync()
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return leaderboard.Any();
    }

    /// <summary>
    /// Get leaderboard sorted by total points (descending).
    /// </summary>
    public async Task<List<TeamStandingDto>> GetLeaderboardByPointsAsync()
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return leaderboard
            .OrderByDescending(t => t.TotalPoints)
            .ThenByDescending(t => t.TotalWins)
            .ToList();
    }

    /// <summary>
    /// Get team standing by name.
    /// </summary>
    public async Task<TeamStandingDto?> GetTeamStandingAsync(string teamName)
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return leaderboard.FirstOrDefault(t => t.TeamName == teamName);
    }
}
