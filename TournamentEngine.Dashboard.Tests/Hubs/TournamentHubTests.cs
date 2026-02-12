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
        _hub = new TournamentHub(_mockStateManager.Object, hubLogger)
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
        var expectedState = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress,
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
        var expectedState = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted
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
        var expectedState = new TournamentStateDto
        {
            TournamentId = "test-tournament",
            Status = TournamentStatus.InProgress
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
}
