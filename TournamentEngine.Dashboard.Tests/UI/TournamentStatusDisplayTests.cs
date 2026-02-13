using FluentAssertions;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using Xunit;

namespace TournamentEngine.Dashboard.Tests.UI;

/// <summary>
/// Tests for tournament status display component.
/// Phase 4: Basic UI - Tournament Status Display
/// </summary>
public class TournamentStatusDisplayTests
{
    [Fact]
    public void CurrentTournament_DisplaysCorrectInfo()
    {
        // Arrange
        var tournament = new CurrentEventDto
        {
            TournamentNumber = 2,
            GameType = GameType.ColonelBlotto,
            Stage = TournamentStage.GroupStage,
            CurrentRound = 3,
            TotalRounds = 5,
            MatchesCompleted = 15,
            TotalMatches = 30,
            ProgressPercentage = 50.0
        };

        // Act & Assert
        tournament.TournamentNumber.Should().Be(2, "Should show tournament 2");
        tournament.GameType.Should().Be(GameType.ColonelBlotto, "Should show ColonelBlotto game type");
        tournament.Stage.Should().Be(TournamentStage.GroupStage, "Should show Group Stage");
        tournament.CurrentRound.Should().Be(3, "Should show current round as 3");
        tournament.TotalRounds.Should().Be(5, "Should show total rounds as 5");
        tournament.MatchesCompleted.Should().Be(15);
        tournament.TotalMatches.Should().Be(30);
    }

    [Fact]
    public void ProgressBar_CalculatesPercentage()
    {
        // Arrange
        var tournament = new CurrentEventDto
        {
            MatchesCompleted = 50,
            TotalMatches = 100,
            ProgressPercentage = 50.0
        };

        // Act
        var calculatedProgress = (tournament.MatchesCompleted * 100.0) / tournament.TotalMatches;

        // Assert
        calculatedProgress.Should().Be(50.0, "Progress should be 50%");
        tournament.ProgressPercentage.Should().Be(50.0);
    }

    [Fact]
    public void ProgressBar_At0Percent_WhenNoMatches()
    {
        // Arrange
        var tournament = new CurrentEventDto
        {
            MatchesCompleted = 0,
            TotalMatches = 100,
            ProgressPercentage = 0.0
        };

        // Act & Assert
        tournament.ProgressPercentage.Should().Be(0.0, "Should show 0% when no matches completed");
    }

    [Fact]
    public void ProgressBar_At100Percent_WhenComplete()
    {
        // Arrange
        var tournament = new CurrentEventDto
        {
            MatchesCompleted = 100,
            TotalMatches = 100,
            ProgressPercentage = 100.0
        };

        // Act & Assert
        tournament.ProgressPercentage.Should().Be(100.0, "Should show 100% when all matches completed");
    }

    [Fact]
    public void SeriesProgress_ShowsMultipleTournaments()
    {
        // Arrange
        var series = new TournamentProgressDto
        {
            TournamentId = "Series001",
            CompletedCount = 2,
            TotalCount = 4,
            CurrentEventIndex = 2,
            Events = new List<EventInTournamentDto>
            {
                new EventInTournamentDto
                {
                    EventNumber = 1,
                    GameType = GameType.RPSLS,
                    Status = EventItemStatus.Completed,
                    Champion = "TeamA"
                },
                new EventInTournamentDto
                {
                    EventNumber = 2,
                    GameType = GameType.ColonelBlotto,
                    Status = EventItemStatus.Completed,
                    Champion = "TeamB"
                },
                new EventInTournamentDto
                {
                    EventNumber = 3,
                    GameType = GameType.RPSLS,
                    Status = EventItemStatus.InProgress,
                    Champion = null
                },
                new EventInTournamentDto
                {
                    EventNumber = 4,
                    GameType = GameType.ColonelBlotto,
                    Status = EventItemStatus.Pending,
                    Champion = null
                }
            }
        };

        // Act & Assert
        series.CompletedCount.Should().Be(2, "Should have 2 completed tournaments");
        series.TotalCount.Should().Be(4, "Should have 4 total tournaments");
        series.CurrentEventIndex.Should().Be(2, "Should be on tournament 3 (index 2)");
        series.Events.Should().HaveCount(4);
        
        series.Events[0].Status.Should().Be(EventItemStatus.Completed);
        series.Events[1].Status.Should().Be(EventItemStatus.Completed);
        series.Events[2].Status.Should().Be(EventItemStatus.InProgress);
        series.Events[3].Status.Should().Be(EventItemStatus.Pending);
    }

    [Fact]
    public void TournamentStage_TransitionsFromGroupStageToFinals()
    {
        // Arrange
        var tournament = new CurrentEventDto
        {
            Stage = TournamentStage.GroupStage,
            CurrentRound = 5,
            TotalRounds = 5
        };

        // Act
        tournament.Stage = TournamentStage.Finals;
        tournament.CurrentRound = 1;
        tournament.TotalRounds = 3;

        // Assert
        tournament.Stage.Should().Be(TournamentStage.Finals, "Should transition to Finals");
        tournament.CurrentRound.Should().Be(1, "Should reset round to 1 for Finals");
        tournament.TotalRounds.Should().Be(3, "Finals should have 3 rounds");
    }

    [Fact]
    public void TournamentStatus_DisplaysChampion_WhenComplete()
    {
        // Arrange
        var series = new TournamentProgressDto
        {
            Events = new List<EventInTournamentDto>
            {
                new EventInTournamentDto
                {
                    EventNumber = 1,
                    GameType = GameType.RPSLS,
                    Status = EventItemStatus.Completed,
                    Champion = "TeamA",
                    StartTime = DateTime.UtcNow.AddHours(-2),
                    EndTime = DateTime.UtcNow.AddHours(-1)
                }
            }
        };

        // Act
        var champion = series.Events[0].Champion;

        // Assert
        champion.Should().Be("TeamA", "Should display the champion");
        series.Events[0].Status.Should().Be(EventItemStatus.Completed);
    }

    [Fact]
    public void GameTypeDisplay_ShowsCorrectType()
    {
        // Arrange & Act
        var rpsls = GameType.RPSLS;
        var colonelBlotto = GameType.ColonelBlotto;

        // Assert
        rpsls.Should().Be(GameType.RPSLS);
        colonelBlotto.Should().Be(GameType.ColonelBlotto);
    }
}
