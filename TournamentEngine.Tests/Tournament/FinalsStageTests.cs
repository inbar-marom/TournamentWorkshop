namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// TDD Tests for Finals Stage and Group Advancement (Phase 3.6)
/// Tests for selecting top bots from groups to advance to finals
/// </summary>
[TestClass]
public class FinalsStageTests
{
    [TestMethod]
    public void SelectTopScorers_FromAggregateScores_ReturnsTopN()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot1", 20 },
            { "Bot2", 18 },
            { "Bot3", 16 },
            { "Bot4", 14 },
            { "Bot5", 12 },
            { "Bot6", 10 },
            { "Bot7", 8 },
            { "Bot8", 6 },
            { "Bot9", 4 },
            { "Bot10", 2 }
        };
        
        int topN = 5;
        
        // Act
        var topScorers = TiebreakerHelper.SelectTopScorers(aggregateScores, topN);
        
        // Assert
        Assert.AreEqual(5, topScorers.Count, "Should select top 5 bots");
        Assert.AreEqual("Bot1", topScorers[0], "Bot1 should be #1");
        Assert.AreEqual("Bot2", topScorers[1], "Bot2 should be #2");
        Assert.AreEqual("Bot3", topScorers[2], "Bot3 should be #3");
        Assert.AreEqual("Bot4", topScorers[3], "Bot4 should be #4");
        Assert.AreEqual("Bot5", topScorers[4], "Bot5 should be #5");
    }

    [TestMethod]
    public void SelectTopScorers_WithTie_IncludesAllTiedBots()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot1", 20 },
            { "Bot2", 18 },
            { "Bot3", 18 }, // Tied with Bot2
            { "Bot4", 16 },
            { "Bot5", 14 }
        };
        
        int topN = 2;
        
        // Act - When selecting top 2, but 2nd and 3rd are tied
        var topScorers = TiebreakerHelper.SelectTopScorers(aggregateScores, topN);
        
        // Assert - Should include Bot1, Bot2, and Bot3 (tied for 2nd)
        Assert.IsTrue(topScorers.Count >= 2, "Should include at least top 2");
        Assert.IsTrue(topScorers.Contains("Bot1"), "Should include Bot1");
        Assert.IsTrue(topScorers.Contains("Bot2"), "Should include Bot2 (tied for 2nd)");
        Assert.IsTrue(topScorers.Contains("Bot3"), "Should include Bot3 (tied for 2nd)");
    }

    [TestMethod]
    public void SelectTopScorers_RequestMoreThanAvailable_ReturnsAll()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot1", 20 },
            { "Bot2", 18 },
            { "Bot3", 16 }
        };
        
        int topN = 10; // Request more than available
        
        // Act
        var topScorers = TiebreakerHelper.SelectTopScorers(aggregateScores, topN);
        
        // Assert
        Assert.AreEqual(3, topScorers.Count, "Should return all 3 available bots");
    }

    [TestMethod]
    public void SelectTopScorers_EmptyScores_ReturnsEmpty()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>();
        int topN = 5;
        
        // Act
        var topScorers = TiebreakerHelper.SelectTopScorers(aggregateScores, topN);
        
        // Assert
        Assert.AreEqual(0, topScorers.Count, "Empty scores should return empty list");
    }

    [TestMethod]
    public void RankByScore_OrdersDescending()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot3", 16 },
            { "Bot1", 20 },
            { "Bot5", 12 },
            { "Bot2", 18 },
            { "Bot4", 14 }
        };
        
        // Act
        var ranked = TiebreakerHelper.RankByScore(aggregateScores);
        
        // Assert
        Assert.AreEqual(5, ranked.Count);
        Assert.AreEqual("Bot1", ranked[0].BotName, "Highest score first");
        Assert.AreEqual(20, ranked[0].Score);
        Assert.AreEqual("Bot2", ranked[1].BotName);
        Assert.AreEqual(18, ranked[1].Score);
        Assert.AreEqual("Bot3", ranked[2].BotName);
        Assert.AreEqual(16, ranked[2].Score);
    }

    [TestMethod]
    public void DetermineChampion_FromAggregateScores_ReturnsHighestScorer()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot1", 15 },
            { "Bot2", 22 }, // Champion
            { "Bot3", 18 },
            { "Bot4", 12 }
        };
        
        // Act
        var champion = TiebreakerHelper.DetermineChampion(aggregateScores);
        
        // Assert
        Assert.AreEqual("Bot2", champion, "Bot with highest score should be champion");
    }

    [TestMethod]
    public void DetermineChampion_WithTie_ReturnsNull()
    {
        // Arrange - Top 2 bots tied
        var aggregateScores = new Dictionary<string, int>
        {
            { "Bot1", 20 },
            { "Bot2", 20 }, // Tied for first
            { "Bot3", 15 }
        };
        
        // Act
        var champion = TiebreakerHelper.DetermineChampion(aggregateScores);
        
        // Assert
        Assert.IsNull(champion, "Tied top score should return null (needs tiebreaker)");
    }

    [TestMethod]
    public void CalculateMatchesForRoundRobin_10Bots4Games_Returns180Matches()
    {
        // Arrange
        int botCount = 10;
        int gameTypes = 4;
        
        // Act
        // Round-robin for N bots: N × (N-1) / 2
        int pairings = botCount * (botCount - 1) / 2;
        int totalMatches = pairings * gameTypes;
        
        // Assert
        Assert.AreEqual(45, pairings, "10 bots should create 45 unique pairings");
        Assert.AreEqual(180, totalMatches, "45 pairings × 4 games = 180 final matches");
    }
}
