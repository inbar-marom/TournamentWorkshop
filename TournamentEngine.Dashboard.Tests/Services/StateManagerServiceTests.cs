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
        var newState = new TournamentStateDto
        {
            TournamentId = "tournament-123",
            Status = TournamentStatus.InProgress,
            Message = "Round 1 in progress",
            OverallLeaderboard = new List<TeamStandingDto>
            {
                new() { Rank = 1, TeamName = "Bot1", TotalPoints = 100 }
            }
        };

        // Act
        await _service.UpdateStateAsync(newState);
        var retrievedState = await _service.GetCurrentStateAsync();

        // Assert
        retrievedState.Should().BeEquivalentTo(newState);
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
        await _service.UpdateStateAsync(new TournamentStateDto
        {
            Status = TournamentStatus.InProgress,
            Message = "Tournament active"
        });
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
                await _service.UpdateStateAsync(new TournamentStateDto
                {
                    TournamentId = $"tournament-{index}",
                    Status = TournamentStatus.InProgress
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
}
