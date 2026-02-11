using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

namespace TournamentEngine.Tests.Integration;

/// <summary>
/// Integration tests for TournamentManager event publishing to dashboard.
/// Phase 3: Verify that TournamentManager publishes events at the right times.
/// </summary>
[TestClass]
public class TournamentManagerEventPublishingTests
{
    private Mock<ITournamentEventPublisher> _mockPublisher = null!;
    private MockGameRunner _gameRunner = null!;
    private MockTournamentEngine _mockEngine = null!;
    private TournamentManager _manager = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockPublisher = new Mock<ITournamentEventPublisher>();
        _gameRunner = new MockGameRunner();
        _mockEngine = new MockTournamentEngine();
        
        // TournamentManager constructor should accept optional ITournamentEventPublisher
        _manager = new TournamentManager(_mockEngine, _gameRunner, _mockPublisher.Object);
    }

    [TestMethod]
    public async Task RunTournamentAsync_PublishesTournamentStartedEvent()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var gameType = GameType.RPSLS;

        // Act
        await _manager.RunTournamentAsync(bots, gameType, config);

        // Assert
        _mockPublisher.Verify(
            x => x.PublishTournamentStartedAsync(It.Is<TournamentStartedDto>(dto =>
                dto.GameType == gameType &&
                dto.TotalBots == bots.Count)),
            Times.Once,
            "TournamentManager should publish TournamentStarted event when tournament begins");
    }

    [TestMethod]
    public async Task RunTournamentAsync_PublishesMatchCompletedForEachMatch()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(4);
        
        // Setup mock to return 2 matches
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> 
        { 
            (dummyBots[0], dummyBots[1]),
            (dummyBots[2], dummyBots[3])
        });

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Should publish 2 match events
        _mockPublisher.Verify(
            x => x.PublishMatchCompletedAsync(It.IsAny<MatchCompletedDto>()),
            Times.Exactly(2),
            "TournamentManager should publish MatchCompleted event after each match");
    }

    [TestMethod]
    public async Task RunTournamentAsync_PublishesTournamentCompletedEvent()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var gameType = GameType.RPSLS;

        // Act
        await _manager.RunTournamentAsync(bots, gameType, config);

        // Assert
        _mockPublisher.Verify(
            x => x.PublishTournamentCompletedAsync(It.Is<TournamentCompletedDto>(dto =>
                dto.GameType == gameType &&
                !string.IsNullOrEmpty(dto.Champion))),
            Times.Once,
            "TournamentManager should publish TournamentCompleted event when tournament finishes");
    }

    [TestMethod]
    public async Task RunTournamentAsync_PublishesStandingsUpdatedEvent()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(2);
        
        // Setup mock to have multiple rounds
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        _mockPublisher.Verify(
            x => x.PublishStandingsUpdatedAsync(It.IsAny<StandingsUpdatedDto>()),
            Times.AtLeastOnce,
            "TournamentManager should publish StandingsUpdated event during tournament");
    }

    [TestMethod]
    public async Task RunTournamentAsync_PublishesEventsInCorrectOrder()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(2);
        var eventOrder = new List<string>();

        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });

        _mockPublisher
            .Setup(x => x.PublishTournamentStartedAsync(It.IsAny<TournamentStartedDto>()))
            .Callback(() => eventOrder.Add("TournamentStarted"))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(x => x.PublishMatchCompletedAsync(It.IsAny<MatchCompletedDto>()))
            .Callback(() => eventOrder.Add("MatchCompleted"))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(x => x.PublishTournamentCompletedAsync(It.IsAny<TournamentCompletedDto>()))
            .Callback(() => eventOrder.Add("TournamentCompleted"))
            .Returns(Task.CompletedTask);

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        eventOrder.Should().NotBeEmpty();
        eventOrder.First().Should().Be("TournamentStarted", "Tournament should start first");
        eventOrder.Last().Should().Be("TournamentCompleted", "Tournament should complete last");
        eventOrder.Should().Contain("MatchCompleted", "Matches should occur during tournament");
    }

    [TestMethod]
    public async Task RunTournamentAsync_MatchCompletedEvent_ContainsCorrectData()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(2);
        var config = TestHelpers.CreateDefaultConfig();
        var gameType = GameType.RPSLS;
        var dummyBots = TestHelpers.CreateDummyBots(2);
        MatchCompletedDto? capturedMatch = null;

        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });

        _mockPublisher
            .Setup(x => x.PublishMatchCompletedAsync(It.IsAny<MatchCompletedDto>()))
            .Callback<MatchCompletedDto>(m => capturedMatch ??= m)
            .Returns(Task.CompletedTask);

        // Act
        await _manager.RunTournamentAsync(bots, gameType, config);

        // Assert
        capturedMatch.Should().NotBeNull("At least one match should be completed");
        capturedMatch!.Bot1Name.Should().NotBeNullOrEmpty("Match should have bot1 name");
        capturedMatch.Bot2Name.Should().NotBeNullOrEmpty("Match should have bot2 name");
        capturedMatch.Outcome.Should().NotBe(MatchOutcome.Unknown, "Match should have a valid outcome");
        capturedMatch.GameType.Should().Be(gameType, "Match should have correct game type");
        capturedMatch.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [TestMethod]
    public async Task RunTournamentAsync_WithNullPublisher_DoesNotThrow()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        
        // Should be able to run without publisher (publisher is optional)
        var managerWithoutPublisher = new TournamentManager(_mockEngine, _gameRunner, eventPublisher: null);

        // Act & Assert - should not throw
        await managerWithoutPublisher.RunTournamentAsync(bots, GameType.RPSLS, config);
        
        Assert.IsTrue(true, "TournamentManager should work without event publisher");
    }
}

