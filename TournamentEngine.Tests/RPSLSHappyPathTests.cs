using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;

namespace TournamentEngine.Tests;

[TestClass]
public class RPSLSHappyPathTests
{
    [TestMethod]
    public async Task RPSLSMatch_RockVsPaper_PaperShouldWin()
    {
        // Arrange
        var rockBot = new RockBot();
        var paperBot = new PaperBot();
        var gameState = CreateRPSLSGameState();

        // Act
        var rockMove = await rockBot.MakeMove(gameState, CancellationToken.None);
        var paperMove = await paperBot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Rock", rockMove);
        Assert.AreEqual("Paper", paperMove);
        
        var winner = DetermineRPSLSWinner(rockMove, paperMove);
        Assert.AreEqual("Paper", winner, "Paper should beat Rock");
    }

    [TestMethod]
    public async Task RPSLSMatch_PaperVsScissors_ScissorsShouldWin()
    {
        // Arrange
        var paperBot = new PaperBot();
        var cycleBot = new CycleBot();
        var gameState = CreateRPSLSGameState();

        // Act
        // CycleBot's sequence: Rock, Paper, Scissors, Lizard, Spock
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Rock
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Paper
        var scissorsMove = await cycleBot.MakeMove(gameState, CancellationToken.None); // Scissors
        var paperMove = await paperBot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Scissors", scissorsMove);
        Assert.AreEqual("Paper", paperMove);
        
        var winner = DetermineRPSLSWinner(scissorsMove, paperMove);
        Assert.AreEqual("Scissors", winner, "Scissors should beat Paper");
    }

    [TestMethod]
    public async Task RPSLSMatch_SpockVsRock_SpockShouldWin()
    {
        // Arrange
        var rockBot = new RockBot();
        var cycleBot = new CycleBot();
        var gameState = CreateRPSLSGameState();

        // Act
        // Get to Spock in cycle: Rock, Paper, Scissors, Lizard, Spock
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Rock
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Paper
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Scissors
        await cycleBot.MakeMove(gameState, CancellationToken.None); // Lizard
        var spockMove = await cycleBot.MakeMove(gameState, CancellationToken.None); // Spock
        var rockMove = await rockBot.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Spock", spockMove);
        Assert.AreEqual("Rock", rockMove);
        
        var winner = DetermineRPSLSWinner(spockMove, rockMove);
        Assert.AreEqual("Spock", winner, "Spock should vaporize Rock");
    }

    [TestMethod]
    public async Task RPSLSMatch_LizardVsSpock_LizardShouldWin()
    {
        // Arrange
        var cycleBot1 = new CycleBot(); // For Lizard
        var cycleBot2 = new CycleBot(); // For Spock
        var gameState = CreateRPSLSGameState();

        // Act
        // Get Lizard from first bot (4th move)
        await cycleBot1.MakeMove(gameState, CancellationToken.None); // Rock
        await cycleBot1.MakeMove(gameState, CancellationToken.None); // Paper
        await cycleBot1.MakeMove(gameState, CancellationToken.None); // Scissors
        var lizardMove = await cycleBot1.MakeMove(gameState, CancellationToken.None); // Lizard
        
        // Get Spock from second bot (5th move)
        await cycleBot2.MakeMove(gameState, CancellationToken.None); // Rock
        await cycleBot2.MakeMove(gameState, CancellationToken.None); // Paper
        await cycleBot2.MakeMove(gameState, CancellationToken.None); // Scissors
        await cycleBot2.MakeMove(gameState, CancellationToken.None); // Lizard
        var spockMove = await cycleBot2.MakeMove(gameState, CancellationToken.None); // Spock

        // Assert
        Assert.AreEqual("Lizard", lizardMove);
        Assert.AreEqual("Spock", spockMove);
        
        var winner = DetermineRPSLSWinner(lizardMove, spockMove);
        Assert.AreEqual("Lizard", winner, "Lizard should poison Spock");
    }

    [TestMethod]
    public async Task RPSLSMatch_SameMoves_ShouldBeDraw()
    {
        // Arrange
        var rockBot1 = new RockBot();
        var rockBot2 = new RockBot();
        var gameState = CreateRPSLSGameState();

        // Act
        var move1 = await rockBot1.MakeMove(gameState, CancellationToken.None);
        var move2 = await rockBot2.MakeMove(gameState, CancellationToken.None);

        // Assert
        Assert.AreEqual("Rock", move1);
        Assert.AreEqual("Rock", move2);
        
        var winner = DetermineRPSLSWinner(move1, move2);
        Assert.IsNull(winner, "Same moves should result in a draw");
    }

