using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.Common;

[TestClass]
public class MultiGameTournamentTests
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
    public void MultiGameTournament_Initialization_Creates4Events()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        
        // Act
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Assert
        Assert.AreEqual(4, tournament.Events.Count);
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.RPSLS));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.ColonelBlotto));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.PenaltyKicks));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.SecurityGame));
    }
    
    [TestMethod]
    public void MultiGameTournament_AllEvents_ShareSameTournamentId()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        
        // Act
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Assert
        foreach (var eventInfo in tournament.Events.Values)
        {
            Assert.AreEqual("test-tournament", eventInfo.TournamentId);
        }
    }
    
    [TestMethod]
    public void MultiGameTournament_AllEvents_ShareSameBots()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        
        // Act
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Assert
        foreach (var eventInfo in tournament.Events.Values)
        {
            Assert.AreEqual(20, eventInfo.Bots.Count);
            for (int i = 0; i < bots.Count; i++)
            {
                Assert.AreEqual(bots[i].TeamName, eventInfo.Bots[i].TeamName);
            }
        }
    }
    
    [TestMethod]
    public void MultiGameTournament_GetCurrentEvent_ReturnsFirstPendingEvent()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Act
        var currentEvent = tournament.GetCurrentEvent();
        
        // Assert
        Assert.IsNotNull(currentEvent);
        Assert.AreEqual(GameType.RPSLS, currentEvent.GameType);
        Assert.AreEqual(TournamentState.NotStarted, currentEvent.State);
    }
    
    [TestMethod]
    public void MultiGameTournament_GetCurrentEvent_ReturnsNextAfterCompletion()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Complete RPSLS
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        
        // Act
        var currentEvent = tournament.GetCurrentEvent();
        
        // Assert
        Assert.IsNotNull(currentEvent);
        Assert.AreEqual(GameType.ColonelBlotto, currentEvent.GameType);
    }
    
    [TestMethod]
    public void MultiGameTournament_IsComplete_FalseWhenEventsRemaining()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Act & Assert
        Assert.IsFalse(tournament.IsComplete());
    }
    
    [TestMethod]
    public void MultiGameTournament_IsComplete_TrueWhenAllEventsCompleted()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        var tournament = new MultiGameTournament("test-tournament", bots, config);
        
        // Complete all events
        foreach (var evt in tournament.Events.Values)
        {
            evt.State = TournamentState.Completed;
        }
        
        // Act & Assert
        Assert.IsTrue(tournament.IsComplete());
    }
}
