using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for formatting and managing match feed data for UI display.
/// Phase 4: Basic UI - Match Feed Component Service
/// </summary>
public class MatchFeedService
{
    private readonly StateManagerService _stateManager;

    public MatchFeedService(StateManagerService stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    /// <summary>
    /// Get recent matches for display in feed.
    /// Returns most recent 20 matches in chronological order (newest first).
    /// </summary>
    public async Task<List<RecentMatchDto>> GetRecentMatchesAsync(int count = 20)
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var matches = state.RecentMatches ?? new List<RecentMatchDto>();

        return matches
            .OrderByDescending(m => m.CompletedAt)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get matches for a specific game type only.
    /// </summary>
    public async Task<List<RecentMatchDto>> GetMatchesByGameTypeAsync(GameType gameType, int count = 20)
    {
        var matches = await GetRecentMatchesAsync(count);
        return matches
            .Where(m => m.GameType == gameType)
            .ToList();
    }

    /// <summary>
    /// Get matches involving a specific team/bot.
    /// </summary>
    public async Task<List<RecentMatchDto>> GetMatchesForTeamAsync(string teamName, int count = 10)
    {
        var matches = await GetRecentMatchesAsync(count);
        return matches
            .Where(m => m.Bot1Name == teamName || m.Bot2Name == teamName)
            .ToList();
    }

    /// <summary>
    /// Get matches with specific outcome (wins, losses, draws).
    /// </summary>
    public async Task<List<RecentMatchDto>> GetMatchesByOutcomeAsync(MatchOutcome outcome, int count = 20)
    {
        var matches = await GetRecentMatchesAsync(count);
        return matches
            .Where(m => m.Outcome == outcome)
            .ToList();
    }

    /// <summary>
    /// Check if match feed has data.
    /// </summary>
    public async Task<bool> HasMatchesAsync()
    {
        var matches = await GetRecentMatchesAsync(1);
        return matches.Any();
    }

    /// <summary>
    /// Get match statistics summary.
    /// </summary>
    public async Task<MatchFeedStats> GetMatchStatsAsync()
    {
        var matches = await GetRecentMatchesAsync(100);
        
        if (!matches.Any())
        {
            return new MatchFeedStats { TotalMatches = 0 };
        }

        var now = DateTime.UtcNow;
        var oldestMatch = matches.Min(m => m.CompletedAt);
        var averageDuration = now - oldestMatch;

        return new MatchFeedStats
        {
            TotalMatches = matches.Count,
            Draws = matches.Count(m => m.Outcome == MatchOutcome.Draw),
            Player1Wins = matches.Count(m => m.Outcome == MatchOutcome.Player1Wins),
            Player2Wins = matches.Count(m => m.Outcome == MatchOutcome.Player2Wins),
            MostRecentAt = matches.First().CompletedAt,
            OldestAt = oldestMatch
        };
    }

    /// <summary>
    /// Get winning/losing streaks for a team.
    /// </summary>
    public async Task<(int wins, int losses)> GetTeamStreakAsync(string teamName, int maxMatches = 20)
    {
        var matches = await GetMatchesForTeamAsync(teamName, maxMatches);
        
        if (!matches.Any())
            return (wins: 0, losses: 0);

        int currentWinStreak = 0;
        int currentLossStreak = 0;

        foreach (var match in matches)
        {
            var isWin = match.WinnerName == teamName;
            
            if (isWin)
            {
                currentWinStreak++;
                currentLossStreak = 0;
            }
            else if (match.Outcome != MatchOutcome.Draw)
            {
                currentLossStreak++;
                currentWinStreak = 0;
            }
        }

        return (wins: currentWinStreak, losses: currentLossStreak);
    }

    /// <summary>
    /// Get match by ID.
    /// </summary>
    public async Task<RecentMatchDto?> GetMatchByIdAsync(string matchId)
    {
        var matches = await GetRecentMatchesAsync(100);
        return matches.FirstOrDefault(m => m.MatchId == matchId);
    }
}

/// <summary>
/// Match feed statistics summary.
/// </summary>
public class MatchFeedStats
{
    public int TotalMatches { get; set; }
    public int Player1Wins { get; set; }
    public int Player2Wins { get; set; }
    public int Draws { get; set; }
    public DateTime MostRecentAt { get; set; }
    public DateTime OldestAt { get; set; }
}
