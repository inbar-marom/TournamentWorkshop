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
        var seriesState = state.SeriesState;
        var steps = seriesState?.Steps ?? new List<SeriesStepDto>();

        var view = new SeriesDashboardViewDto
        {
            SeriesTitle = seriesState?.SeriesName ?? "Tournament Series",
            SeriesStatus = seriesState?.Status ?? SeriesStatus.NotStarted,
            TotalSteps = seriesState?.TotalSteps ?? 0,
            CurrentStepIndex = seriesState?.CurrentStepIndex ?? 0,
            CurrentTournamentName = state.TournamentName,
            CurrentTournamentId = state.TournamentId,
            StatusMessage = state.Message
        };

        var orderedSteps = steps.OrderBy(s => s.StepIndex).ToList();
        var runningStep = orderedSteps.FirstOrDefault(s => s.Status == SeriesStepStatus.Running);

        view.CurrentGameType = runningStep?.GameType ?? state.CurrentTournament?.GameType;

        view.StepTrack = orderedSteps
            .Select(step => new SeriesStepTrackItemDto
            {
                StepIndex = step.StepIndex,
                Status = step.Status
            })
            .ToList();

        view.Winners = orderedSteps
            .Where(step => step.Status == SeriesStepStatus.Completed)
            .Select(step => new SeriesStepSummaryDto
            {
                StepIndex = step.StepIndex,
                GameType = step.GameType,
                Status = step.Status,
                WinnerName = step.WinnerName,
                TournamentName = step.TournamentName
            })
            .ToList();

        view.UpNext = orderedSteps
            .Where(step => step.Status == SeriesStepStatus.Pending)
            .Select(step => new SeriesStepSummaryDto
            {
                StepIndex = step.StepIndex,
                GameType = step.GameType,
                Status = step.Status
            })
            .ToList();

        return view;
    }
}

public class SeriesDashboardViewDto
{
    public string SeriesTitle { get; set; } = string.Empty;
    public SeriesStatus SeriesStatus { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public GameType? CurrentGameType { get; set; }
    public string? CurrentTournamentName { get; set; }
    public string? CurrentTournamentId { get; set; }
    public string? StatusMessage { get; set; }
    public List<SeriesStepTrackItemDto> StepTrack { get; set; } = new();
    public List<SeriesStepSummaryDto> Winners { get; set; } = new();
    public List<SeriesStepSummaryDto> UpNext { get; set; } = new();
}

public class SeriesStepTrackItemDto
{
    public int StepIndex { get; set; }
    public SeriesStepStatus Status { get; set; }
}

public class SeriesStepSummaryDto
{
    public int StepIndex { get; set; }
    public GameType GameType { get; set; }
    public SeriesStepStatus Status { get; set; }
    public string? WinnerName { get; set; }
    public string? TournamentName { get; set; }
}
