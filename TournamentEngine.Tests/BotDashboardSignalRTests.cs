using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Api.Models;
using TournamentEngine.Dashboard.Hubs;

namespace TournamentEngine.Tests;

[TestClass]
public class BotDashboardSignalRTests
{
    private Mock<IHubCallerClients>? _mockClients;
    private Mock<IClientProxy>? _mockClientProxy;
    private Mock<ILogger<TournamentHub>>? _mockLogger;
    private TournamentHub? _hub;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger<TournamentHub>>();

        _hub = new TournamentHub(_mockLogger.Object)
        {
            Clients = _mockClients.Object
        };

        // Setup default client proxy to return completed task
        _mockClientProxy!
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        _mockClients!
            .Setup(c => c.All)
            .Returns(_mockClientProxy.Object);
    }

    [TestMethod]
    public async Task BroadcastBotSubmitted_SendsEventToAllClients()
    {
        // Arrange
        var botDto = new BotDashboardDto
        {
            TeamName = "TestTeam",
            SubmissionTime = DateTime.UtcNow,
            Status = ValidationStatus.Pending,
            FileCount = 2,
            TotalSizeBytes = 1024
        };

        // Act
        await _hub!.BroadcastBotSubmitted(botDto);

        // Assert
        _mockClientProxy!.Verify(
            c => c.SendCoreAsync("BotSubmitted", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastBotValidated_SendsEventToAllClients()
    {
        // Arrange
        var botDto = new BotDashboardDto
        {
            TeamName = "TestTeam",
            Status = ValidationStatus.Valid,
            SubmissionTime = DateTime.UtcNow
        };

        // Act
        await _hub!.BroadcastBotValidated(botDto);

        // Assert
        _mockClientProxy!.Verify(
            c => c.SendCoreAsync("BotValidated", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastBotDeleted_SendsEventToAllClients()
    {
        // Arrange
        var teamName = "DeletedTeam";

        // Act
        await _hub!.BroadcastBotDeleted(teamName);

        // Assert
        _mockClientProxy!.Verify(
            c => c.SendCoreAsync("BotDeleted", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastBotListUpdated_SendsEventToAllClients()
    {
        // Arrange
        var bots = new List<BotDashboardDto>
        {
            new BotDashboardDto { TeamName = "Team1", Status = ValidationStatus.Valid },
            new BotDashboardDto { TeamName = "Team2", Status = ValidationStatus.Invalid }
        };

        // Act
        await _hub!.BroadcastBotListUpdated(bots);

        // Assert
        _mockClientProxy!.Verify(
            c => c.SendCoreAsync("BotListUpdated", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastValidationProgress_SendsProgressEventToAllClients()
    {
        // Arrange
        var teamName = "ValidatingTeam";
        var message = "Validating bot compilation...";

        // Act
        await _hub!.BroadcastValidationProgress(teamName, message);

        // Assert
        _mockClientProxy!.Verify(
            c => c.SendCoreAsync("ValidationProgress", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastBotSubmitted_WithNullBot_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await _hub!.BroadcastBotSubmitted(null!)
        );
    }

    [TestMethod]
    public async Task BroadcastBotValidated_WithNullBot_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await _hub!.BroadcastBotValidated(null!)
        );
    }

    [TestMethod]
    public async Task BroadcastBotDeleted_WithNullTeamName_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await _hub!.BroadcastBotDeleted(null!)
        );
    }
}
