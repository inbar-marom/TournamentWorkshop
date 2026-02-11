using Xunit;
using Moq;
using FluentAssertions;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Tests.UI;

public class GroupStandingsGridComponentTests
{
    private Mock<StateManagerService> _mockStateManager;

    public GroupStandingsGridComponentTests()
    {
        _mockStateManager = new Mock<StateManagerService>();
    }

    [Fact]
    public async Task GetGroupStandingsGrid_WithValidGroups_ReturnsFormattedGridData()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team A", Wins = 3, Losses = 0, Points = 9 },
                        new() { TeamName = "Team B", Wins = 2, Losses = 1, Points = 6 }
                    }
                },
                new()
                {
                    GroupName = "Group B",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team C", Wins = 2, Losses = 1, Points = 6 }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainKey("Group A");
        result["Group A"].Should().HaveCount(2);
    }

    [Fact]
    public async Task GridData_IsSortedByPointsDescending_WithinEachGroup()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team B", Wins = 1, Losses = 2, Points = 3 },
                        new() { TeamName = "Team A", Wins = 3, Losses = 0, Points = 9 }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();

        // Assert
        result["Group A"][0].TeamName.Should().Be("Team A");
        result["Group A"][0].Points.Should().Be(9);
        result["Group A"][1].TeamName.Should().Be("Team B");
        result["Group A"][1].Points.Should().Be(3);
    }

    [Fact]
    public async Task GridData_WithTieInPoints_SortsByWinsDescending()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team B", Wins = 2, Losses = 1, Points = 6 },
                        new() { TeamName = "Team A", Wins = 3, Losses = 0, Points = 6 }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();

        // Assert
        result["Group A"][0].TeamName.Should().Be("Team A");
        result["Group A"][0].Wins.Should().Be(3);
    }

    [Fact]
    public async Task GetGroupStandingsGrid_WithNoGroups_ReturnsEmptyDictionary()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>()
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GridData_ContainsAllRequiredColumns()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { Rank = 1, TeamName = "Team A", Wins = 3, Losses = 0, Points = 9, Draws = 0 }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();
        var team = result["Group A"][0];

        // Assert
        team.Should().NotBeNull();
        team.TeamName.Should().NotBeNullOrEmpty();
        team.Wins.Should().BeGreaterThanOrEqualTo(0);
        team.Losses.Should().BeGreaterThanOrEqualTo(0);
        team.Points.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GridData_WhenGroupStageNotActive_ReturnsNullOrEmptyGrid()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            CurrentTournament = new CurrentTournamentDto
            {
                Stage = TournamentStage.Finals
            },
            GroupStandings = new List<GroupDto>()
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var result = await service.GetGroupStandingsGridAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupCount_ReturnsCorrectNumberOfGroups()
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
                new() { GroupName = "Group A", Rankings = new List<BotRankingDto>() },
                new() { GroupName = "Group B", Rankings = new List<BotRankingDto>() },
                new() { GroupName = "Group C", Rankings = new List<BotRankingDto>() }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var groupCount = await service.GetGroupCountAsync();

        // Assert
        groupCount.Should().Be(3);
    }

    [Fact]
    public async Task GetTeamsInGroup_ReturnsCorrectTeamListForGroup()
    {
        // Arrange
        var state = new TournamentStateDto
        {
            GroupStandings = new List<GroupDto>
            {
                new()
                {
                    GroupName = "Group A",
                    Rankings = new List<BotRankingDto>
                    {
                        new() { TeamName = "Team A" },
                        new() { TeamName = "Team B" }
                    }
                }
            }
        };

        _mockStateManager.Setup(s => s.GetCurrentStateAsync()).ReturnsAsync(state);

        // Act
        var service = new GroupStandingsGridService(_mockStateManager.Object);
        var teams = await service.GetTeamsInGroupAsync("Group A");

        // Assert
        teams.Should().HaveCount(2);
        teams.Should().ContainSingle(t => t.TeamName == "Team A");
    }
}
