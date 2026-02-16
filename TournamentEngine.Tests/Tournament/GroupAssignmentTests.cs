using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;

namespace TournamentEngine.Tests.Tournament;

[TestClass]
public class GroupAssignmentTests
{
    private List<BotInfo> CreateTestBots(int count)
    {
        var bots = new List<BotInfo>();
        for (int i = 0; i < count; i++)
        {
            bots.Add(new BotInfo
            {
                TeamName = $"Bot{i + 1}",
                FolderPath = $"/bots/bot{i + 1}",
                IsValid = true
            });
        }
        return bots;
    }

    [TestMethod]
    public void CreateGroups_100Bots_Creates10EqualGroups()
    {
        // Arrange
        var bots = CreateTestBots(100);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        //Assert
        Assert.AreEqual(10, groups.Count);
        Assert.IsTrue(groups.All(g => g.Bots.Count == 10));
    }
    
    [TestMethod]
    public void CreateGroups_75Bots_HandlesNonEvenSplit()
    {
        // Arrange
        var bots = CreateTestBots(75);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
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
        var bots = CreateTestBots(30);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act - Run 5 times
        var assignments = new List<Dictionary<string, string>>();
        for (int i = 0; i < 5; i++)
        {
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
        var firstBot = bots[0].TeamName;
        
        var groupsForFirstBot = assignments.Select(a => a[firstBot]).Distinct().ToList();
        foundDifference = groupsForFirstBot.Count > 1;
        
        Assert.IsTrue(foundDifference, "Group assignment should be randomized");
    }
    
    [TestMethod]
    public void CreateGroups_AllBotsAssigned_NoDuplicates()
    {
        // Arrange
        var bots = CreateTestBots(50);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
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
        var bots = CreateTestBots(5);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert - Should create only 5 groups (one per bot) or handle gracefully
        Assert.IsTrue(groups.Count <= 5);
        Assert.AreEqual(5, groups.SelectMany(g => g.Bots).Count());
    }
}
