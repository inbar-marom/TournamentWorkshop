namespace TournamentEngine.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
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
}