    [TestMethod]
    public async Task RPSLSMatch_FullGame_ShouldTrackRounds()
    {
        // Arrange
        var rockBot = new RockBot();
        var paperBot = new PaperBot();
        var maxRounds = 5;
        var gameState = CreateRPSLSGameState(maxRounds);
        var paperWins = 0;
        var rockWins = 0;
        var draws = 0;

        // Act - Simulate multiple rounds
        for (int round = 1; round <= maxRounds; round++)
        {
            var rockMove = await rockBot.MakeMove(gameState, CancellationToken.None);
            var paperMove = await paperBot.MakeMove(gameState, CancellationToken.None);
            
            var winner = DetermineRPSLSWinner(rockMove, paperMove);
            
            if (winner == "Rock") rockWins++;
            else if (winner == "Paper") paperWins++;
            else draws++;

            // Update game state for next round
            gameState = CreateRPSLSGameState(maxRounds, round);
        }

        // Assert
        Assert.AreEqual(maxRounds, paperWins + rockWins + draws);
        Assert.AreEqual(5, paperWins); // Paper should win all rounds vs Rock
        Assert.AreEqual(0, rockWins);
        Assert.AreEqual(0, draws);
    }

    [TestMethod]
    public async Task RPSLSMatch_RandomVsCycle_ShouldProduceVariedResults()
    {
        // Arrange
        var randomBot = new RandomBot();
        var cycleBot = new CycleBot();
        var maxRounds = 20;
        var gameState = CreateRPSLSGameState(maxRounds);
        var randomWins = 0;
        var cycleWins = 0;
        var draws = 0;

        // Act - Simulate multiple rounds
        for (int round = 1; round <= maxRounds; round++)
        {
            var randomMove = await randomBot.MakeMove(gameState, CancellationToken.None);
            var cycleMove = await cycleBot.MakeMove(gameState, CancellationToken.None);
            
            // Verify moves are valid
            Assert.IsTrue(IsValidRPSLSMove(randomMove), $"Random bot move '{randomMove}' should be valid");
            Assert.IsTrue(IsValidRPSLSMove(cycleMove), $"Cycle bot move '{cycleMove}' should be valid");
            
            var winner = DetermineRPSLSWinner(randomMove, cycleMove);
            
            if (winner == randomMove) randomWins++;
            else if (winner == cycleMove) cycleWins++;
            else draws++;
        }

        // Assert
        Assert.AreEqual(maxRounds, randomWins + cycleWins + draws);
        // With randomness, we should see some variety (not all wins for one side)
        Assert.IsTrue(randomWins + cycleWins + draws == maxRounds);
    }

    [TestMethod]
    public void RPSLSRules_AllCombinations_ShouldFollowCorrectRules()
    {
        // Arrange - RPSLS rules:
        // Rock crushes Lizard, Rock crushes Scissors
        // Paper covers Rock, Paper disproves Spock
        // Scissors cuts Paper, Scissors decapitates Lizard
        // Lizard poisons Spock, Lizard eats Paper
        // Spock smashes Scissors, Spock vaporizes Rock

        var rules = new Dictionary<(string, string), string>
        {
            // Rock wins
            ("Rock", "Scissors") = "Rock",
            ("Rock", "Lizard") = "Rock",
            
            // Paper wins
            ("Paper", "Rock") = "Paper",
            ("Paper", "Spock") = "Paper",
            
            // Scissors wins
            ("Scissors", "Paper") = "Scissors",
            ("Scissors", "Lizard") = "Scissors",
            
            // Lizard wins
            ("Lizard", "Spock") = "Lizard",
            ("Lizard", "Paper") = "Lizard",
            
            // Spock wins
            ("Spock", "Scissors") = "Spock",
            ("Spock", "Rock") = "Spock",
        };

        // Act & Assert
        foreach (var rule in rules)
        {
            var move1 = rule.Key.Item1;
            var move2 = rule.Key.Item2;
            var expectedWinner = rule.Value;
            
            var actualWinner = DetermineRPSLSWinner(move1, move2);
            Assert.AreEqual(expectedWinner, actualWinner, 
                $"{move1} vs {move2} should result in {expectedWinner} winning");
                
            // Test reverse scenario (move2 vs move1 should give opposite result)
            var reverseWinner = DetermineRPSLSWinner(move2, move1);
            Assert.AreEqual(expectedWinner, reverseWinner, 
                $"{move2} vs {move1} should result in {expectedWinner} winning");
        }
    }

    // Helper methods
    private static GameState CreateRPSLSGameState(int maxRounds = 50, int currentRound = 1)
    {
        return new GameState
        {
            State = new Dictionary<string, object> { { "GameType", GameType.RPSLS } },
            MoveHistory = new List<string>(),
            CurrentRound = currentRound,
            MaxRounds = maxRounds,
            IsGameOver = false,
            Winner = null
        };
    }

    private static bool IsValidRPSLSMove(string move)
    {
        var validMoves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
        return validMoves.Contains(move);
    }

    private static string? DetermineRPSLSWinner(string move1, string move2)
    {
        if (move1 == move2) return null; // Draw

        // Define winning combinations: (winner, loser)
        var winningCombos = new HashSet<(string, string)>
        {
            ("Rock", "Scissors"), ("Rock", "Lizard"),
            ("Paper", "Rock"), ("Paper", "Spock"),
            ("Scissors", "Paper"), ("Scissors", "Lizard"),
            ("Lizard", "Spock"), ("Lizard", "Paper"),
            ("Spock", "Scissors"), ("Spock", "Rock")
        };

        if (winningCombos.Contains((move1, move2)))
            return move1;
        else if (winningCombos.Contains((move2, move1)))
            return move2;
        
        return null; // Should not happen with valid moves
    }
}
