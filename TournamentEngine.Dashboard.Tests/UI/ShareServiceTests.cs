using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class ShareServiceTests
{
    private Mock<StateManagerService> _mockStateManager;
    private Mock<ILogger<StateManagerService>> _mockStateLogger;
    private Mock<ILogger<ShareService>> _mockLogger;

    public ShareServiceTests()
    {
        _mockStateLogger = new Mock<ILogger<StateManagerService>>();
        _mockLogger = new Mock<ILogger<ShareService>>();
        _mockStateManager = new Mock<StateManagerService>(_mockStateLogger.Object);
    }

    [Fact]
    public async Task GenerateShareLink_ReturnsUniqueLink()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);

        // Act
        var result = await service.GenerateShareLinkAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().StartWith("http");
        result.Should().Contain("share");
    }

    [Fact]
    public async Task GenerateShareLink_WithCustomMessage_IncludesMessage()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);

        // Act
        var result = await service.GenerateShareLinkAsync("Check out this tournament!");

        // Assert
        result.Should().Contain("Check%20out%20this%20tournament"); // URL encoded
    }

    [Fact]
    public async Task CreateSnapshot_GeneratesUniqueId()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", Rank = 1 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);

        // Act
        var snapshot1 = await service.CreateSnapshotAsync();
        var snapshot2 = await service.CreateSnapshotAsync();

        // Assert
        snapshot1.Id.Should().NotBe(snapshot2.Id);
        snapshot1.Id.Should().HaveLength(8); // Short ID
    }

    [Fact]
    public async Task CreateSnapshot_CapturesCurrentState()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress,
            Message = "Round 5",
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team Alpha", Rank = 1, TotalPoints = 25 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);

        // Act
        var snapshot = await service.CreateSnapshotAsync();

        // Assert
        snapshot.State.Should().NotBeNull();
        snapshot.State.Message.Should().Be("Round 5");
        snapshot.State.OverallLeaderboard.Should().HaveCount(1);
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetSnapshot_RetrievesSavedSnapshot()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        var snapshot = await service.CreateSnapshotAsync();

        // Act
        var retrieved = await service.GetSnapshotAsync(snapshot.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(snapshot.Id);
        retrieved.CreatedAt.Should().Be(snapshot.CreatedAt);
    }

    [Fact]
    public async Task GetSnapshot_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);

        // Act
        var result = await service.GetSnapshotAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListSnapshots_ReturnsAllSnapshots()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        await service.CreateSnapshotAsync();
        await service.CreateSnapshotAsync();
        await service.CreateSnapshotAsync();

        // Act
        var snapshots = await service.ListSnapshotsAsync();

        // Assert
        snapshots.Should().HaveCount(3);
        snapshots.Should().BeInDescendingOrder(s => s.CreatedAt);
    }

    [Fact]
    public async Task DeleteSnapshot_RemovesSnapshot()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        var snapshot = await service.CreateSnapshotAsync();

        // Act
        var deleted = await service.DeleteSnapshotAsync(snapshot.Id);

        // Assert
        deleted.Should().BeTrue();

        var retrieved = await service.GetSnapshotAsync(snapshot.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GenerateEmbedCode_ReturnsHtmlIframe()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        var snapshot = await service.CreateSnapshotAsync();

        // Act
        var embedCode = await service.GenerateEmbedCodeAsync(snapshot.Id);

        // Assert
        embedCode.Should().Contain("<iframe");
        embedCode.Should().Contain(snapshot.Id);
        embedCode.Should().Contain("width");
        embedCode.Should().Contain("height");
    }

    [Fact]
    public async Task GenerateEmbedCode_WithCustomDimensions_UsesProvidedSize()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        var snapshot = await service.CreateSnapshotAsync();

        // Act
        var embedCode = await service.GenerateEmbedCodeAsync(snapshot.Id, width: 800, height: 600);

        // Assert
        embedCode.Should().Contain("width=\"800\"");
        embedCode.Should().Contain("height=\"600\"");
    }

    [Fact]
    public async Task GetShareStats_ReturnsViewCounts()
    {
        // Arrange
        var state = new TournamentStateDto();
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ShareService(_mockStateManager.Object, _mockLogger.Object);
        var snapshot = await service.CreateSnapshotAsync();

        // Track some views
        await service.TrackViewAsync(snapshot.Id);
        await service.TrackViewAsync(snapshot.Id);
        await service.TrackViewAsync(snapshot.Id);

        // Act
        var stats = await service.GetShareStatsAsync(snapshot.Id);

        // Assert
        stats.Should().NotBeNull();
        stats.ViewCount.Should().Be(3);
    }
}
