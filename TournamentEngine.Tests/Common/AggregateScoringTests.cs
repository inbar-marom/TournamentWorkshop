using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.Common;

[TestClass]
public class AggregateScoringTests
{
    [TestMethod]
    public void GetAggregateScores_MultipleEventsCompleted_SumsPointsCorrectly()
    {
        // Arrange
        var bots = CreateTestBots(10);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Simulate RPSLS results: Bot1 gets 6 points
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot1", 6);
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot2", 3);
        
        // Simulate Blotto results: Bot1 gets 4 points
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.Completed;
        AddMockResults(tournament.Events[GameType.ColonelBlotto], "Bot1", 4);
        AddMockResults(tournament.Events[GameType.ColonelBlotto], "Bot2", 5);
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.AreEqual(10, aggregateScores["Bot1"]); // 6 + 4
        Assert.AreEqual(8, aggregateScores["Bot2"]);  // 3 + 5
    }
    
    [TestMethod]
    public void GetAggregateScores_PartialCompletion_OnlyCountsCompletedEvents()
    {
        // Arrange
        var bots = CreateTestBots(10);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Only RPSLS completed
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot1", 6);
        
        // Others still in progress
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.InProgress;
        tournament.Events[GameType.PenaltyKicks].State = TournamentState.NotStarted;
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.AreEqual(6, aggregateScores["Bot1"]); // Only RPSLS counts
    }
    
    [TestMethod]
    public void GetAggregateScores_AllEventsCompleted_IncludesAllGames()
    {
        // Arrange
        var bots = CreateTestBots(10);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Complete all 4 events with different scores
        foreach (var gameType in config.GameTypes)
        {
            tournament.Events[gameType].State = TournamentState.Completed;
            AddMockResults(tournament.Events[gameType], "Bot1", 3);
        }
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.AreEqual(12, aggregateScores["Bot1"]); // 3 * 4 games
    }
    
    [TestMethod]
    public void GetAggregateScores_WithTies_HandlesMultipleBotsSameScore()
    {
        // Arrange
        var bots = CreateTestBots(10);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot1", 5);
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot2", 5);
        AddMockResults(tournament.Events[GameType.RPSLS], "Bot3", 3);
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.AreEqual(5, aggregateScores["Bot1"]);
        Assert.AreEqual(5, aggregateScores["Bot2"]);
        Assert.AreEqual(3, aggregateScores["Bot3"]);
    }
    
    [TestMethod]
    public void GetAggregateScores_NoEventsCompleted_ReturnsEmptyDictionary()
    {
        // Arrange
        var bots = CreateTestBots(10);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // All events still not started
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.AreEqual(0, aggregateScores.Count);
    }
    
    // Helper methods
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
    
    private void AddMockResults(TournamentInfo eventInfo, string botName, int points)
    {
        // Add mock match results to give bot the specified points
        // Create wins for the specified bot
        for (int i = 0; i < points; i++)
        {
            eventInfo.MatchResults.Add(new MatchResult
            {
                GameType = eventInfo.GameType,
                Bot1Name = botName,
                Bot2Name = $"Opponent{i}",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = botName,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow
            });
        }
    }
}
