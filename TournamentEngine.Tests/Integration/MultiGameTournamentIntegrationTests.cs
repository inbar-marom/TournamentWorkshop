namespace TournamentEngine.Tests.Integration;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Linq;

/// <summary>
/// Integration Tests for Phase 3: Multi-Game Tournament System
/// End-to-end tests validating the complete tournament flow
/// </summary>
[TestClass]
public class MultiGameTournamentIntegrationTests
{
    [TestMethod]
    public void MultiGameTournament_EndToEnd_CompleteFlow()
    {
        // Arrange - Create tournament with 30 bots and 4 game types
        var bots = TestHelpers.CreateDummyBotInfos(30, GameType.RPSLS);
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
            GroupCount = 10, //
            GameTypes = new List<GameType> //
            {
                GameType.RPSLS,
                GameType.ColonelBlotto,
                GameType.PenaltyKicks,
                GameType.SecurityGame
            }
        };

        // Act - Create tournament
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Assert - Verify tournament structure
        Assert.AreEqual(4, tournament.Events.Count, "Should create 4 events for 4 game types");
        Assert.AreEqual(30, tournament.Events[GameType.RPSLS].Bots.Count, "All events should have all bots");
        Assert.IsFalse(tournament.IsComplete(), "Tournament should not be complete initially");
    }

    [TestMethod]
    public void MultiGameTournament_AggregateScoring_CombinesAllEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Simulate completed events with known results
        // RPSLS: Bot1 wins 5 matches
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        for (int i = 0; i < 5; i++)
        {
            tournament.Events[GameType.RPSLS].MatchResults.Add(new MatchResult
            {
                GameType = GameType.RPSLS,
                Bot1Name = "Team1",
                Bot2Name = $"Team{i + 2}",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = "Team1",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow
            });
        }

        // Blotto: Bot1 wins 3 matches
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.Completed;
        for (int i = 0; i < 3; i++)
        {
            tournament.Events[GameType.ColonelBlotto].MatchResults.Add(new MatchResult
            {
                GameType = GameType.ColonelBlotto,
                Bot1Name = "Team1",
                Bot2Name = $"Team{i + 2}",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = "Team1",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow
            });
        }

        // Act
        var aggregateScores = tournament.GetAggregateScores();

        // Assert
        Assert.IsTrue(aggregateScores.ContainsKey("Team1"), "Team1 should have aggregate score");
        Assert.AreEqual(8, aggregateScores["Team1"], "Team1 should have 5 + 3 = 8 total wins");
    }

    [TestMethod]
    public void MultiGameTournament_TiebreakDetection_IdentifiesTiedBots()
    {
        // Arrange - Create scenario with tied bots
        var standings = new Dictionary<string, int>
        {
            { "Team1", 20 },
            { "Team2", 20 }, // Tied with Team1
            { "Team3", 18 },
            { "Team4", 15 }
        };

        // Act
        var ties = TiebreakerHelper.DetectTies(standings);
        var topTie = TiebreakerHelper.IsTopScoreTied(standings);

        // Assert
        Assert.AreEqual(1, ties.Count, "Should detect one tie group");
        Assert.AreEqual(2, ties[0].Count, "Tie should include 2 bots");
        Assert.IsTrue(topTie, "Top score should be tied");
    }

    [TestMethod]
    public void MultiGameTournament_FinalistSelection_ChoosesTop10()
    {
        // Arrange - Simulate 30 bots with aggregate scores
        var aggregateScores = new Dictionary<string, int>();
        for (int i = 1; i <= 30; i++)
        {
            aggregateScores[$"Team{i}"] = 100 - i; // Descending scores
        }

        // Act - Select top 10 for finals
        var finalists = TiebreakerHelper.SelectTopScorers(aggregateScores, 10);

        // Assert
        Assert.AreEqual(10, finalists.Count, "Should select exactly 10 finalists");
        Assert.IsTrue(finalists.Contains("Team1"), "Top scorer should be selected");
        Assert.IsTrue(finalists.Contains("Team10"), "10th place should be selected");
        Assert.IsFalse(finalists.Contains("Team11"), "11th place should not be selected");
    }

    [TestMethod]
    public void MultiGameTournament_ChampionDetermination_IdentifiesWinner()
    {
        // Arrange - Clear winner scenario
        var finalStandings = new Dictionary<string, int>
        {
            { "Team1", 25 },
            { "Team2", 22 },
            { "Team3", 20 },
            { "Team4", 18 }
        };

        // Act
        var champion = TiebreakerHelper.DetermineChampion(finalStandings);

        // Assert
        Assert.IsNotNull(champion, "Champion should be determined");
        Assert.AreEqual("Team1", champion, "Highest scorer should be champion");
    }

    [TestMethod]
    public void MultiGameTournament_ChampionTie_RequiresTiebreaker()
    {
        // Arrange - Tied for first place
        var finalStandings = new Dictionary<string, int>
        {
            { "Team1", 25 },
            { "Team2", 25 }, // Tied with Team1
            { "Team3", 20 },
            { "Team4", 18 }
        };

        // Act
        var champion = TiebreakerHelper.DetermineChampion(finalStandings);

        // Assert
        Assert.IsNull(champion, "Tied first place should return null (requires tiebreaker)");
        
        var tiedForFirst = TiebreakerHelper.GetTopScorersTied(finalStandings);
        Assert.AreEqual(2, tiedForFirst.Count, "Should identify 2 bots tied for first");
    }

    [TestMethod]
    public void MultiGameTournament_EventProgression_FollowsSequence()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var tournament = new MultiGameTournament(
            Guid.NewGuid().ToString(),
            bots,
            config);

        // Act & Assert - Verify events progress in order
        
        // Initial state: RPSLS should be current
        var currentEvent = tournament.GetCurrentEvent();
        Assert.IsNotNull(currentEvent);
        Assert.AreEqual(GameType.RPSLS, currentEvent.GameType, "First event should be RPSLS");

        // Complete RPSLS, next should be Blotto
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        currentEvent = tournament.GetCurrentEvent();
        Assert.AreEqual(GameType.ColonelBlotto, currentEvent.GameType, "Second event should be Blotto");

        // Complete Blotto, next should be Penalty
        tournament.Events[GameType.ColonelBlotto].State = TournamentState.Completed;
        currentEvent = tournament.GetCurrentEvent();
        Assert.AreEqual(GameType.PenaltyKicks, currentEvent.GameType, "Third event should be Penalty");

        // Complete Penalty, next should be Security
        tournament.Events[GameType.PenaltyKicks].State = TournamentState.Completed;
        currentEvent = tournament.GetCurrentEvent();
        Assert.AreEqual(GameType.SecurityGame, currentEvent.GameType, "Fourth event should be Security");

        // Complete Security, tournament should be complete
        tournament.Events[GameType.SecurityGame].State = TournamentState.Completed;
        Assert.IsTrue(tournament.IsComplete(), "All events completed, tournament should be complete");
        Assert.IsNull(tournament.GetCurrentEvent(), "No current event when tournament complete");
    }

    [TestMethod]
    public void MultiGameTournament_Ranking_OrdersByAggregateScore()
    {
        // Arrange
        var aggregateScores = new Dictionary<string, int>
        {
            { "Team3", 85 },
            { "Team1", 95 },
            { "Team4", 75 },
            { "Team2", 90 }
        };

        // Act
        var ranked = TiebreakerHelper.RankByScore(aggregateScores);

        // Assert
        Assert.AreEqual(4, ranked.Count);
        Assert.AreEqual("Team1", ranked[0].BotName, "#1 should be Team1");
        Assert.AreEqual(95, ranked[0].Score);
        Assert.AreEqual("Team2", ranked[1].BotName, "#2 should be Team2");
        Assert.AreEqual(90, ranked[1].Score);
        Assert.AreEqual("Team3", ranked[2].BotName, "#3 should be Team3");
        Assert.AreEqual(85, ranked[2].Score);
        Assert.AreEqual("Team4", ranked[3].BotName, "#4 should be Team4");
        Assert.AreEqual(75, ranked[3].Score);
    }
}
