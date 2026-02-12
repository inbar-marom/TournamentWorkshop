using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;
using Xunit;

namespace TournamentEngine.Dashboard.Tests.UI;

public class SeriesDashboardViewServiceTests
{
    private readonly Mock<StateManagerService> _mockStateManager;
    private readonly Mock<ILogger<StateManagerService>> _mockLogger;

    public SeriesDashboardViewServiceTests()
    {
        _mockLogger = new Mock<ILogger<StateManagerService>>();
        _mockStateManager = new Mock<StateManagerService>(_mockLogger.Object);
    }

    [Fact]
    public async Task BuildSeriesViewAsync_WithSeriesState_BuildsPanels()
    {
        // Arrange
        var seriesState = new SeriesStateDto
        {
            SeriesId = "series-1",
            SeriesName = "Local Series",
            TotalSteps = 3,
            CurrentStepIndex = 2,
            Status = SeriesStatus.InProgress,
            Steps = new List<SeriesStepDto>
            {
                new()
                {
                    StepIndex = 1,
                    GameType = GameType.RPSLS,
                    Status = SeriesStepStatus.Completed,
                    WinnerName = "TeamA"
                },
                new()
                {
                    StepIndex = 2,
                    GameType = GameType.ColonelBlotto,
                    Status = SeriesStepStatus.Running
                },
                new()
                {
                    StepIndex = 3,
                    GameType = GameType.PenaltyKicks,
                    Status = SeriesStepStatus.Pending
                }
            }
        };

        var state = new TournamentStateDto
        {
            TournamentId = "t-1",
            TournamentName = "Colonel Blotto Tournament #2",
            Message = "Tournament in progress",
            SeriesState = seriesState
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);
        var service = new SeriesDashboardViewService(_mockStateManager.Object);

        // Act
        var view = await service.BuildSeriesViewAsync();

        // Assert
        view.SeriesTitle.Should().Be("Local Series");
        view.SeriesStatus.Should().Be(SeriesStatus.InProgress);
        view.TotalSteps.Should().Be(3);
        view.CurrentStepIndex.Should().Be(2);
        view.CurrentGameType.Should().Be(GameType.ColonelBlotto);
        view.CurrentTournamentName.Should().Be("Colonel Blotto Tournament #2");
        view.StatusMessage.Should().Be("Tournament in progress");

        view.StepTrack.Should().HaveCount(3);
        view.StepTrack[0].Status.Should().Be(SeriesStepStatus.Completed);
        view.StepTrack[1].Status.Should().Be(SeriesStepStatus.Running);
        view.StepTrack[2].Status.Should().Be(SeriesStepStatus.Pending);

        view.Winners.Should().HaveCount(1);
        view.Winners[0].StepIndex.Should().Be(1);
        view.Winners[0].WinnerName.Should().Be("TeamA");

        view.UpNext.Should().HaveCount(1);
        view.UpNext[0].StepIndex.Should().Be(3);
        view.UpNext[0].GameType.Should().Be(GameType.PenaltyKicks);
    }

    [Fact]
    public async Task BuildSeriesViewAsync_WithoutSeriesState_UsesDefaults()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            TournamentName = "Standalone Tournament",
            Message = "No series running"
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);
        var service = new SeriesDashboardViewService(_mockStateManager.Object);

        // Act
        var view = await service.BuildSeriesViewAsync();

        // Assert
        view.SeriesTitle.Should().Be("Tournament Series");
        view.SeriesStatus.Should().Be(SeriesStatus.NotStarted);
        view.TotalSteps.Should().Be(0);
        view.CurrentStepIndex.Should().Be(0);
        view.StepTrack.Should().BeEmpty();
        view.Winners.Should().BeEmpty();
        view.UpNext.Should().BeEmpty();
    }
}
