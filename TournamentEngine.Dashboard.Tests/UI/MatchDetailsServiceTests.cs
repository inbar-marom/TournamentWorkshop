using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class MatchDetailsServiceTests
{
    private Mock<StateManagerService> _mockStateManager;
    private Mock<MatchFeedService> _mockMatchFeed;
    private Mock<ILogger<StateManagerService>> _mockLogger;
    private Mock<ILogger<MatchFeedService>> _mockMatchFeedLogger;

    public MatchDetailsServiceTests()
    {
        _mockLogger = new Mock<ILogger<StateManagerService>>();
        _mockMatchFeedLogger = new Mock<ILogger<MatchFeedService>>();
        _mockStateManager = new Mock<StateManagerService>(_mockLogger.Object);
        _mockMatchFeed = new Mock<MatchFeedService>(_mockStateManager.Object);
        
        // Default setup for match feed
        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RecentMatchDto>());
        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RecentMatchDto>());
        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((RecentMatchDto?)null);
    }

    [Fact]
    public async Task GetMatchDetails_WithValidMatchId_ReturnsCompleteMatchInfo()
    {
        // Arrange
        var matchId = "match-123";
        var match = new RecentMatchDto
        {
            MatchId = matchId,
            Bot1Name = "Team A",
            Bot2Name = "Team B",
            WinnerName = "Team A",
            Outcome = MatchOutcome.Player1Wins,
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        };

        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(matchId)).ReturnsAsync(match);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchDetailsAsync(matchId);

        // Assert
        result.Should().NotBeNull();
        result.MatchId.Should().Be(matchId);
        result.Bot1.Should().Be("Team A");
        result.Bot2.Should().Be("Team B");
        result.Winner.Should().Be("Team A");
    }

    [Fact]
    public async Task MatchDetails_ContainsAllRequiredFields()
    {
        // Arrange
        var matchId = "match-123";
        var match = new RecentMatchDto
        {
            MatchId = matchId,
            Bot1Name = "Team A",
            Bot2Name = "Team B",
            WinnerName = "Team A",
            Outcome = MatchOutcome.Player1Wins,
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        };

        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(matchId)).ReturnsAsync(match);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchDetailsAsync(matchId);

        // Assert
        result.MatchId.Should().NotBeEmpty();
        result.Bot1.Should().NotBeNullOrEmpty();
        result.Bot2.Should().NotBeNullOrEmpty();
        result.Winner.Should().NotBeNullOrEmpty();
        result.CompletedAtFormatted.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMatchDetails_WithDrawOutcome_ShowsCorrectOutcome()
    {
        // Arrange
        var matchId = "match-123";
        var match = new RecentMatchDto
        {
            MatchId = matchId,
            Bot1Name = "Team A",
            Bot2Name = "Team B",
            Outcome = MatchOutcome.Draw,
            WinnerName = null,
            CompletedAt = DateTime.UtcNow
        };

        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(matchId)).ReturnsAsync(match);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchDetailsAsync(matchId);

        // Assert
        result.Outcome.Should().Be(MatchOutcome.Draw);
        result.Winner.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchStatistics_ReturnsWinLossRatioForTeam()
    {
        // Arrange
        var teamName = "Team A";
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A", Outcome = MatchOutcome.Player1Wins },
            new() { Bot1Name = "Team A", Bot2Name = "Team C", WinnerName = "Team C", Outcome = MatchOutcome.Player2Wins },
            new() { Bot1Name = "Team B", Bot2Name = "Team A", WinnerName = "Team A", Outcome = MatchOutcome.Player2Wins },
            new() { Bot1Name = "Team A", Bot2Name = "Team D", Outcome = MatchOutcome.Draw }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(teamName, It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTeamMatchStatisticsAsync(teamName);

        // Assert
        result.Should().NotBeNull();
        result.TotalMatches.Should().Be(4);
        result.Wins.Should().Be(2);
        result.Losses.Should().Be(1);
        result.Draws.Should().Be(1);
    }

    [Fact]
    public async Task MatchStatistics_CalculatesWinPercentageCorrectly()
    {
        // Arrange
        var teamName = "Team A";
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", WinnerName = "Team A", Outcome = MatchOutcome.Player1Wins },
            new() { Bot1Name = "Team A", WinnerName = "Team A", Outcome = MatchOutcome.Player1Wins },
            new() { Bot1Name = "Team A", WinnerName = "Other", Outcome = MatchOutcome.Player2Wins }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(teamName, It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTeamMatchStatisticsAsync(teamName);

        // Assert
        result.WinPercentage.Should().BeApproximately(66.67, 0.01);
    }

    [Fact]
    public async Task GetRecentMatchesForTeam_ReturnsLastNMatches()
    {
        // Arrange
        var teamName = "Team A";
        var now = DateTime.UtcNow;
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", CompletedAt = now.AddMinutes(-5) },
            new() { Bot1Name = "Team A", CompletedAt = now.AddMinutes(-10) },
            new() { Bot1Name = "Team A", CompletedAt = now.AddMinutes(-15) }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(teamName, 10)).ReturnsAsync(matches);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetRecentMatchesForTeamAsync(teamName, 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].CompletedAt.Should().BeAfter(result[1].CompletedAt);
    }

    [Fact]
    public async Task GetHeadToHeadHistory_ReturnsAllMatchesBetweenTeams()
    {
        // Arrange
        var team1 = "Team A";
        var team2 = "Team B";
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A" },
            new() { Bot1Name = "Team B", Bot2Name = "Team A", WinnerName = "Team B" },
            new() { Bot1Name = "Team A", Bot2Name = "Team B", Outcome = MatchOutcome.Draw }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(team1, It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetHeadToHeadHistoryAsync(team1, team2);

        // Assert
        result.Should().NotBeNull();
        result.Team1Wins.Should().BeGreaterThanOrEqualTo(0);
        result.Team2Wins.Should().BeGreaterThanOrEqualTo(0);
        result.Draws.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task MatchDetails_WithNullMatch_ReturnsNull()
    {
        // Arrange
        var matchId = "match-999";
        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(matchId)).ReturnsAsync((RecentMatchDto)null);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchDetailsAsync(matchId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchStatistics_WithNoMatches_ReturnsZeroStats()
    {
        // Arrange
        var teamName = "Team Z";
        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync(teamName, It.IsAny<int>())).ReturnsAsync(new List<RecentMatchDto>());

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTeamMatchStatisticsAsync(teamName);

        // Assert
        result.TotalMatches.Should().Be(0);
        result.Wins.Should().Be(0);
        result.Losses.Should().Be(0);
        result.WinPercentage.Should().Be(0);
    }

    [Fact]
    public async Task CompletedAtFormatted_IsRelativeTime()
    {
        // Arrange
        var matchId = "match-456";
        var now = DateTime.UtcNow;
        var match = new RecentMatchDto
        {
            MatchId = matchId,
            Bot1Name = "Team A",
            Bot2Name = "Team B",
            WinnerName = "Team A",
            CompletedAt = now.AddMinutes(-5)
        };

        _mockMatchFeed.Setup(s => s.GetMatchByIdAsync(matchId)).ReturnsAsync(match);

        // Act
        var service = new MatchDetailsService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetMatchDetailsAsync(matchId);

        // Assert
        result.CompletedAtFormatted.Should().Contain("ago");
    }
}
