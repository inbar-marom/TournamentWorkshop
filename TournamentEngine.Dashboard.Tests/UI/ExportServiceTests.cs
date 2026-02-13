using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class ExportServiceTests
{
    private Mock<StateManagerService> _mockStateManager;
    private Mock<MatchFeedService> _mockMatchFeed;
    private Mock<ILogger<StateManagerService>> _mockStateLogger;
    private Mock<ILogger<ExportService>> _mockLogger;

    public ExportServiceTests()
    {
        _mockStateLogger = new Mock<ILogger<StateManagerService>>();
        _mockLogger = new Mock<ILogger<ExportService>>();
        _mockStateManager = new Mock<StateManagerService>(_mockStateLogger.Object);
        _mockMatchFeed = new Mock<MatchFeedService>(_mockStateManager.Object);

        // Default setup
        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RecentMatchDto>());
    }

    [Fact]
    public async Task ExportToJson_ReturnsValidJsonString()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            Message = "Test tournament",
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", TotalPoints = 15, Rank = 1 }
            },
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportToJsonAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("Team A");
        result.Should().Contain("TotalPoints"); // JSON property name
        result.Should().Contain("15"); // Value
    }

    [Fact]
    public async Task ExportToCsv_ReturnsValidCsvFormat()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", TotalPoints = 15, TotalWins = 5, TotalLosses = 2, Rank = 1 },
                new() { TeamName = "Team B", TotalPoints = 12, TotalWins = 4, TotalLosses = 3, Rank = 2 }
            },
            TournamentState = new TournamentStateDto()
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportToCsvAsync();

        // Assert
        result.Should().Contain("Rank,TeamName,Points,Wins,Losses");
        result.Should().Contain("1,Team A,15,5,2");
        result.Should().Contain("2,Team B,12,4,3");
    }

    [Fact]
    public async Task ExportMatchHistory_IncludesAllMatches()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { MatchId = "1", Bot1Name = "Bot A", Bot2Name = "Bot B", WinnerName = "Bot A", CompletedAt = DateTime.UtcNow },
            new() { MatchId = "2", Bot1Name = "Bot C", Bot2Name = "Bot D", WinnerName = "Bot C", CompletedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportMatchHistoryAsync();

        // Assert
        result.Should().Contain("Bot A");
        result.Should().Contain("Bot C");
        result.Length.Should().BeGreaterThan(50); // JSON should be reasonably sized
    }

    [Fact]
    public async Task GetExportFormats_ReturnsAvailableFormats()
    {
        // Arrange
        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var formats = await service.GetAvailableFormatsAsync();

        // Assert
        formats.Should().Contain("json");
        formats.Should().Contain("csv");
        formats.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExportWithFormat_Json_ReturnsJsonData()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportAsync("json");

        // Assert
        result.Format.Should().Be("json");
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task ExportWithFormat_Csv_ReturnsCsvData()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", Rank = 1 }
            },
            TournamentState = new TournamentStateDto()
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportAsync("csv");

        // Assert
        result.Format.Should().Be("csv");
        result.FileName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task ExportWithInvalidFormat_ThrowsException()
    {
        // Arrange
        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync("invalid"));
    }

    [Fact]
    public async Task ExportFileName_IncludesTimestamp()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportAsync("json");

        // Assert
        result.FileName.Should().Contain("tournament");
        result.FileName.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}"); // Contains date
    }

    [Fact]
    public async Task ExportFullSnapshot_IncludesAllData()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", Rank = 1 }
            },
            CurrentEvent = new CurrentEventDto
            {
                GameType = GameType.RPSLS,
                Stage = TournamentStage.GroupStage
            },
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            }
        };

        var matches = new List<RecentMatchDto>
        {
            new() { MatchId = "1", Bot1Name = "A", Bot2Name = "B" }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);
        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportFullSnapshotAsync();

        // Assert
        result.Should().Contain("Team A");
        result.Should().Contain("RPSLS");
        result.Should().Contain("MatchId"); // Contains match data
    }

    [Fact]
    public async Task ExportMetadata_IncludesExportInfo()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto()
        };
        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        var service = new ExportService(_mockStateManager.Object, _mockMatchFeed.Object, _mockLogger.Object);

        // Act
        var result = await service.ExportAsync("json");

        // Assert
        result.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Version.Should().NotBeEmpty();
    }
}
