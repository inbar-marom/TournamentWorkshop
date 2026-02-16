namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Linq;

/// <summary>
/// TDD Tests for Multi-Game Tournament Execution (Phase 3.3)
/// Tests tournament running all 4 game types sequentially
/// </summary>
[TestClass]
public class MultiGameExecutionTests
{
    [TestMethod]
    public void MultiGameTournament_Initialization_CreatesFourEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        
        // Act
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Assert
        Assert.AreEqual(4, tournament.Events.Count, "Should create 4 events for 4 game types");
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.RPSLS));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.ColonelBlotto));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.PenaltyKicks));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.SecurityGame));
    }

    [TestMethod]
    public void MultiGameTournament_AllEvents_ShareSameBots()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        
        // Act
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Assert
        foreach (var eventInfo in tournament.Events.Values)
        {
            Assert.AreEqual(20, eventInfo.Bots.Count, "All events should have same bot count");
            
            // Verify all bot names match
            var eventBotNames = eventInfo.Bots.Select(b => b.TeamName).OrderBy(n => n).ToList();
            var originalBotNames = bots.Select(b => b.TeamName).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(originalBotNames, eventBotNames, "All events should have same bots");
        }
    }

    [TestMethod]
    public void MultiGameTournament_GetCurrentEvent_ReturnsFirstIncompleteEvent()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Act - Initially all events are NotStarted
        var currentEvent = tournament.GetCurrentEvent();

        // Assert - Should return RPSLS (first in order)
        Assert.IsNotNull(currentEvent, "Should have a current event");
        Assert.AreEqual(GameType.RPSLS, currentEvent.GameType, "First event should be RPSLS");
    }

    [TestMethod]
    public void MultiGameTournament_GetCurrentEvent_SkipsCompletedEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Mark RPSLS as completed
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;

        // Act
        var currentEvent = tournament.GetCurrentEvent();

        // Assert - Should skip to ColonelBlotto
        Assert.IsNotNull(currentEvent);
        Assert.AreEqual(GameType.ColonelBlotto, currentEvent.GameType, 
            "Should skip completed RPSLS and return ColonelBlotto");
    }

    [TestMethod]
    public void MultiGameTournament_IsComplete_ReturnsFalseWhenEventsRemain()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Complete 3 out of 4 events
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.Completed;
        tournament.Events[GameType.PenaltyKicks].State = TournamentState.Completed;

        // Act
        var isComplete = tournament.IsComplete();

        // Assert
        Assert.IsFalse(isComplete, "Tournament should not be complete with SecurityGame remaining");
    }

    [TestMethod]
    public void MultiGameTournament_IsComplete_ReturnsTrueWhenAllEventsComplete()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Complete all events
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.Completed;
        tournament.Events[GameType.PenaltyKicks].State = TournamentState.Completed;
        tournament.Events[GameType.SecurityGame].State = TournamentState.Completed;

        // Act
        var isComplete = tournament.IsComplete();

        // Assert
        Assert.IsTrue(isComplete, "Tournament should be complete when all 4 events are done");
    }

    [TestMethod]
    public void MultiGameTournament_GetAggregateScores_ReturnsEmptyForNoCompletedEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Act
        var scores = tournament.GetAggregateScores();

        // Assert
        Assert.AreEqual(0, scores.Count, "No completed events should yield no scores");
    }

    [TestMethod]
    public void MultiGameTournament_GetAggregateScores_SumsWinsAcrossEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(3, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Add match results to RPSLS event
        var rpslsEvent = tournament.Events[GameType.RPSLS];
        rpslsEvent.State = TournamentState.Completed;
        rpslsEvent.MatchResults.Add(new MatchResult
        {
            Bot1Name = "Team1",
            Bot2Name = "Team2",
            GameType = GameType.RPSLS,
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "Team1"
        });
        rpslsEvent.MatchResults.Add(new MatchResult
        {
            Bot1Name = "Team1",
            Bot2Name = "Team3",
            GameType = GameType.RPSLS,
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "Team1"
        });

        // Add match results to ColonelBlotto event
        var blottoEvent = tournament.Events[GameType.ColonelBlotto];
        blottoEvent.State = TournamentState.Completed;
        blottoEvent.MatchResults.Add(new MatchResult
        {
            Bot1Name = "Team1",
            Bot2Name = "Team2",
            GameType = GameType.ColonelBlotto,
            Outcome = MatchOutcome.Player2Wins,
            WinnerName = "Team2"
        });

        // Act
        var scores = tournament.GetAggregateScores();

        // Assert
        Assert.AreEqual(2, scores["Team1"], "Team1 should have 2 wins (both from RPSLS)");
        Assert.AreEqual(1, scores["Team2"], "Team2 should have 1 win (from Blotto)");
    }

    [TestMethod]
    public void MultiGameTournament_EventOrder_FollowsDefinedSequence()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        var expectedOrder = new[]
        {
            GameType.RPSLS,
            GameType.ColonelBlotto,
            GameType.PenaltyKicks,
            GameType.SecurityGame
        };

        // Act & Assert - Verify events are processed in order
        for (int i = 0; i < expectedOrder.Length; i++)
        {
            var currentEvent = tournament.GetCurrentEvent();
            Assert.IsNotNull(currentEvent, $"Event {i} should exist");
            Assert.AreEqual(expectedOrder[i], currentEvent.GameType, 
                $"Event {i} should be {expectedOrder[i]}");
            
            // Mark current event as completed to move to next
            currentEvent.State = TournamentState.Completed;
        }

        // After all events completed, GetCurrentEvent should return null
        Assert.IsNull(tournament.GetCurrentEvent(), "All events completed, should return null");
    }

    [TestMethod]
    public void MultiGameTournament_WithCustomGameTypes_CreatesCorrectEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = new TournamentConfig
        {
            ImportTimeout = TimeSpan.FromSeconds(5),
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            LogLevel = "INFO",
            LogFilePath = "test_tournament.log",
            BotsDirectory = "test_bots",
            ResultsFilePath = "test_results.json",
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto } //
        };

        // Act
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Assert
        Assert.AreEqual(2, tournament.Events.Count, "Should create only 2 events for custom config");
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.RPSLS));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.ColonelBlotto));
        Assert.IsFalse(tournament.Events.ContainsKey(GameType.PenaltyKicks));
        Assert.IsFalse(tournament.Events.ContainsKey(GameType.SecurityGame));
    }
}
