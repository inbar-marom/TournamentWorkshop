using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class ChartsServiceTests
{
    private Mock<StateManagerService> _mockStateManager;
    private Mock<MatchFeedService> _mockMatchFeed;
    private Mock<ILogger<StateManagerService>> _mockLogger;
    private Mock<ILogger<MatchFeedService>> _mockMatchFeedLogger;

    public ChartsServiceTests()
    {
        _mockLogger = new Mock<ILogger<StateManagerService>>();
        _mockMatchFeedLogger = new Mock<ILogger<MatchFeedService>>();
        _mockStateManager = new Mock<StateManagerService>(_mockLogger.Object);
        _mockMatchFeed = new Mock<MatchFeedService>(_mockStateManager.Object);
        
        // Default setup for match feed
        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RecentMatchDto>());
    }

    [Fact]
    public async Task GetWinRatioChartData_WithValidMatches_ReturnsFormattedData()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", TotalWins = 5, TotalLosses = 2 },
                new() { TeamName = "Team B", TotalWins = 4, TotalLosses = 3 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetWinRatioChartDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.TeamNames.Should().HaveCount(2);
        result.WinCounts.Should().HaveCount(2);
        result.LossCounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task WinRatioChartData_ContainsCorrectPercentages()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", TotalWins = 6, TotalLosses = 4 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetWinRatioChartDataAsync();

        // Assert
        result.WinPercentages.Should().HaveCount(1);
        result.WinPercentages[0].Should().Be(60.0);
    }

    [Fact]
    public async Task GetMatchHistoryTrendData_ReturnsChartSeriesOverTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A", CompletedAt = now.AddHours(-2) },
            new() { Bot1Name = "Team A", Bot2Name = "Team C", WinnerName = "Team A", CompletedAt = now.AddHours(-1) }
        };

        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchHistoryTrendDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.TimeLabels.Should().NotBeEmpty();
        result.TotalMatchesByTime.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGameTypeDistributionChartData_ShowsMatchCountByGameType()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { MatchId = "1", Bot1Name = "Bot1", Bot2Name = "Bot2", GameType = GameType.RPSLS, CompletedAt = DateTime.UtcNow },
            new() { MatchId = "2", Bot1Name = "Bot3", Bot2Name = "Bot4", GameType = GameType.RPSLS, CompletedAt = DateTime.UtcNow.AddMinutes(-1) },
            new() { MatchId = "3", Bot1Name = "Bot1", Bot2Name = "Bot3", GameType = GameType.RPSLS, CompletedAt = DateTime.UtcNow.AddMinutes(-2) }
        };

        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                GameType = GameType.RPSLS
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);
        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetGameTypeDistributionChartDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.GameTypeLabels.Should().NotBeEmpty();
        result.MatchCounts.Should().NotBeEmpty();
        result.GameTypeLabels.Should().Contain("RPSLS");
        result.MatchCounts.Should().Contain(3);
    }

    [Fact]
    public async Task GetPointsDistributionChart_ShowsTeamPointsDistribution()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", TotalPoints = 15 },
                new() { TeamName = "Team B", TotalPoints = 12 },
                new() { TeamName = "Team C", TotalPoints = 18 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetPointsDistributionChartDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.TeamNames.Should().HaveCount(3);
        result.Points.Should().HaveCount(3);
        // Should be sorted by points descending
        result.Points.Should().Equal(18, 15, 12);
        result.TeamNames.Should().Equal("Team C", "Team A", "Team B");
    }

    [Fact]
    public async Task ChartData_IsOrderedByValueDescending()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team C", TotalPoints = 5 },
                new() { TeamName = "Team A", TotalPoints = 20 },
                new() { TeamName = "Team B", TotalPoints = 15 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetPointsDistributionChartDataAsync();

        // Assert
        result.Points[0].Should().Be(20);
        result.Points[1].Should().Be(15);
        result.Points[2].Should().Be(5);
    }

    [Fact]
    public async Task GetMatchOutcomeDistributionChart_ShowsWinDrawLossCounts()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { WinnerName = "Team A", Outcome = MatchOutcome.Player1Wins },
            new() { WinnerName = "Team A", Outcome = MatchOutcome.Player1Wins },
            new() { WinnerName = "Team B", Outcome = MatchOutcome.Player2Wins },
            new() { Outcome = MatchOutcome.Draw }
        };

        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchOutcomeDistributionChartDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.OutcomeLabels.Should().Contain("Wins");
        result.OutcomeCounts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTeamHeadToHeadChart_ShowsMatchupBetweenTeams()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A" },
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team B" },
            new() { Bot1Name = "Team B", Bot2Name = "Team A", WinnerName = "Team A" }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync("Team A", It.IsAny<int>())).ReturnsAsync(
            matches.Where(m => m.Bot1Name == "Team A" || m.Bot2Name == "Team A").ToList());

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTeamHeadToHeadChartAsync("Team A", "Team B");

        // Assert
        result.Should().NotBeNull();
        result.TeamAWins.Should().BeGreaterThanOrEqualTo(0);
        result.TeamBWins.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ChartData_WithNoData_ReturnsEmptyChartData()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>()
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new ChartsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetPointsDistributionChartDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.TeamNames.Should().BeEmpty();
        result.Points.Should().BeEmpty();
    }
}
