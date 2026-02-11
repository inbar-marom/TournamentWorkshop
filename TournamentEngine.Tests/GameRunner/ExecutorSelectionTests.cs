using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentEngine.Tests.GameRunner;

[TestClass]
public class ExecutorSelectionTests
{
    private static TournamentConfig CreateTestConfig()
    {
        return new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MaxRoundsRPSLS = 50
        };
    }

    [TestMethod]
    public async Task ExecuteMatch_RPSLSGameType_ShouldUseRpslsExecutor()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.RPSLS, result.GameType);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("RPSLS Match")));
        // RPSLS should execute 50 rounds
        Assert.IsTrue(result.Bot1Score + result.Bot2Score <= 50);
    }

    [TestMethod]
    public async Task ExecuteMatch_ColonelBlottoGameType_ShouldUseBlottoExecutor()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.ColonelBlotto, result.GameType);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Colonel Blotto")));
        // Blotto has 5 battlefields
        Assert.IsTrue(result.Bot1Score + result.Bot2Score <= 5);
    }

    [TestMethod]
    public async Task ExecuteMatch_PenaltyKicksGameType_ShouldUsePenaltyExecutor()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.PenaltyKicks, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.PenaltyKicks, result.GameType);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Penalty Kicks")));
    }

    [TestMethod]
    public async Task ExecuteMatch_SecurityGameType_ShouldUseSecurityExecutor()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.SecurityGame, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.SecurityGame, result.GameType);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Security Game")));
    }

    [TestMethod]
    public async Task ExecuteMatch_AllGameTypes_ShouldProduceValidResults()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new CycleBot();
        var gameTypes = new[] { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame };

        foreach (var gameType in gameTypes)
        {
            // Act
            var result = await gameRunner.ExecuteMatch(bot1, bot2, gameType, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, $"Result should not be null for {gameType}");
            Assert.AreEqual(gameType, result.GameType, $"Game type should match for {gameType}");
            Assert.AreEqual(bot1.TeamName, result.Bot1Name);
            Assert.AreEqual(bot2.TeamName, result.Bot2Name);
            Assert.IsNotNull(result.MatchLog);
            Assert.IsTrue(result.MatchLog.Count > 0);
            Assert.IsNotNull(result.StartTime);
            Assert.IsNotNull(result.EndTime);
            Assert.IsTrue(result.Duration.TotalMilliseconds >= 0);
        }
    }

    [TestMethod]
    public async Task ValidateBot_AllGameTypes_ShouldValidateCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var validBot = new RockBot();
        var gameTypes = new[] { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame };

        foreach (var gameType in gameTypes)
        {
            // Act
            var isValid = await gameRunner.ValidateBot(validBot, gameType);

            // Assert
            Assert.IsTrue(isValid, $"Bot should be valid for {gameType}");
        }
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public async Task ExecuteMatch_InvalidGameType_ShouldThrowException()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();
        var bot2 = new PaperBot();
        var invalidGameType = (GameType)999; // Invalid game type

        // Act
        await gameRunner.ExecuteMatch(bot1, bot2, invalidGameType, CancellationToken.None);

        // Assert - exception expected
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task ExecuteMatch_NullBot1_ShouldThrowException()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot2 = new PaperBot();

        // Act
        await gameRunner.ExecuteMatch(null!, bot2, GameType.RPSLS, CancellationToken.None);

        // Assert - exception expected
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task ExecuteMatch_NullBot2_ShouldThrowException()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var bot1 = new RockBot();

        // Act
        await gameRunner.ExecuteMatch(bot1, null!, GameType.RPSLS, CancellationToken.None);

        // Assert - exception expected
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task Constructor_NullConfig_ShouldThrowException()
    {
        await Task.Yield();
        var gameRunner = new Core.GameRunner.GameRunner(null!);
    }
}
