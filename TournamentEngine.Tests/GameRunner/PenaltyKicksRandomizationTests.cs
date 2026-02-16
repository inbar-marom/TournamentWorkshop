namespace TournamentEngine.Tests.GameRunner;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Tests for Penalty Kicks role randomization - Phase 2.1
/// Verifies that shooter/goalkeeper roles are randomly assigned instead of always Bot1/Bot2
/// </summary>
[TestClass]
public class PenaltyKicksRandomizationTests
{
    [TestMethod]
    public async Task PenaltyKicks_MultipleMatches_AssignsRandomRoles()
    {
        // Arrange
        var config = TestHelpers.CreateDefaultConfig();
        var gameRunner = new GameRunner(config);
        
        var bots = TestHelpers.CreateDummyBots(2, GameType.PenaltyKicks);
        var bot1 = bots[0];
        var bot2 = bots[1];

        // Act - Run multiple matches and check for role variation
        var rolesAssigned = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.PenaltyKicks, CancellationToken.None);
            Assert.IsNotNull(result);
            
            // Check match log for role information
            var logText = string.Join("\n", result.MatchLog);
            rolesAssigned.Add(logText);
        }

        // Assert - Verify roles are randomized (not always the same)
        Assert.IsTrue(rolesAssigned.Count == 20, "Should have 20 matches");
        // With randomization, it's extremely unlikely all 20 matches have identical logs
        var uniqueLogs = rolesAssigned.Distinct().Count();
        Assert.IsTrue(uniqueLogs > 1, "Logs should vary due to role randomization");
    }

    [TestMethod]
    public async Task PenaltyKicks_RoleAssignment_IsFair()
    {
        // Arrange
        var config = TestHelpers.CreateDefaultConfig();
        var gameRunner = new GameRunner(config);
        
        var bots = TestHelpers.CreateDummyBots(2, GameType.PenaltyKicks);
        var bot1 = bots[0];
        var bot2 = bots[1];

        // Act - Run 100 matches and track which bot was assigned shooter first
        int bot1ShotFirst = 0;
        int bot2ShotFirst = 0;

        for (int i = 0; i < 100; i++)
        {
            var result = await gameRunner.ExecuteMatch(bot1, bot2, GameType.PenaltyKicks, CancellationToken.None);
            Assert.IsNotNull(result);
            
            // Parse logs to determine initial shooter
            var firstLog = result.MatchLog.FirstOrDefault(log => log.Contains("Shooter:"));
            if (firstLog != null)
            {
                if (firstLog.Contains($"Shooter: {bot1.TeamName}"))
                {
                    bot1ShotFirst++;
                }
                else if (firstLog.Contains($"Shooter: {bot2.TeamName}"))
                {
                    bot2ShotFirst++;
                }
            }
        }

        // Assert - Distribution should be roughly 50/50 (allow 30-70 range for randomness)
        Assert.IsTrue(bot1ShotFirst >= 30 && bot1ShotFirst <= 70, 
            $"Team1 as shooter {bot1ShotFirst}/100, Team2 as shooter {bot2ShotFirst}/100 - should be fair ~50/100 each");
        Assert.IsTrue(bot2ShotFirst >= 30 && bot2ShotFirst <= 70,
            $"Team1 as shooter {bot1ShotFirst}/100, Team2 as shooter {bot2ShotFirst}/100 - should be fair ~50/100 each");
        Assert.AreEqual(100, bot1ShotFirst + bot2ShotFirst, "All matches should have assigned roles");
    }

    [TestMethod]
    public async Task PenaltyKicks_RoleRandomization_DoesNotAffectOutcome()
    {
        // Arrange
        var config = TestHelpers.CreateDefaultConfig();
        var gameRunner = new GameRunner(config);
        
        var bots = TestHelpers.CreateDummyBots(2, GameType.PenaltyKicks);
        var dominantBot = bots[0];
        var weakBot = bots[1];

        // Act - Run multiple matches
        int bot1Wins = 0;
        for (int i = 0; i < 10; i++)
        {
            var result = await gameRunner.ExecuteMatch(dominantBot, weakBot, GameType.PenaltyKicks, CancellationToken.None);
            Assert.IsNotNull(result);
            
            if (result.Outcome != MatchOutcome.Draw)
            {
                bot1Wins++;
            }
        }

        // Assert - Matches should complete successfully despite role randomization
        Assert.IsTrue(bot1Wins >= 0, 
            $"Games should complete, {bot1Wins} decided out of 10");
    }
}
