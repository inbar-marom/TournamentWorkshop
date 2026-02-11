using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;

namespace TournamentEngine.Tests;

[TestClass]
public class DummyBotTests
{
    [TestMethod]
    public void DummyBots_ShouldImplementIBotCorrectly()
    {
        // Arrange
        var bots = new IBot[]
        {
            new RockBot(),
            new CycleBot(),
            new RandomBot(),
            new PaperBot(),
            new FaultyBot(),
            new TimeoutBot()
        };

        // Act & Assert
        foreach (var bot in bots)
        {
            Assert.IsNotNull(bot.TeamName);
            Assert.IsTrue(bot.TeamName.Length > 0);
            Assert.AreEqual(GameType.RPSLS, bot.GameType);
        }
    }

    [TestMethod]
    public async Task RockBot_MakeMove_ShouldAlwaysReturnRock()
    {
        // Arrange
        var bot = new RockBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };

        // Act
        var move1 = await bot.MakeMove(gameState, CancellationToken.None);
        var move2 = await bot.MakeMove(gameState, CancellationToken.None);
        var move3 = await bot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Rock", move1);
        Assert.AreEqual("Rock", move2);
        Assert.AreEqual("Rock", move3);
    }

    [TestMethod]
    public async Task PaperBot_MakeMove_ShouldAlwaysReturnPaper()
    {
        // Arrange
        var bot = new PaperBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };

        // Act
        var move1 = await bot.MakeMove(gameState, CancellationToken.None);
        var move2 = await bot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Paper", move1);
        Assert.AreEqual("Paper", move2);
    }

    [TestMethod]
    public async Task CycleBot_MakeMove_ShouldCycleThroughMoves()
    {
        // Arrange
        var bot = new CycleBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };
        var expectedMoves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };

        // Act & Assert
        for (int i = 0; i < expectedMoves.Length; i++)
        {
            var move = await bot.MakeMove(gameState, CancellationToken.None);
            Assert.AreEqual(expectedMoves[i], move, $"Expected {expectedMoves[i]} at position {i}");
        }

        // Test cycling back to start
        var cycleMove = await bot.MakeMove(gameState, CancellationToken.None);
        Assert.AreEqual("Rock", cycleMove);
    }

    [TestMethod]
    public async Task RandomBot_MakeMove_ShouldReturnValidMoves()
    {
        // Arrange
        var bot = new RandomBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };
        var validMoves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };

        // Act & Assert
        for (int i = 0; i < 20; i++)
        {
            var move = await bot.MakeMove(gameState, CancellationToken.None);
            Assert.IsTrue(validMoves.Contains(move));
        }
    }

    [TestMethod]
    public async Task FaultyBot_MakeMove_ShouldReturnInvalidMove()
    {
        // Arrange
        var bot = new FaultyBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };

        // Act
        var move = await bot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("InvalidMove", move);
    }

    [TestMethod]
    public async Task AllBots_AllocateTroops_ShouldReturnArrayOfLength5()
    {
        // Arrange
        var bots = new IBot[]
        {
            new RockBot(),
            new CycleBot(),
            new RandomBot(),
            new PaperBot()
        };
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 1
        };

        // Act & Assert
        foreach (var bot in bots)
        {
            var allocation = await bot.AllocateTroops(gameState, CancellationToken.None);
            
            Assert.AreEqual(5, allocation.Length, "Allocation should have 5 entries");
            Assert.IsTrue(allocation.All(x => x >= 0), $"{bot.TeamName} should not allocate negative troops");
        }
    }

    [TestMethod]
    public async Task ValidBots_AllocateTroops_ShouldSumTo100()
    {
        // Arrange
        var bots = new IBot[]
        {
            new RockBot(),
            new CycleBot(),
            new PaperBot()
        };
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 1
        };

        // Act & Assert
        foreach (var bot in bots)
        {
            var allocation = await bot.AllocateTroops(gameState, CancellationToken.None);
            Assert.AreEqual(100, allocation.Sum(), $"{bot.TeamName} should allocate exactly 100 troops");
        }
    }

    [TestMethod]
    public async Task FaultyBot_AllocateTroops_ShouldReturnInvalidAllocation()
    {
        // Arrange
        var bot = new FaultyBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 1
        };

        // Act
        var allocation = await bot.AllocateTroops(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual(5, allocation.Length);
        Assert.AreNotEqual(100, allocation.Sum());
        Assert.AreEqual(150, allocation.Sum());
    }

    [TestMethod]
    public async Task RandomBot_AllocateTroops_ShouldSumTo100()
    {
        // Arrange
        var bot = new RandomBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 1
        };

        // Act & Assert - Test multiple times due to randomness
        for (int i = 0; i < 10; i++)
        {
            var allocation = await bot.AllocateTroops(gameState, CancellationToken.None);
            
            Assert.AreEqual(5, allocation.Length);
            Assert.AreEqual(100, allocation.Sum());
            Assert.IsTrue(allocation.All(x => x >= 0), $"Random allocation {i} should not have negative values");
        }
    }

    [TestMethod]
    public async Task BotMethods_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var bot = new RockBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - should complete quickly since it's not doing async work
        var move = await bot.MakeMove(gameState, cts.Token);
        Assert.AreEqual("Rock", move);
        
        var allocation = await bot.AllocateTroops(gameState, cts.Token);
        Assert.AreEqual(5, allocation.Length);

        var penaltyDecision = await bot.MakePenaltyDecision(gameState, cts.Token);
        Assert.AreEqual("Left", penaltyDecision);

        var securityMove = await bot.MakeSecurityMove(gameState, cts.Token);
        Assert.AreEqual("Defend", securityMove);
    }

    [TestMethod]
    public async Task TimeoutBot_WithShortTimeout_ShouldThrowTaskCanceledException()
    {
        // Arrange
        var bot = new TimeoutBot();
        var gameState = new GameState
        {
            State = new Dictionary<string, object>(),
            CurrentRound = 1,
            MaxRounds = 50
        };
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)); // Short timeout

        // Act & Assert
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
        {
            await bot.MakeMove(gameState, cts.Token);
        });

        // Reset for next test
        cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
        {
            await bot.AllocateTroops(gameState, cts.Token);
        });
    }

    [TestMethod]
    public void BotTeamNames_ShouldBeUnique()
    {
        // Arrange
        var bots = new IBot[]
        {
            new RockBot(),
            new CycleBot(),
            new RandomBot(),
            new PaperBot(),
            new FaultyBot(),
            new TimeoutBot()
        };

        // Act
        var teamNames = bots.Select(b => b.TeamName).ToList();

        // Assert
        Assert.AreEqual(teamNames.Count, teamNames.Distinct().Count());
    }
}
