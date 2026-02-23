using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Common;
using DashboardCommon = TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service that provides access to the running tournament engine's current state.
/// Bridges the gap between the tournament execution and the dashboard display.
/// </summary>
public class TournamentEngineQueryService
{
    private readonly ITournamentManager? _tournamentManager;
    private readonly TournamentSeriesManager? _seriesManager;
    private readonly ILogger<TournamentEngineQueryService> _logger;

    public TournamentEngineQueryService(
        ITournamentManager? tournamentManager,
        TournamentSeriesManager? seriesManager,
        ILogger<TournamentEngineQueryService> logger)
    {
        _tournamentManager = tournamentManager;
        _seriesManager = seriesManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all matches from the currently running tournament.
    /// </summary>
    public async Task<List<RecentMatchDto>> GetAllMatchesAsync()
    {
        try
        {
            // If tournament is running in-process, try to get matches from the series manager
            if (_seriesManager != null)
            {
                return await _seriesManager.GetAllMatchesAsync();
            }

            _logger.LogWarning("No tournament series manager available - matches cannot be retrieved from running tournament");
            return new List<RecentMatchDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving matches from tournament engine");
            return new List<RecentMatchDto>();
        }
    }

    /// <summary>
    /// Get matches filtered by event (game type).
    /// </summary>
    public async Task<List<RecentMatchDto>> GetMatchesByEventAsync(string eventName)
    {
        var allMatches = await GetAllMatchesAsync();
        return allMatches
            .Where(m => (m.EventName ?? "").Equals(eventName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get current tournament state summary.
    /// </summary>
    public async Task<DashboardStateDto> GetCurrentTournamentStateAsync()
    {
        try
        {
            if (_seriesManager != null)
            {
                return await _seriesManager.GetDashboardStateAsync();
            }

            _logger.LogWarning("No tournament series manager available - cannot retrieve tournament state");
            return new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = "No tournament running - dashboard in standby mode"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournament state from engine");
            return new DashboardStateDto
            {
                Status = TournamentStatus.NotStarted,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get group standings for a specific event.
    /// </summary>
    public async Task<List<GroupDto>> GetGroupStandingsByEventAsync(string eventName)
    {
        try
        {
            if (_seriesManager != null)
            {
                return await _seriesManager.GetGroupStandingsByEventAsync(eventName);
            }

            return new List<GroupDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group standings from tournament engine");
            return new List<GroupDto>();
        }
    }

    /// <summary>
    /// Check if a tournament is currently running.
    /// </summary>
    public bool IsTournamentRunning => _seriesManager != null || _tournamentManager != null;
}
