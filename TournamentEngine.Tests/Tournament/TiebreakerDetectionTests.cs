namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// TDD Tests for Tiebreaker Detection (Phase 3.5)
/// Tests for identifying when bots are tied and need tiebreaker matches
/// </summary>
[TestClass]
public class TiebreakerDetectionTests
{
    [TestMethod]
    public void DetectTies_2BotsWithSameScore_ReturnsTie()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 10 },
            { "Bot2", 10 },
            { "Bot3", 5 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count, "Should detect one tie group");
        Assert.AreEqual(2, ties[0].Count, "Tie should include 2 bots");
        CollectionAssert.Contains(ties[0], "Bot1");
        CollectionAssert.Contains(ties[0], "Bot2");
    }
    
    [TestMethod]
    public void DetectTies_3WayTie_Returns3BotGroup()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 12 },
            { "Bot2", 12 },
            { "Bot3", 12 },
            { "Bot4", 8 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count, "Should detect one tie group");
        Assert.AreEqual(3, ties[0].Count, "Tie should include 3 bots");
        CollectionAssert.Contains(ties[0], "Bot1");
        CollectionAssert.Contains(ties[0], "Bot2");
        CollectionAssert.Contains(ties[0], "Bot3");
    }
    
    [TestMethod]
    public void DetectTies_NoTies_ReturnsEmpty()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 15 },
            { "Bot2", 10 },
            { "Bot3", 5 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(0, ties.Count, "Should detect no ties");
    }

    [TestMethod]
    public void DetectTies_MultipleTieGroups_DetectsAll()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 15 },
            { "Bot2", 15 },
            { "Bot3", 10 },
            { "Bot4", 10 },
            { "Bot5", 5 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(2, ties.Count, "Should detect two separate tie groups");
        
        // Find the tie at score 15
        var highTie = ties.FirstOrDefault(t => t.Contains("Bot1"));
        Assert.IsNotNull(highTie);
        Assert.AreEqual(2, highTie.Count);
        CollectionAssert.Contains(highTie, "Bot1");
        CollectionAssert.Contains(highTie, "Bot2");
        
        // Find the tie at score 10
        var midTie = ties.FirstOrDefault(t => t.Contains("Bot3"));
        Assert.IsNotNull(midTie);
        Assert.AreEqual(2, midTie.Count);
        CollectionAssert.Contains(midTie, "Bot3");
        CollectionAssert.Contains(midTie, "Bot4");
    }

    [TestMethod]
    public void DetectTies_AllBotsTied_ReturnsOneGroup()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 10 },
            { "Bot2", 10 },
            { "Bot3", 10 },
            { "Bot4", 10 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count, "All bots tied should form one group");
        Assert.AreEqual(4, ties[0].Count, "Group should include all 4 bots");
    }

    [TestMethod]
    public void DetectTies_TopScoreTied_RequiresTiebreaker()
    {
        // Arrange - This is the most important case: when top scorers are tied
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 20 },
            { "Bot2", 20 },
            { "Bot3", 15 },
            { "Bot4", 10 }
        };
        
        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count, "Should detect tie at top");
        Assert.AreEqual(2, ties[0].Count, "Top tie includes 2 bots");
        
        // Verify only top scorers are in the tie
        var topTie = ties[0];
        CollectionAssert.Contains(topTie, "Bot1");
        CollectionAssert.Contains(topTie, "Bot2");
        CollectionAssert.DoesNotContain(topTie, "Bot3");
    }
}
