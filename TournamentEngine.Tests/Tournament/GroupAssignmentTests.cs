using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Tests.Helpers;

namespace TournamentEngine.Tests.Tournament;

[TestClass]
public class GroupAssignmentTests
{
    [TestMethod]
    public void CreateGroups_100Bots_Creates10EqualGroups()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(100, GameType.RPSLS);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(new MockGameRunner(), new ScoringSystem());
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        Assert.AreEqual(10, groups.Count);
        Assert.IsTrue(groups.All(g => g.Bots.Count == 10));
    }
    
    [TestMethod]
    public void CreateGroups_75Bots_HandlesNonEvenSplit()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(75, GameType.RPSLS);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(new MockGameRunner(), new ScoringSystem());
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        Assert.AreEqual(10, groups.Count);
        
        // 75 bots / 10 groups = 7-8 per group
        var groupSizes = groups.Select(g => g.Bots.Count).ToList();
        Assert.IsTrue(groupSizes.All(size => size >= 7 && size <= 8));
        Assert.AreEqual(75, groupSizes.Sum()); // Total still 75
    }
    
    [TestMethod]
    public void CreateGroups_MultipleRuns_ProducesRandomDistribution()
    {
        // Arrange
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(new MockGameRunner(), new ScoringSystem());
        
        // Act - Run 5 times
        var assignments = new List<Dictionary<string, string>>();
        for (int i = 0; i < 5; i++)
        {
            var bots = TestHelpers.CreateDummyBots(30, GameType.RPSLS);
            var groups = engine.CreateInitialGroups(bots, config);
            var assignment = new Dictionary<string, string>();
            
            foreach (var group in groups)
            {
                foreach (var bot in group.Bots)
                {
                    assignment[bot.TeamName] = group.GroupId;
                }
            }
            assignments.Add(assignment);
        }
        
        // Assert - At least one bot should be in different groups across runs
        bool foundDifference = false;
        var firstBotName = "Team1";
        
        var groupsForFirstBot = assignments.Select(a => a[firstBotName]).Distinct().ToList();
        foundDifference = groupsForFirstBot.Count > 1;
        
        Assert.IsTrue(foundDifference, "Group assignment should be randomized");
    }
    
    [TestMethod]
    public void CreateGroups_AllBotsAssigned_NoDuplicates()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(50, GameType.RPSLS);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(new MockGameRunner(), new ScoringSystem());
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        var allAssignedBots = groups.SelectMany(g => g.Bots).ToList();
        Assert.AreEqual(50, allAssignedBots.Count);
        
        var uniqueBots = allAssignedBots.Select(b => b.TeamName).Distinct().ToList();
        Assert.AreEqual(50, uniqueBots.Count, "No duplicates allowed");
    }
    
    [TestMethod]
    public void CreateGroups_LessBotsThanGroups_CreatesFewerGroups()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(5, GameType.RPSLS);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(new MockGameRunner(), new ScoringSystem());
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert - Should create only 5 groups (one per bot) or handle gracefully
        Assert.IsTrue(groups.Count <= 5);
        Assert.AreEqual(5, groups.SelectMany(g => g.Bots).Count());
    }
}
