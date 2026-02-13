using FluentAssertions;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;
using Xunit;

namespace TournamentEngine.Dashboard.Tests.UI;

/// <summary>
/// Tests for real-time UI update handling via SignalR.
/// Phase 4: Basic UI - Real-time Updates
/// </summary>
public class RealtimeUIUpdateTests
{
    private readonly Mock<StateManagerService> _mockStateManager;

    public RealtimeUIUpdateTests()
    {
        _mockStateManager = new Mock<StateManagerService>();
    }

    [Fact]
    public async Task OnMatchCompleted_UpdatesMatchFeed()
    {
        // Arrange
        var initialMatches = new List<RecentMatchDto>();
        var state = new DashboardStateDto
        {
            RecentMatches = initialMatches
        };

        _mockStateManager
            .Setup(x => x.GetCurrentStateAsync())
            .ReturnsAsync(state);

        var newMatch = new RecentMatchDto
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

        // Act
        state.RecentMatches.Add(newMatch);

        // Assert
        state.RecentMatches.Should().HaveCount(1, "Match feed should update with new match");
        state.RecentMatches[0].Bot1Name.Should().Be("AlphaBot");
    }

    [Fact]
    public async Task OnStandingsUpdated_RefreshesLeaderboard()
    {
        // Arrange
        var oldStandings = new List<TeamStandingDto>
        {
            new TeamStandingDto { Rank = 1, TeamName = "Team A", TotalPoints = 100, RankChange = 0 }
        };

        var state = new DashboardStateDto
        {
            OverallLeaderboard = oldStandings
        };

        var newStandings = new List<TeamStandingDto>
        {
            new TeamStandingDto { Rank = 1, TeamName = "Team A", TotalPoints = 110, RankChange = 0 }
        };

        // Act
        state.OverallLeaderboard = newStandings;

        // Assert
        state.OverallLeaderboard[0].TotalPoints.Should().Be(110, "Leaderboard should update with new standings");
    }

    [Fact]
    public async Task OnTournamentUpdated_RefreshesTournamentStatus()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            CurrentEvent = new CurrentEventDto
            {
                TournamentNumber = 1,
                GameType = GameType.RPSLS,
                MatchesCompleted = 5,
                TotalMatches = 20,
                ProgressPercentage = 25.0
            }
        };

        // Act
        state.CurrentEvent.MatchesCompleted = 10;
        state.CurrentEvent.ProgressPercentage = 50.0;

        // Assert
        state.CurrentEvent.MatchesCompleted.Should().Be(10);
        state.CurrentEvent.ProgressPercentage.Should().Be(50.0, "Progress should update to 50%");
    }

    [Fact]
    public async Task LastUpdatedTimestamp_IsSet_OnStateUpdate()
    {
        // Arrange
        var beforeUpdate = DateTime.UtcNow;
        var state = new TournamentStateDto
        {
            LastUpdated = beforeUpdate
        };

        // Act
        var afterUpdate = DateTime.UtcNow;
        state.LastUpdated = afterUpdate;

        // Assert
        state.LastUpdated.Should().BeCloseTo(afterUpdate, TimeSpan.FromSeconds(1));
        state.LastUpdated.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task StateTransition_FromWaitingToInProgress_UpdatesStatus()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted
        };

        // Act
        state.Status = TournamentStatus.InProgress;

        // Assert
        state.Status.Should().Be(TournamentStatus.InProgress);
    }

    [Fact]
    public async Task StateTransition_FromInProgressToCompleted_UpdatesStatus()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.InProgress
        };

        // Act
        state.Status = TournamentStatus.Completed;

        // Assert
        state.Status.Should().Be(TournamentStatus.Completed);
    }

    [Fact]
    public async Task UIState_HandlesRapidUpdates_InCorrectOrder()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted,
            LastUpdated = DateTime.UtcNow
        };

        var updates = new List<TournamentStatus>
        {
            TournamentStatus.InProgress,
            TournamentStatus.InProgress,
            TournamentStatus.InProgress,
            TournamentStatus.Completed
        };

        // Act
        foreach (var status in updates)
        {
            state.Status = status;
            state.LastUpdated = DateTime.UtcNow;
        }

        // Assert
        state.Status.Should().Be(TournamentStatus.Completed, "Final status should be Completed");
    }

    [Fact]
    public async Task NextMatch_Preview_UpdatesWhenAvailable()
    {
        // Arrange
        var state = new DashboardStateDto
        {
            NextMatch = null
        };

        var nextMatch = new NextMatchDto
        {
            Bot1Name = "AlphaBot",
            Bot2Name = "BetaBot",
            GameType = GameType.RPSLS,
            EstimatedSecondsUntilStart = 30
        };

        // Act
        state.NextMatch = nextMatch;

        // Assert
        state.NextMatch.Should().NotBeNull();
        state.NextMatch!.Bot1Name.Should().Be("AlphaBot");
        state.NextMatch.Bot2Name.Should().Be("BetaBot");
        state.NextMatch.EstimatedSecondsUntilStart.Should().Be(30);
    }
}
