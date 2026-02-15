using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Tests.Hubs;

public class TournamentHubTests
{
    private readonly Mock<StateManagerService> _mockStateManager;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly TournamentHub _hub;

    public TournamentHubTests()
    {
        var mockLogger = Mock.Of<ILogger<StateManagerService>>();
        _mockStateManager = new Mock<StateManagerService>(mockLogger);
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();
        _mockContext = new Mock<HubCallerContext>();
        _mockGroups = new Mock<IGroupManager>();

        _mockClients.Setup(x => x.Caller).Returns(_mockClientProxy.Object);
        _mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockClients.Setup(x => x.All).Returns(_mockClientProxy.Object);

        var hubLogger = Mock.Of<ILogger<TournamentHub>>();
        _hub = new TournamentHub(_mockStateManager.Object, null, hubLogger)
        {
            Clients = _mockClients.Object,
            Context = _mockContext.Object,
            Groups = _mockGroups.Object
        };

        _mockContext.Setup(x => x.ConnectionId).Returns("test-connection-id");
    }

    [Fact]
    public async Task OnConnectedAsync_SubscribesClientAndSendsCurrentState()
    {
        // Arrange
        var expectedState = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            },
            Message = "Test state"
        };
        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(expectedState);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockStateManager.Verify(x => x.GetCurrentStateAsync(), Times.Once);
        _mockGroups.Verify(
            x => x.AddToGroupAsync("test-connection-id", "TournamentViewers", default),
            Times.Once);
        
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "CurrentState",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToUpdates_SendsConfirmation()
    {
        // Arrange
        var expectedState = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.NotStarted
            }
        };
        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(expectedState);

        // Act
        await _hub.SubscribeToUpdates();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "SubscriptionConfirmed",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentState_SendsStateToCallerViaSignalR()
    {
        // Arrange
        var expectedState = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                TournamentId = "test-tournament",
                Status = TournamentStatus.InProgress
            }
        };
        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(expectedState);

        // Act
        await _hub.GetCurrentState();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "CurrentState",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetRecentMatches_SendsMatchesToCaller()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { MatchId = "match-1", Bot1Name = "Bot1", Bot2Name = "Bot2" },
            new() { MatchId = "match-2", Bot1Name = "Bot3", Bot2Name = "Bot4" }
        };
        _mockStateManager
            .Setup(x => x.GetRecentMatches(5))
            .Returns(matches);

        // Act
        await _hub.GetRecentMatches(5);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "RecentMatches",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Ping_SendsPong()
    {
        // Act
        await _hub.Ping();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "Pong",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SeriesStarted_UpdatesStateAndBroadcasts()
    {
        // Arrange
        var seriesEvent = new TournamentStartedEventDto
        {
            TournamentId = "series-1",
            TournamentName = "Local Series",
            TotalSteps = 2,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted }
            },
            StartedAt = DateTime.UtcNow
        };

        // Act
        await _hub.SeriesStarted(seriesEvent);

        // Assert
        _mockStateManager.Verify(x => x.UpdateTournamentStartedAsync(seriesEvent), Times.Once);
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "TournamentStarted",
                It.Is<object[]>(args => args.Length == 1 && args[0] == seriesEvent),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SeriesProgressUpdated_UpdatesStateAndBroadcasts()
    {
        // Arrange
        var progressEvent = new TournamentProgressUpdatedEventDto
        {
            TournamentState = new TournamentStateDto
            {
                TournamentId = "series-1",
                TournamentName = "Local Series",
                TotalSteps = 2,
                CurrentStepIndex = 1,
                Status = TournamentStatus.InProgress,
                Steps = new List<EventStepDto>
                {
                    new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress },
                    new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted }
                }
            },
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _hub.SeriesProgressUpdated(progressEvent);

        // Assert
        _mockStateManager.Verify(x => x.UpdateTournamentProgressAsync(progressEvent), Times.Once);
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "SeriesProgressUpdated",
                It.Is<object[]>(args => args.Length == 1 && args[0] == progressEvent),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SeriesStepCompleted_UpdatesStateAndBroadcasts()
    {
        // Arrange
        var completedEvent = new EventStepCompletedDto
        {
            StepIndex = 1,
            GameType = GameType.RPSLS,
            WinnerName = "Bot1",
            TournamentId = "tournament-1",
            TournamentName = "RPSLS Tournament #1",
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _hub.EventStepCompleted(completedEvent);

        // Assert
        _mockStateManager.Verify(x => x.UpdateEventStepCompletedAsync(completedEvent), Times.Once);
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "EventStepCompleted",
                It.Is<object[]>(args => args.Length == 1 && args[0] == completedEvent),
                default),
            Times.Once);
    }

    [Fact]
    public async Task TournamentCompleted_UpdatesStateAndBroadcasts()
    {
        // Arrange
        var completedEvent = new TournamentCompletedEventDto
        {
            TournamentId = "series-1",
            TournamentName = "Local Series",
            Champion = "Bot1",
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _hub.TournamentCompleted(completedEvent);

        // Assert
        _mockStateManager.Verify(x => x.UpdateTournamentCompletedAsync(completedEvent), Times.Once);
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "TournamentCompleted",
                It.Is<object[]>(args => args.Length == 1 && args[0] == completedEvent),
                default),
            Times.Once);
    }
}
