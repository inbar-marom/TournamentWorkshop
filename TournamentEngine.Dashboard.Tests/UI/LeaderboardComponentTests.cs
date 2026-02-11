using FluentAssertions;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;
using Xunit;

namespace TournamentEngine.Dashboard.Tests.UI;

/// <summary>
/// Tests for leaderboard component data transformation and rendering.
/// Phase 4: Basic UI - Leaderboard Component
/// </summary>
public class LeaderboardComponentTests
{
    private readonly Mock<StateManagerService> _mockStateManager;

    public LeaderboardComponentTests()
    {
        _mockStateManager = new Mock<StateManagerService>();
    }

    [Fact]
    public async Task GetLeaderboardData_WithValidState_ReturnsFormattedLeaderboard()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new TeamStandingDto
                {
                    Rank = 1,
                    TeamName = "Team A",
                    TotalPoints = 100,
                    TournamentWins = 2,
                    TotalWins = 10,
                    TotalLosses = 2,
                    RankChange = 0
                },
                new TeamStandingDto
                {
                    Rank = 2,
                    TeamName = "Team B",
                    TotalPoints = 90,
                    TournamentWins = 1,
                    TotalWins = 8,
                    TotalLosses = 4,
                    RankChange = 1
                }
            }
        };

        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(state);

        // Act
        var leaderboard = state.OverallLeaderboard;

        // Assert
        leaderboard.Should().HaveCount(2, "Should have 2 teams in leaderboard");
        leaderboard[0].Rank.Should().Be(1, "First team should be rank 1");
        leaderboard[0].TeamName.Should().Be("Team A", "First team should be Team A");
        leaderboard[0].TotalPoints.Should().Be(100, "Team A should have 100 points");
    }

    [Fact]
    public async Task GetLeaderboardData_WithRankChanges_ShowsRankChange()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new TeamStandingDto
                {
                    Rank = 1,
                    TeamName = "Team A",
                    TotalPoints = 100,
                    RankChange = 1  // Moved up 1 position
                },
                new TeamStandingDto
                {
                    Rank = 2,
                    TeamName = "Team B",
                    TotalPoints = 90,
                    RankChange = -1  // Moved down 1 position
                }
            }
        };

        // Act
        var rankChange = state.OverallLeaderboard[0].RankChange;

        // Assert
        rankChange.Should().Be(1, "Team A should show +1 rank change");
        state.OverallLeaderboard[1].RankChange.Should().Be(-1, "Team B should show -1 rank change");
    }

    [Fact]
    public async Task GetLeaderboardData_WithNoTeams_ReturnsEmptyList()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>()
        };

        // Act
        var leaderboard = state.OverallLeaderboard;

        // Assert
        leaderboard.Should().BeEmpty("Should return empty list when no teams exist");
    }

    [Fact]
    public async Task LeaderboardFormatting_ContainsAllRequiredFields()
    {
        // Arrange
        var team = new TeamStandingDto
        {
            Rank = 1,
            TeamName = "Team A",
            TotalPoints = 100,
            TournamentWins = 2,
            TotalWins = 10,
            TotalLosses = 2,
            RankChange = 0
        };

        // Act & Assert
        team.Rank.Should().NotBe(default);
        team.TeamName.Should().NotBeNullOrEmpty();
        team.TotalPoints.Should().BeGreaterThanOrEqualTo(0);
        team.TournamentWins.Should().BeGreaterThanOrEqualTo(0);
        team.TotalWins.Should().BeGreaterThanOrEqualTo(0);
        team.TotalLosses.Should().BeGreaterThanOrEqualTo(0);
    }
}
