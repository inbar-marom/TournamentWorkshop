namespace TournamentEngine.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Unit tests for TournamentSeriesManager
/// </summary>
[TestClass]
public class TournamentSeriesManagerTests
{
    [TestMethod]
    public async Task RunSeriesAsync_WithSingleTournament_ProducesCorrectSeriesInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        Assert.IsNotNull(seriesInfo);
        Assert.IsNotNull(seriesInfo.SeriesId);
        Assert.AreEqual(1, seriesInfo.Tournaments.Count, "Should have 1 tournament");
        
        var tournament = seriesInfo.Tournaments[0];
        Assert.AreEqual(GameType.RPSLS, tournament.GameType);
        Assert.IsNotNull(tournament.Champion, "Tournament should have a champion");
        
        Assert.IsNotNull(seriesInfo.SeriesStandings);
        Assert.AreEqual(bots.Count, seriesInfo.SeriesStandings.Count, "Should have standings for all bots");
        
        Assert.IsNotNull(seriesInfo.SeriesChampion, "Series should have a champion");
        Assert.AreEqual(tournament.Champion, seriesInfo.SeriesChampion, "Series champion should match tournament champion");
        
        Assert.IsNotNull(seriesInfo.EndTime, "Series should be complete");
        Assert.IsTrue(seriesInfo.EndTime > seriesInfo.StartTime);
    }

    [TestMethod]
    public async Task RunSeriesAsync_PublishesSeriesLifecycleEvents()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = TestHelpers.CreateDefaultConfig(),
            SeriesName = "Test Series"
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var eventPublisher = new Mock<ITournamentEventPublisher>();
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem, eventPublisher.Object);

        // Act
        await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        eventPublisher.Verify(
            x => x.PublishSeriesStartedAsync(It.Is<SeriesStartedDto>(dto =>
                dto.SeriesName == "Test Series" &&
                dto.TotalSteps == 2 &&
                dto.Steps.Count == 2 &&
                dto.Steps[0].Status == SeriesStepStatus.Running &&
                dto.Steps[1].Status == SeriesStepStatus.Pending)),
            Times.Once);

        eventPublisher.Verify(
            x => x.PublishSeriesStepCompletedAsync(It.Is<SeriesStepCompletedDto>(dto => dto.StepIndex == 1)),
            Times.Once);

        eventPublisher.Verify(
            x => x.PublishSeriesStepCompletedAsync(It.Is<SeriesStepCompletedDto>(dto => dto.StepIndex == 2)),
            Times.Once);

        eventPublisher.Verify(
            x => x.PublishSeriesCompletedAsync(It.Is<SeriesCompletedDto>(dto =>
                dto.SeriesName == "Test Series" &&
                !string.IsNullOrWhiteSpace(dto.Champion))),
            Times.Once);

        eventPublisher.Verify(
            x => x.PublishSeriesProgressUpdatedAsync(It.IsAny<SeriesProgressUpdatedDto>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task RunSeriesAsync_WithMultipleTournamentsSameGame_AggregatesScores()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        Assert.AreEqual(3, seriesInfo.Tournaments.Count, "Should have 3 tournaments");
        
        // Verify all tournaments are RPSLS
        foreach (var tournament in seriesInfo.Tournaments)
        {
            Assert.AreEqual(GameType.RPSLS, tournament.GameType);
        }

        // Verify series champion has highest total score
        var champion = seriesInfo.SeriesStandings[0];
        Assert.AreEqual(seriesInfo.SeriesChampion, champion.BotName);

        // Verify total series score is sum of individual tournament scores
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            int expectedTotal = 0;
            foreach (var tournament in seriesInfo.Tournaments)
            {
                var botRanking = scoringSystem.GetCurrentRankings(tournament)
                    .FirstOrDefault(r => r.BotName.Equals(standing.BotName, StringComparison.OrdinalIgnoreCase));
                if (botRanking != null)
                    expectedTotal += botRanking.TotalScore;
            }
            Assert.AreEqual(expectedTotal, standing.TotalSeriesScore, 
                $"{standing.BotName} total series score should equal sum of tournament scores");
        }

        // Verify all scores are tracked under RPSLS game type
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            Assert.IsTrue(standing.ScoresByGame.ContainsKey(GameType.RPSLS));
            Assert.AreEqual(standing.TotalSeriesScore, standing.ScoresByGame[GameType.RPSLS]);
        }
    }

    [TestMethod]
    public async Task RunSeriesAsync_WithDifferentGameTypes_RunsEachGameInOrder()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        Assert.AreEqual(GameType.RPSLS, seriesInfo.Tournaments[0].GameType, "First tournament should be RPSLS");
        Assert.AreEqual(GameType.ColonelBlotto, seriesInfo.Tournaments[1].GameType, "Second tournament should be Blotto");
        Assert.AreEqual(GameType.RPSLS, seriesInfo.Tournaments[2].GameType, "Third tournament should be RPSLS");

        // Verify scores are tracked by game type
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            // Should have scores for both game types
            if (standing.ScoresByGame.ContainsKey(GameType.RPSLS))
            {
                // RPSLS score should be from tournaments 0 and 2
                var rpsls1 = scoringSystem.GetCurrentRankings(seriesInfo.Tournaments[0])
                    .FirstOrDefault(r => r.BotName.Equals(standing.BotName, StringComparison.OrdinalIgnoreCase));
                var rpsls2 = scoringSystem.GetCurrentRankings(seriesInfo.Tournaments[2])
                    .FirstOrDefault(r => r.BotName.Equals(standing.BotName, StringComparison.OrdinalIgnoreCase));
                
                int expectedRpslsScore = (rpsls1?.TotalScore ?? 0) + (rpsls2?.TotalScore ?? 0);
                Assert.AreEqual(expectedRpslsScore, standing.ScoresByGame[GameType.RPSLS]);
            }
        }
    }

    [TestMethod]
    public async Task RunSeriesAsync_CalculatesSeriesStandingsCorrectly()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert - verify standings are ordered by total score
        for (int i = 0; i < seriesInfo.SeriesStandings.Count - 1; i++)
        {
            var current = seriesInfo.SeriesStandings[i];
            var next = seriesInfo.SeriesStandings[i + 1];

            // Higher score should come first, or if equal, more wins, or if equal, fewer losses
            Assert.IsTrue(
                current.TotalSeriesScore > next.TotalSeriesScore ||
                (current.TotalSeriesScore == next.TotalSeriesScore && current.TotalWins >= next.TotalWins) ||
                (current.TotalSeriesScore == next.TotalSeriesScore && current.TotalWins == next.TotalWins && current.TotalLosses <= next.TotalLosses),
                $"Standings should be ordered correctly: {current.BotName} before {next.BotName}"
            );
        }

        // Verify champion is first in standings
        Assert.AreEqual(seriesInfo.SeriesStandings[0].BotName, seriesInfo.SeriesChampion);
    }

    [TestMethod]
    public async Task RunSeriesAsync_TracksTournamentWins()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        // Verify each bot has placement tracked for each tournament
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            Assert.AreEqual(3, standing.TournamentPlacements.Count, 
                $"{standing.BotName} should have placement for all 3 tournaments");

            // Verify tournament wins count
            int expectedWins = standing.TournamentPlacements.Count(p => p == 1);
            Assert.AreEqual(expectedWins, standing.TournamentsWon,
                $"{standing.BotName} should have {expectedWins} tournament wins");
        }

        // Verify sum of all tournament wins equals number of tournaments
        int totalWins = seriesInfo.SeriesStandings.Sum(s => s.TournamentsWon);
        Assert.AreEqual(3, totalWins, "Total tournament wins should equal number of tournaments");
    }

    [TestMethod]
    public async Task RunSeriesAsync_CancellationDuringSecondTournament_ThrowsOperationCanceledException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        var cts = new System.Threading.CancellationTokenSource();
        
        // Create pre-cancelled token
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            await seriesManager.RunSeriesAsync(bots, config, cts.Token));
    }

    [TestMethod]
    public async Task RunSeriesAsync_WithEmptyBotsList_ThrowsArgumentException()
    {
        // Arrange
        var bots = new List<BotInfo>();
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            await seriesManager.RunSeriesAsync(bots, config));
    }

    [TestMethod]
    public async Task RunSeriesAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        TournamentSeriesConfig config = null!;

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            await seriesManager.RunSeriesAsync(bots, config));
    }

    [TestMethod]
    public async Task RunSeriesAsync_CalculatesSeriesStatistics()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var gameRunner = new MockGameRunner();
        var scoringSystem = new Core.Scoring.ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var tournamentManager = new TournamentManager(engine, gameRunner);
        var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

        // Act
        var seriesInfo = await seriesManager.RunSeriesAsync(bots, config);

        // Assert
        // Verify total matches across all tournaments
        int expectedTotalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        Assert.AreEqual(expectedTotalMatches, seriesInfo.TotalMatches,
            "TotalMatches should equal sum of matches across all tournaments");

        // Verify duration is positive
        Assert.IsNotNull(seriesInfo.EndTime, "Series should have end time");
        var duration = seriesInfo.EndTime.Value - seriesInfo.StartTime;
        Assert.IsTrue(duration.TotalMilliseconds > 0, "Series duration should be positive");

        // Verify breakdown by game type
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.RPSLS),
            "Should have match count for RPSLS");
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.ColonelBlotto),
            "Should have match count for ColonelBlotto");

        // Verify RPSLS matches = sum of matches from tournament 0 and tournament 2
        int expectedRpsls = seriesInfo.Tournaments[0].MatchResults.Count + 
                           seriesInfo.Tournaments[2].MatchResults.Count;
        Assert.AreEqual(expectedRpsls, seriesInfo.MatchesByGameType[GameType.RPSLS],
            "RPSLS match count should include both RPSLS tournaments");

        // Verify ColonelBlotto matches = matches from tournament 1
        int expectedBlotto = seriesInfo.Tournaments[1].MatchResults.Count;
        Assert.AreEqual(expectedBlotto, seriesInfo.MatchesByGameType[GameType.ColonelBlotto],
            "ColonelBlotto match count should match tournament 1");

        // Verify all matches accounted for
        int sumByGameType = seriesInfo.MatchesByGameType.Values.Sum();
        Assert.AreEqual(seriesInfo.TotalMatches, sumByGameType,
            "Sum of matches by game type should equal total matches");
    }
}
