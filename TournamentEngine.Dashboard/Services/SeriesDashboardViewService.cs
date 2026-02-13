using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

public class SeriesDashboardViewService
{
    private readonly StateManagerService _stateManager;

    public SeriesDashboardViewService(StateManagerService stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    public async Task<SeriesDashboardViewDto> BuildSeriesViewAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var tournamentState = state.TournamentState;
        var steps = tournamentState?.Steps ?? new List<EventStepDto>();

        var view = new SeriesDashboardViewDto
        {
            SeriesTitle = tournamentState?.TournamentName ?? "Tournament Series",
            SeriesStatus = tournamentState?.Status ?? TournamentStatus.NotStarted,
            TotalSteps = tournamentState?.TotalSteps ?? 0,
            CurrentStepIndex = tournamentState?.CurrentStepIndex ?? 0,
            CurrentTournamentName = state.TournamentName,
            CurrentTournamentId = state.TournamentId,
            StatusMessage = state.Message
        };

        var orderedSteps = steps.OrderBy(s => s.StepIndex).ToList();
        var runningStep = orderedSteps.FirstOrDefault(s => s.Status == EventStepStatus.InProgress);

        view.CurrentGameType = runningStep?.GameType ?? state.CurrentEvent?.GameType;

        view.StepTrack = orderedSteps
            .Select(step => new SeriesStepTrackItemDto
            {
                StepIndex = step.StepIndex,
                Status = step.Status
            })
            .ToList();

        view.Winners = orderedSteps
            .Where(step => step.Status == EventStepStatus.Completed)
            .Select(step => new SeriesStepSummaryDto
            {
                StepIndex = step.StepIndex,
                GameType = step.GameType,
                Status = step.Status,
                WinnerName = step.WinnerName,
                TournamentName = step.EventName
            })
            .ToList();

        view.UpNext = orderedSteps
            .Where(step => step.Status == EventStepStatus.NotStarted)
            .Select(step => new SeriesStepSummaryDto
            {
                StepIndex = step.StepIndex,
                GameType = step.GameType,
                Status = step.Status
            })
            .ToList();

        // Add overall leaderboard data from current state
        view.OverallLeaderboard = state.OverallLeaderboard;

        return view;
    }
}

public class SeriesDashboardViewDto
{
    public string SeriesTitle { get; set; } = string.Empty;
    public TournamentStatus SeriesStatus { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public GameType? CurrentGameType { get; set; }
    public string? CurrentTournamentName { get; set; }
    public string? CurrentTournamentId { get; set; }
    public string? StatusMessage { get; set; }
    public List<SeriesStepTrackItemDto> StepTrack { get; set; } = new();
    public List<SeriesStepSummaryDto> Winners { get; set; } = new();
    public List<SeriesStepSummaryDto> UpNext { get; set; } = new();
    public List<TeamStandingDto>? OverallLeaderboard { get; set; }
}

public class SeriesStepTrackItemDto
{
    public int StepIndex { get; set; }
    public EventStepStatus Status { get; set; }
}

public class SeriesStepSummaryDto
{
    public int StepIndex { get; set; }
    public GameType GameType { get; set; }
    public EventStepStatus Status { get; set; }
    public string? WinnerName { get; set; }
    public string? TournamentName { get; set; }
}
