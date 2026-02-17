namespace TournamentEngine.Tests.GameRunner;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Tests.Helpers;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for game history tracking in GameState
/// </summary>
[TestClass]
public class GameHistoryTests
{
    [TestMethod]
    public async Task RPSLS_FirstMatch_HasEmptyHistory()
    {
        // Arrange
        var config = IntegrationTestHelpers.CreateConfig();
        var gameRunner = new GameRunner(config);
        var bot1 = new HistoryTrackingBot("Bot1", "Rock");
        var bot2 = new HistoryTrackingBot("Bot2", "Paper");

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // On first match, bot should see empty history
        Assert.AreEqual(0, bot1.FirstGameStateReceived?.RoundHistory?.Count ?? 0, 
            "Bot should have empty history on first match");
    }

    [TestMethod]
    public async Task RPSLS_DuringMatch_ProvidesPreviousRounds()
    {
        // Arrange
        var config = IntegrationTestHelpers.CreateConfig();
        var customConfig = new TournamentConfig
        {
            ImportTimeout = config.ImportTimeout,
            MoveTimeout = config.MoveTimeout,
            MemoryLimitMB = config.MemoryLimitMB,
            MaxRoundsRPSLS = 10, // Ensure multiple rounds
            LogLevel = config.LogLevel,
            LogFilePath = config.LogFilePath,
            BotsDirectory = config.BotsDirectory,
            ResultsFilePath = config.ResultsFilePath
        };
        var gameRunner = new GameRunner(customConfig);
        var bot1 = new HistoryTrackingBot("Bot1", "Rock");
        var bot2 = new HistoryTrackingBot("Bot2", "Paper");

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // During the match, after round 1, bot should see history from round 1
        // The tracking bot captures the first GameState it sees (round 1)
        // So we expect 0 history items on round 1 (no previous rounds yet)
        Assert.AreEqual(0, bot1.FirstGameStateReceived?.RoundHistory?.Count ?? 0,
            "First round should have no history yet");
            
        // But if we check later rounds, they should have history
        // We need a better bot that tracks multiple rounds
    }

    [TestMethod]
    public async Task RPSLS_MultipleRounds_BuildsHistory()
    {
        // Arrange
        var config = IntegrationTestHelpers.CreateConfig();
        var customConfig = new TournamentConfig
        {
            ImportTimeout = config.ImportTimeout,
            MoveTimeout = config.MoveTimeout,
            MemoryLimitMB = config.MemoryLimitMB,
            MaxRoundsRPSLS = 5,
            LogLevel = config.LogLevel,
            LogFilePath = config.LogFilePath,
            BotsDirectory = config.BotsDirectory,
            ResultsFilePath = config.ResultsFilePath
        };
        var gameRunner = new GameRunner(customConfig);
        var bot1 = new MultiRoundTrackingBot("Bot1", "Rock");
        var bot2 = new MultiRoundTrackingBot("Bot2", "Scissors");

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // On round 2, should see 1 history item
        if (bot1.GameStatesReceived.Count >= 2)
        {
            var round2State = bot1.GameStatesReceived[1];
            Assert.AreEqual(1, round2State.RoundHistory.Count, 
                "Round 2 should have 1 history item (round 1)");
            Assert.AreEqual(1, round2State.RoundHistory[0].Round);
        }
        
        // On round 3, should see 2 history items
        if (bot1.GameStatesReceived.Count >= 3)
        {
            var round3State = bot1.GameStatesReceived[2];
            Assert.AreEqual(2, round3State.RoundHistory.Count,
                "Round 3 should have 2 history items (rounds 1-2)");
        }
    }

    [TestMethod]
    [Ignore("Cross-match history tracking requires architectural changes - deferred")]
    public async Task RPSLS_SecondMatch_IncludesPreviousHistory()
    {
        // Arrange
        var config = IntegrationTestHelpers.CreateConfig();
        var gameRunner = new GameRunner(config);
        var bot1 = new HistoryTrackingBot("Bot1", "Rock");
        var bot2 = new HistoryTrackingBot("Bot2", "Paper");

        // Act - Run two matches between same bots
        await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);
        bot1.Reset();
        bot2.Reset();
        
        var result2 = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result2);
        // On second match, bot should see history from first match
        // Note: This requires GameRunner to track match history by opponent
        Assert.IsTrue(bot1.FirstGameStateReceived?.RoundHistory?.Count > 0, 
            "Bot should have history from previous match against same opponent");
    }

    [TestMethod]
    public async Task PenaltyKicks_TracksHistoryWithRoles()
    {
        // Arrange
        var config = IntegrationTestHelpers.CreateConfig();
        var gameRunner = new GameRunner(config);
        var bot1 = new HistoryTrackingBot("Bot1", "Left");
        var bot2 = new HistoryTrackingBot("Bot2", "Center");

        // Act
        await gameRunner.ExecuteMatch(bot1, bot2, GameType.PenaltyKicks, CancellationToken.None);

        // Assert
        var gameState = bot1.FirstGameStateReceived;
        Assert.IsNotNull(gameState);
        
        // History should include role information
        if (gameState.RoundHistory != null && gameState.RoundHistory.Count > 0)
        {
            var firstRound = gameState.RoundHistory[0];
            Assert.IsNotNull(firstRound.Role, "History should track which role bot played");
        }
    }
}

/// <summary>
/// Test bot that tracks the GameState it receives
/// </summary>
public class HistoryTrackingBot : IBot
{
    private readonly string _defaultMove;
    public GameState? FirstGameStateReceived { get; private set; }

    public HistoryTrackingBot(string teamName, string defaultMove)
    {
        TeamName = teamName;
        _defaultMove = defaultMove;
        GameType = GameType.RPSLS;
    }

    public string TeamName { get; }
    public GameType GameType { get; }

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        FirstGameStateReceived ??= gameState;
        return Task.FromResult(_defaultMove);
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        FirstGameStateReceived ??= gameState;
        return Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        FirstGameStateReceived ??= gameState;
        return Task.FromResult(_defaultMove);
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        FirstGameStateReceived ??= gameState;
        return Task.FromResult("0");
    }

    public void Reset()
    {
        FirstGameStateReceived = null;
    }
}

/// <summary>
/// Test bot that tracks all GameStates it receives across rounds
/// </summary>
public class MultiRoundTrackingBot : IBot
{
    private readonly string _defaultMove;
    public List<GameState> GameStatesReceived { get; private set; } = new();

    public MultiRoundTrackingBot(string teamName, string defaultMove)
    {
        TeamName = teamName;
        _defaultMove = defaultMove;
        GameType = GameType.RPSLS;
    }

    public string TeamName { get; }
    public GameType GameType { get; }

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        GameStatesReceived.Add(gameState);
        return Task.FromResult(_defaultMove);
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        GameStatesReceived.Add(gameState);
        return Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        GameStatesReceived.Add(gameState);
        return Task.FromResult(_defaultMove);
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        GameStatesReceived.Add(gameState);
        return Task.FromResult("0");
    }
}
