using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Tests.Services;

public class StateManagerServiceTests
{
    private readonly StateManagerService _service;

    public StateManagerServiceTests()
    {
        var mockLogger = Mock.Of<ILogger<StateManagerService>>();
        _service = new StateManagerService(mockLogger);
    }

    [Fact]
    public async Task GetCurrentStateAsync_InitialState_ReturnsDefaultWaitingState()
    {
        // Act
        var state = await _service.GetCurrentStateAsync();

        // Assert
        state.Should().NotBeNull();
        state.Status.Should().Be(TournamentStatus.NotStarted);
        state.Message.Should().Be("Waiting for tournament to start...");
        state.OverallLeaderboard.Should().BeEmpty();
        state.GroupStandings.Should().BeEmpty();
        state.RecentMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateStateAsync_SetsNewState()
    {
        // Arrange
        var newTournamentState = new TournamentStateDto
        {
            TournamentId = "tournament-123",
            Status = TournamentStatus.InProgress
        };
        var newDashboardState = new DashboardStateDto
        {
            TournamentState = newTournamentState,
            Message = "Round 1 in progress",
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { Rank = 1, TeamName = "Bot1", TotalPoints = 100 }
            }
        };

        // Act
        await _service.UpdateStateAsync(newDashboardState);
        var retrievedState = await _service.GetCurrentStateAsync();

        // Assert
        retrievedState.TournamentState.Should().BeEquivalentTo(newTournamentState);
    }

    [Fact]
    public async Task AddRecentMatch_AddsMatchToQueue()
    {
        // Arrange
        var match = new MatchCompletedDto
        {
            MatchId = "match-1",
            Bot1Name = "Bot1",
            Bot2Name = "Bot2",
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "Bot1",
            Bot1Score = 10,
            Bot2Score = 5,
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        };

        // Act
        _service.AddRecentMatch(match);
        var matches = _service.GetRecentMatches(10);

        // Assert
        matches.Should().ContainSingle();
        matches[0].Should().BeEquivalentTo(match);
    }

    [Fact]
    public async Task AddRecentMatch_KeepsOnlyLast50Matches()
    {
        // Arrange - Add 60 matches
        for (int i = 0; i < 60; i++)
        {
            _service.AddRecentMatch(new MatchCompletedDto
            {
                MatchId = $"match-{i}",
                Bot1Name = "Bot1",
                Bot2Name = "Bot2",
                Outcome = MatchOutcome.Player1Wins,
                CompletedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        // Act
        var matches = _service.GetRecentMatches(100);

        // Assert
        matches.Should().HaveCount(50);
        // Should have the most recent 50 (match-10 through match-59)
        matches.First().MatchId.Should().Be("match-59");
        matches.Last().MatchId.Should().Be("match-10");
    }

    [Fact]
    public async Task GetRecentMatches_ReturnsRequestedCount()
    {
        // Arrange - Add 20 matches
        for (int i = 0; i < 20; i++)
        {
            _service.AddRecentMatch(new MatchCompletedDto
            {
                MatchId = $"match-{i}",
                Bot1Name = "Bot1",
                Bot2Name = "Bot2",
                Outcome = MatchOutcome.Player1Wins
            });
        }

        // Act
        var matches = _service.GetRecentMatches(5);

        // Assert
        matches.Should().HaveCount(5);
        // Should return the 5 most recent
        matches.First().MatchId.Should().Be("match-19");
        matches.Last().MatchId.Should().Be("match-15");
    }

    [Fact]
    public async Task ClearStateAsync_ResetsToInitialState()
    {
        // Arrange - Set up some state
        var updateState = new DashboardStateDto
        {
            TournamentState = new TournamentStateDto
            {
                Status = TournamentStatus.InProgress
            },
            Message = "Tournament active"
        };
        await _service.UpdateStateAsync(updateState);
        _service.AddRecentMatch(new MatchCompletedDto
        {
            MatchId = "match-1",
            Bot1Name = "Bot1",
            Bot2Name = "Bot2"
        });

        // Act
        await _service.ClearStateAsync();

        // Assert
        var state = await _service.GetCurrentStateAsync();
        state.Status.Should().Be(TournamentStatus.NotStarted);
        state.Message.Should().Be("State cleared");
        
        var matches = _service.GetRecentMatches(10);
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple concurrent operations
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await _service.UpdateStateAsync(new DashboardStateDto
                {
                    TournamentState = new TournamentStateDto
                    {
                        TournamentId = $"tournament-{index}",
                        Status = TournamentStatus.InProgress
                    }
                });
            }));

            tasks.Add(Task.Run(() =>
            {
                _service.AddRecentMatch(new MatchCompletedDto
                {
                    MatchId = $"match-{index}",
                    Bot1Name = "Bot1",
                    Bot2Name = "Bot2"
                });
            }));

            tasks.Add(Task.Run(async () =>
            {
                await _service.GetCurrentStateAsync();
            }));

            tasks.Add(Task.Run(() =>
            {
                _service.GetRecentMatches(10);
            }));
        }

