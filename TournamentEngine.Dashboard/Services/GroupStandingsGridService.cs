using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing group standings grid display and operations.
/// </summary>
public class GroupStandingsGridService
{
    private readonly StateManagerService _stateManager;

    public GroupStandingsGridService(StateManagerService stateManager)
    {
        _stateManager = stateManager;
    }

    /// <summary>
    /// Gets the group standings grid organized by group name.
    /// </summary>
    public async Task<Dictionary<string, List<BotRankingDto>>> GetGroupStandingsGridAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var result = new Dictionary<string, List<BotRankingDto>>();

        if (state?.GroupStandings == null || state.GroupStandings.Count == 0)
        {
            return result;
        }

        foreach (var group in state.GroupStandings)
        {
            // Sort by points descending, then by wins descending
            var sortedRankings = group.Rankings
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.Wins)
                .ToList();

            result[group.GroupName] = sortedRankings;
        }

        return result;
    }

    /// <summary>
    /// Gets the number of groups in the current tournament.
    /// </summary>
    public async Task<int> GetGroupCountAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state?.GroupStandings?.Count ?? 0;
    }

    /// <summary>
    /// Gets all teams in a specific group.
    /// </summary>
    public async Task<List<BotRankingDto>> GetTeamsInGroupAsync(string groupName)
    {
        var grid = await GetGroupStandingsGridAsync();
        return grid.ContainsKey(groupName) ? grid[groupName] : new List<BotRankingDto>();
    }

    /// <summary>
    /// Gets whether group stage is currently active.
    /// </summary>
    public async Task<bool> IsGroupStageActiveAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state?.CurrentEvent?.Stage == TournamentStage.GroupStage &&
               state.GroupStandings?.Count > 0;
    }

    /// <summary>
    /// Gets the highest-ranked team in a group.
    /// </summary>
    public async Task<BotRankingDto?> GetGroupLeaderAsync(string groupName)
    {
        var teams = await GetTeamsInGroupAsync(groupName);
        return teams.FirstOrDefault();
    }

    /// <summary>
    /// Gets teams qualified to advance from a group (top N).
    /// </summary>
    public async Task<List<BotRankingDto>> GetQualifiedTeamsFromGroupAsync(string groupName, int advancingCount = 2)
    {
        var teams = await GetTeamsInGroupAsync(groupName);
        return teams.Take(advancingCount).ToList();
    }

    /// <summary>
    /// Gets total matches played in all groups.
    /// </summary>
    public async Task<int> GetTotalGroupMatchesPlayedAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        if (state?.GroupStandings == null)
            return 0;

        int total = 0;
        foreach (var group in state.GroupStandings)
        {
            foreach (var ranking in group.Rankings)
            {
                total += ranking.Wins + ranking.Losses + ranking.Draws;
            }
        }

        // Each match involves 2 teams, so divide by 2
        return total / 2;
    }
}
