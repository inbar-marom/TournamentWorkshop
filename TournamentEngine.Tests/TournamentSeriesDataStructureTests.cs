namespace TournamentEngine.Tests;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Unit tests for tournament series data structures
/// </summary>
[TestClass]
public class TournamentSeriesDataStructureTests
{
    [TestMethod]
    public void TournamentSeriesConfig_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual(3, config.GameTypes.Count);
        Assert.IsTrue(config.AggregateScores);
    }

    [TestMethod]
    public void TournamentSeriesConfig_Validate_WithValidData_DoesNotThrow()
    {
        // Arrange
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        // Act & Assert
        config.Validate(); // Should not throw
    }

    [TestMethod]
    public void TournamentSeriesConfig_Validate_WithEmptyGameTypes_ThrowsArgumentException()
    {
        // Arrange
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType>(),
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => config.Validate());
        StringAssert.Contains(ex.Message, "game type");
    }

    [TestMethod]
    public void SeriesStanding_Creation_InitializesCollections()
    {
        // Arrange & Act
        var standing = new SeriesStanding { BotName = "TestBot" };

        // Assert
        Assert.AreEqual("TestBot", standing.BotName);
        Assert.AreEqual(0, standing.TotalSeriesScore);
        Assert.AreEqual(0, standing.TournamentsWon);
        Assert.AreEqual(0, standing.TotalWins);
        Assert.AreEqual(0, standing.TotalLosses);
        Assert.IsNotNull(standing.ScoresByGame);
        Assert.AreEqual(0, standing.ScoresByGame.Count);
        Assert.IsNotNull(standing.TournamentPlacements);
        Assert.AreEqual(0, standing.TournamentPlacements.Count);
    }

    [TestMethod]
    public void SeriesStanding_CanTrackScoresByGame()
    {
        // Arrange
        var standing = new SeriesStanding { BotName = "TestBot" };

        // Act
        standing.ScoresByGame[GameType.RPSLS] = 150;
        standing.ScoresByGame[GameType.ColonelBlotto] = 75;

        // Assert
        Assert.AreEqual(150, standing.ScoresByGame[GameType.RPSLS]);
        Assert.AreEqual(75, standing.ScoresByGame[GameType.ColonelBlotto]);
    }

    [TestMethod]
    public void SeriesStanding_CanTrackTournamentPlacements()
    {
        // Arrange
        var standing = new SeriesStanding { BotName = "TestBot" };

        // Act
        standing.TournamentPlacements.Add(1); // Won tournament 1
        standing.TournamentPlacements.Add(3); // 3rd place in tournament 2
        standing.TournamentPlacements.Add(1); // Won tournament 3

        // Assert
        Assert.AreEqual(3, standing.TournamentPlacements.Count);
        Assert.AreEqual(1, standing.TournamentPlacements[0]);
        Assert.AreEqual(3, standing.TournamentPlacements[1]);
        Assert.AreEqual(1, standing.TournamentPlacements[2]);
    }

    [TestMethod]
    public void TournamentSeriesInfo_Creation_InitializesCollections()
    {
        // Arrange
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        // Act
        var seriesInfo = new TournamentSeriesInfo
        {
            SeriesId = "series-123",
            StartTime = DateTime.UtcNow,
            Config = config
        };

        // Assert
        Assert.AreEqual("series-123", seriesInfo.SeriesId);
        Assert.IsNotNull(seriesInfo.Tournaments);
        Assert.AreEqual(0, seriesInfo.Tournaments.Count);
        Assert.IsNotNull(seriesInfo.SeriesStandings);
        Assert.AreEqual(0, seriesInfo.SeriesStandings.Count);
        Assert.IsNull(seriesInfo.SeriesChampion);
        Assert.IsNull(seriesInfo.EndTime);
        Assert.IsNotNull(seriesInfo.Config);
    }

    [TestMethod]
    public void TournamentSeriesInfo_CanSetSeriesChampion()
    {
        // Arrange
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var seriesInfo = new TournamentSeriesInfo
        {
            SeriesId = "series-123",
            StartTime = DateTime.UtcNow,
            Config = config
        };

        // Act
        seriesInfo.SeriesChampion = "ChampionBot";

        // Assert
        Assert.AreEqual("ChampionBot", seriesInfo.SeriesChampion);
    }

    [TestMethod]
    public void TournamentSeriesInfo_CanSetEndTime()
    {
        // Arrange
        var config = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS },
            BaseConfig = TestHelpers.CreateDefaultConfig()
        };

        var startTime = DateTime.UtcNow;
        var seriesInfo = new TournamentSeriesInfo
        {
            SeriesId = "series-123",
            StartTime = startTime,
            Config = config
        };

        // Act
        var endTime = startTime.AddMinutes(10);
        seriesInfo.EndTime = endTime;

        // Assert
        Assert.AreEqual(endTime, seriesInfo.EndTime);
        Assert.IsTrue(seriesInfo.EndTime > seriesInfo.StartTime);
    }
}
