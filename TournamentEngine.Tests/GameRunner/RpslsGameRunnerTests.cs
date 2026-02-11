using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentEngine.Tests.GameRunner;

[TestClass]
public class RpslsGameRunnerTests
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
    public async Task ExecuteMatch_RockVsPaper_PaperShouldWin()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var paperBot = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, paperBot, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.RPSLS, result.GameType);
        Assert.AreEqual(rockBot.TeamName, result.Bot1Name);
        Assert.AreEqual(paperBot.TeamName, result.Bot2Name);
        Assert.AreEqual(MatchOutcome.Player2Wins, result.Outcome);
        Assert.AreEqual(paperBot.TeamName, result.WinnerName);
        Assert.AreEqual(0, result.Bot1Score); // Rock never wins against Paper
        Assert.AreEqual(50, result.Bot2Score); // Paper wins all 50 rounds
        Assert.IsNotNull(result.MatchLog);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("RPSLS Match")));
    }

    [TestMethod]
    public async Task ExecuteMatch_SameBots_ShouldResultInDraw()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot1 = new RockBot();
        var rockBot2 = new RockBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot1, rockBot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MatchOutcome.Draw, result.Outcome);
        Assert.IsNull(result.WinnerName);
        Assert.AreEqual(0, result.Bot1Score);
        Assert.AreEqual(0, result.Bot2Score);
    }

    [TestMethod]
    public async Task ExecuteMatch_FaultyBot_ShouldHandleInvalidMoves()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var faultyBot = new FaultyBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, faultyBot, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MatchOutcome.Player1Wins, result.Outcome);
        Assert.AreEqual(rockBot.TeamName, result.WinnerName);
        Assert.IsTrue(result.Errors.Count > 0, "Should have errors from faulty bot");
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid move")));
    }

    [TestMethod]
    public async Task ExecuteMatch_TimeoutBot_ShouldHandleTimeout()
    {
        // Arrange
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromMilliseconds(100), // Short timeout
            MaxRoundsRPSLS = 5 // Fewer rounds for faster test
        };
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var timeoutBot = new TimeoutBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, timeoutBot, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MatchOutcome.Player1Wins, result.Outcome);
        Assert.AreEqual(rockBot.TeamName, result.WinnerName);
        Assert.IsTrue(result.Errors.Count > 0, "Should have timeout errors");
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Timeout")));
    }

    [TestMethod]
    public async Task ExecuteMatch_RandomVsCycle_ShouldProduceValidResult()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var randomBot = new RandomBot();
        var cycleBot = new CycleBot();

        // Act
        var result = await gameRunner.ExecuteMatch(randomBot, cycleBot, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.RPSLS, result.GameType);
        Assert.AreEqual(randomBot.TeamName, result.Bot1Name);
        Assert.AreEqual(cycleBot.TeamName, result.Bot2Name);
        Assert.IsTrue(result.Outcome == MatchOutcome.Player1Wins || 
                     result.Outcome == MatchOutcome.Player2Wins || 
                     result.Outcome == MatchOutcome.Draw);
        Assert.AreEqual(50, result.Bot1Score + result.Bot2Score); // Should play all 50 rounds
        Assert.IsNotNull(result.MatchLog);
        Assert.IsTrue(result.Duration.TotalSeconds > 0);
    }

    [TestMethod]
    public async Task ExecuteMatch_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var paperBot = new PaperBot();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await gameRunner.ExecuteMatch(rockBot, paperBot, GameType.RPSLS, cts.Token);
        });
    }

    [TestMethod]
    public async Task ValidateBot_ValidRPSLSBot_ShouldReturnTrue()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();

        // Act
        var isValid = await gameRunner.ValidateBot(rockBot, GameType.RPSLS);

        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public async Task ValidateBot_NullBot_ShouldReturnFalse()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);

        // Act
        var isValid = await gameRunner.ValidateBot(null!, GameType.RPSLS);

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public async Task ExecuteMatch_BothBotsFail_ShouldUseDeterministicRandom()
    {
        // Arrange
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromMilliseconds(100),
            MaxRoundsRPSLS = 10
        };
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var faultyBot1 = new FaultyBot();
        var faultyBot2 = new FaultyBot();

        // Act
        var result = await gameRunner.ExecuteMatch(faultyBot1, faultyBot2, GameType.RPSLS, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Outcome == MatchOutcome.BothError || 
                     result.Outcome == MatchOutcome.Player1Wins || 
                     result.Outcome == MatchOutcome.Player2Wins);
        Assert.IsTrue(result.Errors.Count > 0);
        // Should have deterministic scoring even with both bots failing
        Assert.IsTrue(result.Bot1Score + result.Bot2Score <= 10);
    }
}
