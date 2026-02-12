using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Dashboard.Controllers;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Tests.Controllers;

public class TournamentApiControllerTests
{
    private readonly Mock<StateManagerService> _mockStateManager;
    private readonly SeriesDashboardViewService _seriesDashboard;
    private readonly TournamentApiController _controller;

    public TournamentApiControllerTests()
    {
        var mockLogger = Mock.Of<ILogger<StateManagerService>>();
        _mockStateManager = new Mock<StateManagerService>(mockLogger);
        _seriesDashboard = new SeriesDashboardViewService(_mockStateManager.Object);
        var controllerLogger = Mock.Of<ILogger<TournamentApiController>>();
        _controller = new TournamentApiController(_mockStateManager.Object, _seriesDashboard, controllerLogger);
    }

    [Fact]
    public async Task GetCurrentState_ReturnsOkWithState()
    {
        // Arrange
        var expectedState = new TournamentStateDto
        {
            TournamentId = "tournament-123",
            Status = TournamentStatus.InProgress,
            Message = "Round 1 in progress"
        };
        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(expectedState);

        // Act
        var result = await _controller.GetCurrentState();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeEquivalentTo(expectedState);
    }

    [Fact]
    public void GetRecentMatches_WithCount_ReturnsRequestedMatches()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { MatchId = "match-1", Bot1Name = "Bot1", Bot2Name = "Bot2" },
            new() { MatchId = "match-2", Bot1Name = "Bot3", Bot2Name = "Bot4" }
        };
        _mockStateManager
            .Setup(x => x.GetRecentMatches(2))
            .Returns(matches);

        // Act
        var result = _controller.GetRecentMatches(2);

        // Assert
        var okResult = result.Result as OkObjectResult; okResult.Should().NotBeNull();
        
        var returnedMatches = okResult.Value as List<RecentMatchDto>;
        returnedMatches.Should().HaveCount(2);
        returnedMatches.Should().BeEquivalentTo(matches);
    }

    [Fact]
    public void GetRecentMatches_DefaultCount_Returns20()
    {
        // Arrange
        var matches = Enumerable.Range(0, 10)
            .Select(i => new RecentMatchDto { MatchId = $"match-{i}" })
            .ToList();
        _mockStateManager
            .Setup(x => x.GetRecentMatches(20))
            .Returns(matches);

        // Act
        var result = _controller.GetRecentMatches();

        // Assert
        _mockStateManager.Verify(x => x.GetRecentMatches(20), Times.Once);
        var okResult = result.Result as OkObjectResult;
        var returnedMatches = okResult?.Value as List<RecentMatchDto>;
        returnedMatches.Should().HaveCount(10);
    }

    [Fact]
    public void GetHealth_ReturnsOkWithHealthyStatus()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        
        // Use reflection to check properties on the anonymous object
        var val = okResult!.Value;
        val.Should().NotBeNull();
        
        var statusProp = val!.GetType().GetProperty("Status");
        statusProp.Should().NotBeNull();
        statusProp!.GetValue(val).Should().Be("Healthy");
    }

    [Fact]
    public async Task GetSeriesView_ReturnsOkWithViewModel()
    {
        // Arrange
        var seriesState = new SeriesStateDto
        {
            SeriesId = "series-1",
            SeriesName = "Spring Series",
            TotalSteps = 2,
            CurrentStepIndex = 1,
            Status = SeriesStatus.InProgress,
            Steps = new List<SeriesStepDto>
            {
                new()
                {
                    StepIndex = 1,
                    GameType = GameType.RPSLS,
                    Status = SeriesStepStatus.Running
                },
                new()
                {
                    StepIndex = 2,
                    GameType = GameType.PenaltyKicks,
                    Status = SeriesStepStatus.Pending
                }
            }
        };

        var state = new TournamentStateDto
        {
            TournamentId = "t-1",
            TournamentName = "RPSLS Tournament",
            Message = "Tournament running",
            SeriesState = seriesState
        };

        _mockStateManager.Setup(x => x.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var result = await _controller.GetSeriesView();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var view = okResult!.Value as SeriesDashboardViewDto;
        view.Should().NotBeNull();
        view!.SeriesTitle.Should().Be("Spring Series");
        view.SeriesStatus.Should().Be(SeriesStatus.InProgress);
        view.TotalSteps.Should().Be(2);
        view.CurrentStepIndex.Should().Be(1);
        view.StepTrack.Should().HaveCount(2);
    }
}
