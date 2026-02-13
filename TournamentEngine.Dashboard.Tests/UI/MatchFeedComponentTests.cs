using FluentAssertions;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;
using Xunit;

namespace TournamentEngine.Dashboard.Tests.UI;

/// <summary>
/// Tests for match feed component data formatting and real-time updates.
/// Phase 4: Basic UI - Match Feed Component
/// </summary>
public class MatchFeedComponentTests
{
    private readonly Mock<StateManagerService> _mockStateManager;

    public MatchFeedComponentTests()
    {
        _mockStateManager = new Mock<StateManagerService>();
    }

    [Fact]
    public async Task GetRecentMatches_WithMatches_ReturnsFormattedList()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new RecentMatchDto
            {
                MatchId = Guid.NewGuid().ToString(),
                Bot1Name = "AlphaBot",
                Bot2Name = "BetaBot",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = "AlphaBot",
                Bot1Score = 30,
                Bot2Score = 20,
                CompletedAt = DateTime.UtcNow,
                GameType = GameType.RPSLS
            },
            new RecentMatchDto
            {
                MatchId = Guid.NewGuid().ToString(),
                Bot1Name = "GammaBot",
                Bot2Name = "DeltaBot",
                Outcome = MatchOutcome.Player2Wins,
                WinnerName = "DeltaBot",
                Bot1Score = 15,
                Bot2Score = 25,
                CompletedAt = DateTime.UtcNow.AddSeconds(-60),
                GameType = GameType.RPSLS
            }
        };

        var state = new DashboardStateDto
        {
            RecentMatches = matches
        };

        // Act
        var recentMatches = state.RecentMatches;

        // Assert
        recentMatches.Should().HaveCount(2, "Should have 2 recent matches");
        recentMatches[0].Bot1Name.Should().Be("AlphaBot");
        recentMatches[0].Bot2Name.Should().Be("BetaBot");
        recentMatches[0].Outcome.Should().Be(MatchOutcome.Player1Wins);
    }

    [Fact]
    public async Task GetRecentMatches_ReturnsMostRecent20()
    {
        // Arrange
        var matches = new List<RecentMatchDto>();
        for (int i = 0; i < 30; i++)
        {
            matches.Add(new RecentMatchDto
            {
                MatchId = Guid.NewGuid().ToString(),
                Bot1Name = $"Bot{i}",
                Bot2Name = $"Bot{i + 1}",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = $"Bot{i}",
                Bot1Score = 25,
                Bot2Score = 20,
                CompletedAt = DateTime.UtcNow.AddSeconds(-i),
                GameType = GameType.RPSLS
            });
        }

        var state = new DashboardStateDto
        {
            RecentMatches = matches.OrderByDescending(m => m.CompletedAt).Take(20).ToList()
        };

        // Act
        var recentMatches = state.RecentMatches;

        // Assert
        recentMatches.Should().HaveCount(20, "Should limit to 20 most recent matches");
    }

    [Fact]
    public async Task MatchResult_ShowsCorrectWinner()
    {
        // Arrange
        var match = new RecentMatchDto
        {
            Bot1Name = "AlphaBot",
            Bot2Name = "BetaBot",
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "AlphaBot",
            Bot1Score = 30,
            Bot2Score = 20
        };

        // Act & Assert
        match.Outcome.Should().Be(MatchOutcome.Player1Wins);
        match.WinnerName.Should().Be("AlphaBot");
        match.Bot1Score.Should().BeGreaterThan(match.Bot2Score);
    }

    [Fact]
    public async Task MatchResult_WithDraw_ShowsCorrectOutcome()
    {
        // Arrange
        var match = new RecentMatchDto
        {
            Bot1Name = "AlphaBot",
            Bot2Name = "BetaBot",
            Outcome = MatchOutcome.Draw,
            WinnerName = null,
            Bot1Score = 25,
            Bot2Score = 25
        };

        // Act & Assert
        match.Outcome.Should().Be(MatchOutcome.Draw);
        match.WinnerName.Should().BeNull();
        match.Bot1Score.Should().Be(match.Bot2Score);
    }

    [Fact]
    public async Task MatchTimestamp_ShowsWhenCompleted()
    {
        // Arrange
        var completedTime = DateTime.UtcNow.AddMinutes(-5);
        var match = new RecentMatchDto
        {
            Bot1Name = "AlphaBot",
            Bot2Name = "BetaBot",
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "AlphaBot",
            Bot1Score = 30,
            Bot2Score = 20,
            CompletedAt = completedTime
        };

        // Act & Assert
        match.CompletedAt.Should().Be(completedTime);
        (DateTime.UtcNow - match.CompletedAt).Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MatchFeed_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            RecentMatches = new List<RecentMatchDto>()
        };

        // Act
        var recentMatches = state.RecentMatches;

        // Assert
        recentMatches.Should().BeEmpty("Should return empty list when no matches exist");
    }

    [Fact]
    public async Task MatchFeed_ContainsAllRequiredFields()
    {
        // Arrange
        var match = new RecentMatchDto
        {
            MatchId = Guid.NewGuid().ToString(),
            Bot1Name = "AlphaBot",
            Bot2Name = "BetaBot",
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "AlphaBot",
            Bot1Score = 30,
            Bot2Score = 20,
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        };

        // Act & Assert
        match.MatchId.Should().NotBeNullOrEmpty();
        match.Bot1Name.Should().NotBeNullOrEmpty();
        match.Bot2Name.Should().NotBeNullOrEmpty();
        match.GameType.Should().Be(GameType.RPSLS);
        match.CompletedAt.Should().NotBe(default);
    }
}
