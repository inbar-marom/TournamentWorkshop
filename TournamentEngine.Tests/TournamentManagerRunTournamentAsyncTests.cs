namespace TournamentEngine.Tests;

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Unit tests for TournamentManager.RunTournamentAsync
/// These tests verify the orchestration logic using mocks, not the full tournament implementation
/// </summary>
[TestClass]
public class TournamentManagerRunTournamentAsyncTests
{
    private MockGameRunner _gameRunner = null!;
    private MockTournamentEngine _mockEngine = null!;
    private TournamentManager _manager = null!;

    [TestInitialize]
    public void Setup()
    {
        _gameRunner = new MockGameRunner();
        _mockEngine = new MockTournamentEngine();
        _manager = new TournamentManager(_mockEngine, _gameRunner);
    }

    [TestMethod]
    public async Task RunTournamentAsync_CallsInitializeTournament_Once()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.AreEqual(1, _mockEngine.InitializeCallCount, "Should call InitializeTournament exactly once");
        Assert.IsTrue(_mockEngine.MethodCalls[0].Contains("InitializeTournament"));
    }

    [TestMethod]
    public async Task RunTournamentAsync_CallsGetNextMatches_UntilNoMatchesRemain()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(4);
        
        // Setup mock to return matches, then empty list
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[2], dummyBots[3]) });
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)>()); // No more matches

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsTrue(_mockEngine.GetNextMatchesCallCount >= 3, "Should call GetNextMatches until empty");
    }

    [TestMethod]
    public async Task RunTournamentAsync_CallsRecordMatchResult_ForEachMatch()
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

        // Assert
        Assert.AreEqual(2, _mockEngine.RecordMatchResultCallCount, "Should record each match result");
    }

    [TestMethod]
    public async Task RunTournamentAsync_CallsAdvanceToNextRound_WhenNoMatchesRemain()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(4);
        
        // Setup mock to return matches in first call, empty in second
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) });

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsTrue(_mockEngine.AdvanceToNextRoundCallCount >= 1, "Should call AdvanceToNextRound");
    }

    [TestMethod]
    public async Task RunTournamentAsync_CallsGetTournamentInfo_AtEnd()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsTrue(_mockEngine.GetTournamentInfoCallCount >= 1, "Should call GetTournamentInfo to get final result");
    }

    [TestMethod]
    public async Task RunTournamentAsync_ExecutesMatches_ThroughGameRunner()
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

        // Assert
        Assert.AreEqual(2, _gameRunner.ExecutedMatches.Count, "Should execute each match through game runner");
    }

    [TestMethod]
    public async Task RunTournamentAsync_WithNullBots_ThrowsArgumentException()
    {
        // Arrange
        var config = TestHelpers.CreateDefaultConfig();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await _manager.RunTournamentAsync(null!, GameType.RPSLS, config);
        });
    }

    [TestMethod]
    public async Task RunTournamentAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
        {
            await _manager.RunTournamentAsync(bots, GameType.RPSLS, null!);
        });
    }

    [TestMethod]
    public async Task RunTournamentAsync_WithCancellationToken_ThrowsOnCancel()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20);
        var config = TestHelpers.CreateDefaultConfig();
        var cts = new CancellationTokenSource();
        
        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await _manager.RunTournamentAsync(bots, GameType.RPSLS, config, cts.Token);
        });
    }

    [TestMethod]
    public async Task RunTournamentAsync_ReturnsCompletedTournamentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        
        _mockEngine.FinalInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = GameType.RPSLS,
            State = TournamentState.Completed,
            Champion = "Team1",
            MatchResults = new List<MatchResult>(),
            EndTime = DateTime.UtcNow
        };

        // Act
        var result = await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.AreEqual(TournamentState.Completed, result.State);
        Assert.IsNotNull(result.Champion);
        Assert.AreEqual("Team1", result.Champion);
    }

    [TestMethod]
    public async Task RunTournamentAsync_LoopsUntilTournamentComplete()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = TestHelpers.CreateDefaultConfig();
        var dummyBots = TestHelpers.CreateDummyBots(4);
        
        // Setup mock to simulate multiple rounds with empty batches triggering round advancement
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[0], dummyBots[1]) }); // Round 1, match 1
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)>()); // Trigger advance
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)> { (dummyBots[2], dummyBots[3]) }); // Round 2, match 1
        _mockEngine.MatchBatches.Enqueue(new List<(IBot, IBot)>()); // Trigger advance

        // Act
        await _manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - should have advanced rounds multiple times
        Assert.IsTrue(_mockEngine.AdvanceToNextRoundCallCount >= 2, "Should advance through multiple rounds");
        Assert.AreEqual(2, _mockEngine.RecordMatchResultCallCount, "Should record all 2 matches");
    }
}
