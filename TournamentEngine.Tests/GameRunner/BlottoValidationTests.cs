using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentEngine.Tests.GameRunner;

[TestClass]
public class BlottoValidationTests
{
    private static TournamentConfig CreateTestConfig()
    {
        return new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1)
        };
    }

    [TestMethod]
    public async Task ExecuteMatch_ValidAllocations_ShouldDetermineWinner()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot(); // Allocates [20, 20, 20, 20, 20]
        var cycleBot = new CycleBot(); // Also allocates [20, 20, 20, 20, 20]

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, cycleBot, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.ColonelBlotto, result.GameType);
        Assert.AreEqual(rockBot.TeamName, result.Bot1Name);
        Assert.AreEqual(cycleBot.TeamName, result.Bot2Name);
        Assert.AreEqual(MatchOutcome.Draw, result.Outcome); // Equal allocations = draw
        Assert.IsNull(result.WinnerName);
        Assert.AreEqual(0, result.Bot1Score);
        Assert.AreEqual(0, result.Bot2Score);
        Assert.IsNotNull(result.MatchLog);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Colonel Blotto")));
    }

    [TestMethod]
    public async Task ExecuteMatch_InvalidAllocation_ShouldAwardWinToValidBot()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot(); // Valid allocation
        var faultyBot = new FaultyBot(); // Invalid allocation (sums to 150)

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, faultyBot, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MatchOutcome.Player1Wins, result.Outcome);
        Assert.AreEqual(rockBot.TeamName, result.WinnerName);
        Assert.AreEqual(5, result.Bot1Score); // Wins all 5 battlefields
        Assert.AreEqual(0, result.Bot2Score);
        Assert.IsTrue(result.Errors.Count > 0);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid allocation") || e.Contains("sum")));
    }

    [TestMethod]
    public async Task ExecuteMatch_TimeoutBot_ShouldHandleTimeout()
    {
        // Arrange
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromMilliseconds(100) // Short timeout
        };
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var timeoutBot = new TimeoutBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, timeoutBot, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MatchOutcome.Player1Wins, result.Outcome);
        Assert.AreEqual(rockBot.TeamName, result.WinnerName);
        Assert.IsTrue(result.Errors.Count > 0);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Timeout")));
    }

    [TestMethod]
    public async Task ExecuteMatch_BothInvalid_ShouldUseDeterministicRandom()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var faultyBot1 = new FaultyBot();
        var faultyBot2 = new FaultyBot();

        // Act
        var result = await gameRunner.ExecuteMatch(faultyBot1, faultyBot2, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // One of them should win deterministically
        Assert.IsTrue(result.Outcome == MatchOutcome.Player1Wins || 
                     result.Outcome == MatchOutcome.Player2Wins || 
                     result.Outcome == MatchOutcome.BothError);
        Assert.IsTrue(result.Errors.Count >= 2); // Both should have errors
    }

    [TestMethod]
    public async Task ValidateBot_ValidBlottoBot_ShouldReturnTrue()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();

        // Act
        var isValid = await gameRunner.ValidateBot(rockBot, GameType.ColonelBlotto);

        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public async Task ExecuteMatch_RandomBot_ShouldProduceValidAllocation()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var randomBot = new RandomBot();
        var rockBot = new RockBot();

        // Act
        var result = await gameRunner.ExecuteMatch(randomBot, rockBot, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(GameType.ColonelBlotto, result.GameType);
        // Random bot should produce valid allocation
        Assert.IsTrue(result.Outcome == MatchOutcome.Player1Wins || 
                     result.Outcome == MatchOutcome.Player2Wins || 
                     result.Outcome == MatchOutcome.Draw);
        Assert.AreEqual(5, result.Bot1Score + result.Bot2Score); // Total of 5 battlefields decided
    }

    [TestMethod]
    public async Task ExecuteMatch_MatchLog_ShouldContainBattlefieldResults()
    {
        // Arrange
        var config = CreateTestConfig();
        var gameRunner = new Core.GameRunner.GameRunner(config);
        var rockBot = new RockBot();
        var paperBot = new PaperBot();

        // Act
        var result = await gameRunner.ExecuteMatch(rockBot, paperBot, GameType.ColonelBlotto, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result.MatchLog);
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Battlefield 1")));
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Battlefield 5")));
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("allocation")));
        Assert.IsTrue(result.MatchLog.Any(line => line.Contains("Final Result")));
    }
}