        // Assert - Should complete without exceptions
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await Task.WhenAll(tasks);
            throw new Exception("Should not throw");
        }).ContinueWith(t => t.Exception.Should().BeNull());
    }

    [Fact]
    public async Task UpdateTournamentStartedAsync_SetsSeriesState()
    {
        // Arrange
        var seriesEvent = new TournamentStartedEventDto
        {
            TournamentId = "series-1",
            TournamentName = "Local Series",
            TotalSteps = 3,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted },
                new() { StepIndex = 3, GameType = GameType.PenaltyKicks, Status = EventStepStatus.NotStarted }
            },
            StartedAt = DateTime.UtcNow
        };

        // Act
        await _service.UpdateTournamentStartedAsync(seriesEvent);
        var state = await _service.GetCurrentStateAsync();

        // Assert
        state.TournamentState.Should().NotBeNull();
        state.TournamentState!.TournamentId.Should().Be("series-1");
        state.TournamentState.TournamentName.Should().Be("Local Series");
        state.TournamentState.TotalSteps.Should().Be(3);
        state.TournamentState.Status.Should().Be(TournamentStatus.InProgress);
        state.TournamentState.Steps.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateTournamentProgressAsync_UpdatesCurrentStep()
    {
        // Arrange
        await _service.UpdateTournamentStartedAsync(new TournamentStartedEventDto
        {
            TournamentId = "series-2",
            TournamentName = "Progress Series",
            TotalSteps = 2,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted }
            },
            StartedAt = DateTime.UtcNow
        });

        var progressEvent = new TournamentProgressUpdatedEventDto
        {
            TournamentState = new TournamentStateDto
            {
                TournamentId = "series-2",
                TournamentName = "Progress Series",
                TotalSteps = 2,
                CurrentStepIndex = 2,
                Status = TournamentStatus.InProgress,
                Steps = new List<EventStepDto>
                {
                    new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.Completed, WinnerName = "TeamA" },
                    new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.InProgress }
                }
            },
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _service.UpdateTournamentProgressAsync(progressEvent);
        var state = await _service.GetCurrentStateAsync();

        // Assert
        state.TournamentState.Should().NotBeNull();
        state.TournamentState!.CurrentStepIndex.Should().Be(2);
        state.TournamentState.Steps.First(s => s.StepIndex == 1).Status.Should().Be(EventStepStatus.Completed);
    }

    [Fact]
    public async Task UpdateEventStepCompletedAsync_MarksWinner()
    {
        // Arrange
        await _service.UpdateTournamentStartedAsync(new TournamentStartedEventDto
        {
            TournamentId = "series-3",
            TournamentName = "Winner Series",
            TotalSteps = 1,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress }
            },
            StartedAt = DateTime.UtcNow
        });

        var completedEvent = new EventStepCompletedDto
        {
            StepIndex = 1,
            GameType = GameType.RPSLS,
            WinnerName = "TeamWin",
            TournamentId = "t-1",
            TournamentName = "RPSLS Tournament #1",
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _service.UpdateEventStepCompletedAsync(completedEvent);
        var state = await _service.GetCurrentStateAsync();

        // Assert
        state.TournamentState.Should().NotBeNull();
        var step = state.TournamentState!.Steps.Single(s => s.StepIndex == 1);
        step.Status.Should().Be(EventStepStatus.Completed);
        step.WinnerName.Should().Be("TeamWin");
        step.TournamentId.Should().Be("t-1");
    }

    [Fact]
    public async Task UpdateTournamentCompletedAsync_SetsSeriesCompleted()
    {
        // Arrange
        await _service.UpdateTournamentStartedAsync(new TournamentStartedEventDto
        {
            TournamentId = "series-4",
            TournamentName = "Complete Series",
            TotalSteps = 1,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.Completed, WinnerName = "TeamWin" }
            },
            StartedAt = DateTime.UtcNow
        });

        var completedEvent = new TournamentCompletedEventDto
        {
            TournamentId = "series-4",
            TournamentName = "Complete Series",
            Champion = "TeamWin",
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _service.UpdateTournamentCompletedAsync(completedEvent);
        var state = await _service.GetCurrentStateAsync();

        // Assert
        state.TournamentState.Should().NotBeNull();
        state.TournamentState!.Status.Should().Be(TournamentStatus.Completed);
    }

    [Fact]
    public async Task UpdateStateAsync_PreservesTournamentSteps_WhenIncomingStateOmitsTournamentState()
    {
        await _service.UpdateTournamentStartedAsync(new TournamentStartedEventDto
        {
            TournamentId = "series-keep-steps",
            TournamentName = "Keep Steps Series",
            TotalSteps = 2,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.Completed, WinnerName = "TeamA" },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.InProgress }
            },
            StartedAt = DateTime.UtcNow
        });

        await _service.UpdateStateAsync(new DashboardStateDto
        {
            Status = TournamentStatus.Completed,
            Message = "Final snapshot with partial fields",
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { TeamName = "TeamA", TotalPoints = 12, Rank = 1 }
            }
        });

        var state = await _service.GetCurrentStateAsync();

        state.TournamentState.Should().NotBeNull();
        state.TournamentState!.Steps.Should().HaveCount(2);
        state.TournamentState.Steps.Should().Contain(s => s.StepIndex == 1 && s.WinnerName == "TeamA");
    }

    [Fact]
    public async Task UpdateStandingsAsync_AggregatesScoresAcrossEvents_AndReplacesLatestSnapshotPerEvent()
    {
        await _service.UpdateTournamentStartedAsync(new TournamentStartedEventDto
        {
            TournamentId = "series-cumulative",
            TournamentName = "Cumulative Series",
            TotalSteps = 2,
            Steps = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.InProgress },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted }
            },
            StartedAt = DateTime.UtcNow
        });

        await _service.UpdateStandingsAsync(new StandingsUpdatedDto
        {
            TournamentId = "event-1",
            TournamentName = "Event 1",
            OverallStandings = new List<TeamStandingDto>
            {
                new() { TeamName = "TeamA", TotalPoints = 3 },
                new() { TeamName = "TeamB", TotalPoints = 1 }
            },
            UpdatedAt = DateTime.UtcNow
        });

        await _service.UpdateStandingsAsync(new StandingsUpdatedDto
        {
            TournamentId = "event-2",
            TournamentName = "Event 2",
            OverallStandings = new List<TeamStandingDto>
            {
                new() { TeamName = "TeamA", TotalPoints = 2 },
                new() { TeamName = "TeamB", TotalPoints = 4 }
            },
            UpdatedAt = DateTime.UtcNow
        });

        await _service.UpdateStandingsAsync(new StandingsUpdatedDto
        {
            TournamentId = "event-1",
            TournamentName = "Event 1",
            OverallStandings = new List<TeamStandingDto>
            {
                new() { TeamName = "TeamA", TotalPoints = 5 },
                new() { TeamName = "TeamB", TotalPoints = 0 }
            },
            UpdatedAt = DateTime.UtcNow
        });

        var state = await _service.GetCurrentStateAsync();
        state.OverallLeaderboard.Should().NotBeNull();
        state.OverallLeaderboard.Should().Contain(s => s.TeamName == "TeamA" && s.TotalPoints == 7);
        state.OverallLeaderboard.Should().Contain(s => s.TeamName == "TeamB" && s.TotalPoints == 4);
        state.OverallLeaderboard.First().TeamName.Should().Be("TeamA");
    }
}
