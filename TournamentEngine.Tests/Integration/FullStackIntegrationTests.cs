namespace TournamentEngine.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Full-stack integration tests combining Tournament Engine (including Series) 
/// with Dashboard-ready data structures to verify end-to-end system functionality.
/// These tests verify that tournament results can be properly captured and formatted
/// for use in dashboard services.
/// </summary>
[TestClass]
public class FullStackIntegrationTests
{
    private GameRunner _gameRunner = null!;
    private ScoringSystem _scoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;
    private TournamentManager _tournamentManager = null!;
    private TournamentSeriesManager _seriesManager = null!;
    private TournamentConfig _baseConfig = null!;

    [TestInitialize]
    public void Setup()
    {
        _baseConfig = IntegrationTestHelpers.CreateConfig();
        _gameRunner = new GameRunner(_baseConfig);
        _scoringSystem = new ScoringSystem();
        _engine = new GroupStageTournamentEngine(_gameRunner, _scoringSystem);
        _tournamentManager = new TournamentManager(_engine, _gameRunner);
        _seriesManager = new TournamentSeriesManager(_tournamentManager, _scoringSystem);
    }

    [TestMethod]
    public async Task FullStack_SingleTournament_ProducesExportableData()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        
        // Act - Run tournament
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);
        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);
        
        // Assert
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.Champion);
        Assert.AreEqual(bots.Count, rankings.Count);
        Assert.AreEqual(tournamentInfo.Champion, rankings.First(r => r.FinalPlacement == 1).BotName);
        Assert.IsTrue(tournamentInfo.MatchResults.Count > 0);
    }

    [TestMethod]
    public async Task FullStack_TournamentSeries_ProducesConsistentResults()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
            BaseConfig = _baseConfig
        };
        
        // Act - Run series
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        int totalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        
        // Assert
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        Assert.IsNotNull(seriesInfo.SeriesChampion);
        Assert.AreEqual(seriesInfo.TotalMatches, totalMatches);
        Assert.AreEqual(bots.Count, seriesInfo.SeriesStandings.Count);
        Assert.IsTrue(seriesInfo.SeriesStandings.Any(s => s.BotName == seriesInfo.SeriesChampion));
    }

    [TestMethod]
    public async Task FullStack_Tournament_DataForExport()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        
        // Act
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);
        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);
        var champion = rankings.First(r => r.FinalPlacement == 1);
        
        // Assert
        Assert.AreEqual("Completed", tournamentInfo.State.ToString());
        Assert.AreEqual(champion.BotName, tournamentInfo.Champion);
        Assert.AreEqual(bots.Count, rankings.Count);
        Assert.IsTrue(rankings[0].TotalScore >= 0);
    }

    [TestMethod]
    public async Task FullStack_Series_SnapshotData()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> 
            { 
                GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame
            },
            BaseConfig = _baseConfig
        };
        
        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        // Assert
        Assert.AreEqual(4, seriesInfo.Tournaments.Count);
        foreach (var tournament in seriesInfo.Tournaments)
        {
            Assert.IsNotNull(tournament.Champion);
            Assert.IsTrue(tournament.MatchResults.Count > 0);
            var rankings = _scoringSystem.GetCurrentRankings(tournament);
            Assert.AreEqual(bots.Count, rankings.Count);
        }
    }

    [TestMethod]
    public async Task FullStack_Tournament_Leaderboard()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(5);
        
        // Act
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);
        var rankings = _scoringSystem.GetCurrentRankings(tournamentInfo);
        var leaderboard = rankings.OrderBy(l => l.FinalPlacement).ToList();
        
        // Assert
        Assert.AreEqual(bots.Count, leaderboard.Count);
        Assert.AreEqual(1, leaderboard[0].FinalPlacement);
        Assert.AreEqual(bots.Count, leaderboard[bots.Count - 1].FinalPlacement);
        
        for (int i = 0; i < leaderboard.Count - 1; i++)
        {
            Assert.IsTrue(leaderboard[i].TotalScore >= leaderboard[i + 1].TotalScore);
        }
    }

    [TestMethod]
    public async Task FullStack_Series_Aggregation()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };
        
        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        // Assert
        Assert.AreEqual(2, seriesInfo.Tournaments.Count);
        Assert.AreEqual(bots.Count, seriesInfo.SeriesStandings.Count);
        Assert.IsTrue(seriesInfo.TotalMatches > 0);
        
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            Assert.AreEqual(2, standing.TournamentPlacements.Count);
            Assert.IsTrue(standing.TotalSeriesScore >= 0);
        }
    }

    [TestMethod]
    public async Task FullStack_LargeSeries_DataIntegrity()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(15);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };
        
        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        
        int totalMatchesFromTournaments = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
        int totalMatchesFromStats = seriesInfo.MatchesByGameType.Values.Sum();
        var standingBots = new HashSet<string>(
            seriesInfo.SeriesStandings.Select(s => s.BotName), StringComparer.OrdinalIgnoreCase);
        var tournamentBots = new HashSet<string>(
            bots.Select(b => b.TeamName), StringComparer.OrdinalIgnoreCase);
        
        // Assert
        Assert.AreEqual(seriesInfo.TotalMatches, totalMatchesFromTournaments);
        Assert.AreEqual(seriesInfo.TotalMatches, totalMatchesFromStats);
        Assert.IsTrue(standingBots.SetEquals(tournamentBots));
        Assert.IsTrue(seriesInfo.SeriesStandings.Any(s => s.BotName == seriesInfo.SeriesChampion));
    }

    [TestMethod]
    public async Task FullStack_Tournament_MatchResults()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(6);
        
        // Act
        var tournamentInfo = await _tournamentManager.RunTournamentAsync(
            bots, GameType.RPSLS, _baseConfig);
        
        var matchFeed = tournamentInfo.MatchResults
            .OrderByDescending(m => tournamentInfo.MatchResults.IndexOf(m))
            .Take(10)
            .ToList();
        
        // Assert
        Assert.IsTrue(matchFeed.Count > 0);
        Assert.IsTrue(matchFeed.Count <= 10);
        
        foreach (var match in matchFeed)
        {
            Assert.IsNotNull(match.Bot1Name);
            Assert.IsNotNull(match.Bot2Name);
            Assert.IsTrue(match.Bot1Score >= 0);
            Assert.IsTrue(match.Bot2Score >= 0);
        }
    }

    [TestMethod]
    public async Task FullStack_Series_TournamentWins()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
            BaseConfig = _baseConfig
        };
        
        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);
        var placementsByBot = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            placementsByBot[standing.BotName] = standing.TournamentPlacements;
        }
        
        // Assert
        Assert.AreEqual(bots.Count, placementsByBot.Count);
        foreach (var bot in bots)
        {
            Assert.IsTrue(placementsByBot.ContainsKey(bot.TeamName));
            Assert.AreEqual(3, placementsByBot[bot.TeamName].Count);
            
            foreach (var placement in placementsByBot[bot.TeamName])
            {
                Assert.IsTrue(placement >= 1 && placement <= bots.Count);
            }
        }
        
        var championPlacements = placementsByBot[seriesInfo.SeriesChampion];
        double avgPlacement = championPlacements.Average();
        Assert.IsTrue(avgPlacement <= (bots.Count * 0.75), "Champion should have reasonable placement");
    }
}
