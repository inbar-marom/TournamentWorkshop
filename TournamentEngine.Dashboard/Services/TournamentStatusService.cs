using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for formatting and managing tournament status data for UI display.
/// Phase 4: Basic UI - Tournament Status Component Service
/// </summary>
public class TournamentStatusService
{
    private readonly StateManagerService _stateManager;

    public TournamentStatusService(StateManagerService stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    /// <summary>
    /// Get current event status information.
    /// </summary>
    public async Task<CurrentEventDto?> GetCurrentTournamentAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state.CurrentEvent;
    }

    /// <summary>
    /// Get progress percentage of current tournament.
    /// </summary>
    public async Task<double> GetProgressPercentageAsync()
    {
        var tournament = await GetCurrentTournamentAsync();
        
        if (tournament == null || tournament.TotalMatches == 0)
            return 0.0;

        return (tournament.MatchesCompleted * 100.0) / tournament.TotalMatches;
    }

    /// <summary>
    /// Get tournament progress information.
    /// </summary>
    public async Task<TournamentProgressDto?> GetSeriesProgressAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state.TournamentProgress;
    }

    /// <summary>
    /// Get all events in tournament.
    /// </summary>
    public async Task<List<EventInTournamentDto>> GetTournamentsInSeriesAsync()
    {
        var series = await GetSeriesProgressAsync();
        return series?.Events ?? new List<EventInTournamentDto>();
    }

    /// <summary>
    /// Get completed tournaments count.
    /// </summary>
    public async Task<int> GetCompletedTournamentsCountAsync()
    {
        var series = await GetSeriesProgressAsync();
        return series?.CompletedCount ?? 0;
    }

    /// <summary>
    /// Get total tournaments in series.
    /// </summary>
    public async Task<int> GetTotalTournamentsCountAsync()
    {
        var series = await GetSeriesProgressAsync();
        return series?.TotalCount ?? 0;
    }

    /// <summary>
    /// Get series completion percentage.
    /// </summary>
    public async Task<double> GetSeriesProgressPercentageAsync()
    {
        var series = await GetSeriesProgressAsync();
        
        if (series == null || series.TotalCount == 0)
            return 0.0;

        return (series.CompletedCount * 100.0) / series.TotalCount;
    }

    /// <summary>
    /// Get event by number in tournament.
    /// </summary>
    public async Task<EventInTournamentDto?> GetTournamentByNumberAsync(int tournamentNumber)
    {
        var tournaments = await GetTournamentsInSeriesAsync();
        return tournaments.FirstOrDefault(t => t.EventNumber == tournamentNumber);
    }

    /// <summary>
    /// Get current tournament number being played.
    /// </summary>
    public async Task<int> GetCurrentTournamentNumberAsync()
    {
        var series = await GetSeriesProgressAsync();
        if (series == null)
            return 1;

        return series.CurrentEventIndex + 1; // Convert 0-based index to 1-based event number
    }

    /// <summary>
    /// Check if tournament has started.
    /// </summary>
    public async Task<bool> IsTournamentStartedAsync()
    {
        var tournament = await GetCurrentTournamentAsync();
        return tournament != null && tournament.TournamentNumber > 0;
    }

    /// <summary>
    /// Check if tournament is in progress.
    /// </summary>
    public async Task<bool> IsTournamentInProgressAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state.Status == TournamentStatus.InProgress;
    }

    /// <summary>
    /// Check if tournament is completed.
    /// </summary>
    public async Task<bool> IsTournamentCompletedAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        return state.Status == TournamentStatus.Completed;
    }

    /// <summary>
    /// Get matches remaining in tournament.
    /// </summary>
    public async Task<int> GetMatchesRemainingAsync()
    {
        var tournament = await GetCurrentTournamentAsync();
        
        if (tournament == null)
            return 0;

        return tournament.TotalMatches - tournament.MatchesCompleted;
    }

    /// <summary>
    /// Get estimated time remaining based on average match duration.
    /// </summary>
    public async Task<TimeSpan?> GetEstimatedTimeRemainingAsync(TimeSpan? averageMatchDuration = null)
    {
        var tournament = await GetCurrentTournamentAsync();
        var remaining = await GetMatchesRemainingAsync();

        if (tournament == null || remaining <= 0)
            return null;

        // Default to 30 seconds per match if not provided
        var duration = averageMatchDuration ?? TimeSpan.FromSeconds(30);
        return TimeSpan.FromSeconds(remaining * duration.TotalSeconds);
    }

    /// <summary>
    /// Get tournament stage display text.
    /// </summary>
    public async Task<string> GetStageDisplayTextAsync()
    {
        var tournament = await GetCurrentTournamentAsync();
        
        if (tournament == null)
            return "Not Started";

        var stageText = tournament.Stage == TournamentStage.GroupStage ? "Group Stage" : "Finals";
        return $"{stageText} - Round {tournament.CurrentRound}/{tournament.TotalRounds}";
    }

    /// <summary>
    /// Get tournament display title.
    /// </summary>
    public async Task<string> GetTournamentTitleAsync()
    {
        var tournamentNumber = await GetCurrentTournamentNumberAsync();
        var total = await GetTotalTournamentsCountAsync();
        var tournament = await GetCurrentTournamentAsync();

        var gameType = tournament != null ? GameTypeHelper.GetDisplayName(tournament.GameType) : "Unknown";
        return $"Tournament {tournamentNumber} of {total} ({gameType})";
    }
}
