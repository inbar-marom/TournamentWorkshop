using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using System.Net.Http;
using System.Net.Http.Json;

namespace TournamentEngine.Core.Tournament;

/// <summary>
/// Sends match results to the Dashboard API via HTTP POST.
/// Allows real-time match data visibility across process boundaries.
/// </summary>
public class HttpMatchResultsLogger : IMatchResultsLogger
{
    private readonly string _dashboardApiUrl;
    private readonly HttpClient _httpClient;

    public HttpMatchResultsLogger(string dashboardApiUrl)
    {
        _dashboardApiUrl = dashboardApiUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public void StartTournamentRun(string tournamentId, GameType gameType)
    {
        // Just mark the start, no logging needed
    }

    public void AppendMatchResult(MatchResult matchResult, string groupLabel)
    {
        try
        {
            var dto = new RecentMatchDto
            {
                MatchId = Guid.NewGuid().ToString(),
                Bot1Name = matchResult.Bot1Name,
                Bot2Name = matchResult.Bot2Name,
                Bot1Score = matchResult.Bot1Score,
                Bot2Score = matchResult.Bot2Score,
                Outcome = matchResult.Outcome,
                GameType = matchResult.GameType,
                WinnerName = matchResult.WinnerName,
                EventName = matchResult.GameType.ToString(),
                GroupLabel = groupLabel,
                CompletedAt = matchResult.EndTime
            };

            // Fire and forget - don't block tournament execution on API calls
            _ = PostMatchResultAsync(dto);
        }
        catch
        {
            // Silently ignore errors - don't let API issues block tournament
        }
    }

    private async Task PostMatchResultAsync(RecentMatchDto matchResult)
    {
        try
        {
            var endpoint = $"{_dashboardApiUrl}/api/tournament-engine/match-result";
            await _httpClient.PostAsJsonAsync(endpoint, matchResult);
        }
        catch
        {
            // Silently ignore - this shouldn't block the tournament
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
