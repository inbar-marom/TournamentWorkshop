using Xunit;
using Moq;
using FluentAssertions;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class TournamentVisualizationServiceTests
{
    private Mock<StateManagerService> _mockStateManager;
    private Mock<MatchFeedService> _mockMatchFeed;

    public TournamentVisualizationServiceTests()
    {
        _mockStateManager = new Mock<StateManagerService>();
        _mockMatchFeed = new Mock<MatchFeedService>();
    }

    [Fact]
    public async Task GetBracketVisualization_WithValidTournament_ReturnsBracketData()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.Finals
            },
            RecentMatches = new List<RecentMatchDto>
            {
                new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A" },
                new() { Bot1Name = "Team A", Bot2Name = "Team C", WinnerName = "Team A" }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetBracketVisualizationAsync();

        // Assert
        result.Should().NotBeNull();
        result.Rounds.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBracketData_DisplaysTeamsAndWinners()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.Finals
            },
            RecentMatches = new List<RecentMatchDto>
            {
                new()
                {
                    Bot1Name = "Team A",
                    Bot2Name = "Team B",
                    WinnerName = "Team A",
                    Outcome = MatchOutcome.Player1Wins
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetBracketVisualizationAsync();

        // Assert
        result.Rounds.Should().NotBeEmpty();
        result.Rounds[0].Matchups.Should().NotBeEmpty();
        result.Rounds[0].Matchups[0].Team1.Should().Be("Team A");
        result.Rounds[0].Matchups[0].Team2.Should().Be("Team B");
        result.Rounds[0].Matchups[0].Winner.Should().Be("Team A");
    }

    [Fact]
    public async Task GetTournamentTreeStructure_ShowsGroupsToFinals()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.GroupStage
            },
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team A", Wins = 2, Losses = 0 },
                        new() { TeamName = "Team B", Wins = 1, Losses = 1 }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTournamentTreeStructureAsync();

        // Assert
        result.Should().NotBeNull();
        result.CurrentStage.Should().Be("GroupStage");
        result.Stages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TreeStructure_ContainsStageTransitionInfo()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.GroupStage,
                CurrentRound = 3,
                TotalRounds = 5
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetTournamentTreeStructureAsync();

        // Assert
        result.CurrentStage.Should().Be("GroupStage");
        result.CurrentRound.Should().Be(3);
        result.TotalRounds.Should().Be(5);
    }

    [Fact]
    public async Task GetRoundRobinGrid_ShowsAllTeamMatchups()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A" },
                new() { TeamName = "Team B" },
                new() { TeamName = "Team C" }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetRoundRobinGridAsync();

        // Assert
        result.Should().NotBeNull();
        result.TeamNames.Should().HaveCount(3);
        result.TeamNames.Should().Contain("Team A");
    }

    [Fact]
    public async Task RoundRobinGrid_ContainsMatchResultsMatrix()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", Bot2Name = "Team B", WinnerName = "Team A" },
            new() { Bot1Name = "Team A", Bot2Name = "Team C", WinnerName = "Team A" },
            new() { Bot1Name = "Team B", Bot2Name = "Team C", Outcome = MatchOutcome.Draw }
        };

        _mockMatchFeed.Setup(s => s.GetRecentMatchesAsync(It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetRoundRobinGridAsync();

        // Assert
        result.ResultsMatrix.Should().NotBeNull();
        result.ResultsMatrix.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetChampionPath_ShowsWinnerJourneyThroughTournament()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            SeriesProgress = new SeriesProgressDto
            {
                Tournaments = new List<TournamentInSeriesDto>
                {
                    new() { TournamentNumber = 1, Champion = "Team A", Status = TournamentItemStatus.Completed }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetChampionPathAsync();

        // Assert
        result.Should().NotBeNull();
        result.ChampionName.Should().Be("Team A");
        result.MatchesWon.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ChampionPath_ContainsAllWinsInSequence()
    {
        // Arrange
        var matches = new List<RecentMatchDto>
        {
            new() { Bot1Name = "Team A", WinnerName = "Team A", CompletedAt = DateTime.UtcNow.AddHours(-3) },
            new() { Bot1Name = "Team A", WinnerName = "Team A", CompletedAt = DateTime.UtcNow.AddHours(-2) },
            new() { Bot1Name = "Team A", WinnerName = "Team A", CompletedAt = DateTime.UtcNow.AddHours(-1) }
        };

        _mockMatchFeed.Setup(s => s.GetMatchesForTeamAsync("Team A", It.IsAny<int>())).ReturnsAsync(matches);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetChampionPathAsync();

        // Assert
        result.MatchesWon.Should().Be(3);
    }

    [Fact]
    public async Task GetProgressionVisualization_ShowsTeamProgressionThroughStages()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", Rank = 1 },
                new() { TeamName = "Team B", Rank = 2 }
            },
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.Finals
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetProgressionVisualizationAsync();

        // Assert
        result.Should().NotBeNull();
        result.Teams.Should().NotBeEmpty();
        result.CurrentStage.Should().Be("Finals");
    }

    [Fact]
    public async Task ProgressionVisualization_IndicatesTeamStatusInCurrentStage()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "Team A", Rank = 1 },
                new() { TeamName = "Team B", Rank = 2 },
                new() { TeamName = "Team C", Rank = 3 }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetProgressionVisualizationAsync();

        // Assert
        result.Teams[0].TeamName.Should().Be("Team A");
        result.Teams[0].CurrentRank.Should().Be(1);
    }

    [Fact]
    public async Task GetVisualizationData_WithNoTournament_ReturnsEmptyOrNull()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = null
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetBracketVisualizationAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSeriesVisualization_ShowsMultipleTournaments()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            SeriesProgress = new SeriesProgressDto
            {
                TotalCount = 4,
                CompletedCount = 2,
                Tournaments = new List<TournamentInSeriesDto>
                {
                    new() { TournamentNumber = 1, Champion = "Team A", Status = TournamentItemStatus.Completed },
                    new() { TournamentNumber = 2, Champion = "Team B", Status = TournamentItemStatus.Completed },
                    new() { TournamentNumber = 3, Status = TournamentItemStatus.InProgress }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetSeriesVisualizationAsync();

        // Assert
        result.Should().NotBeNull();
        result.TournamentCount.Should().Be(4);
        result.CompletedCount.Should().Be(2);
        result.Tournaments.Should().HaveCount(3);
    }

    [Fact]
    public async Task SeriesVisualization_IndicatesChampionOfEachTournament()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            SeriesProgress = new SeriesProgressDto
            {
                Tournaments = new List<TournamentInSeriesDto>
                {
                    new() { TournamentNumber = 1, Champion = "Team A" },
                    new() { TournamentNumber = 2, Champion = "Team B" }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new TournamentVisualizationService(_mockStateManager.Object, _mockMatchFeed.Object);
        var result = await service.GetSeriesVisualizationAsync();

        // Assert
        result.Tournaments[0].ChampionName.Should().Be("Team A");
        result.Tournaments[1].ChampionName.Should().Be("Team B");
    }
}
