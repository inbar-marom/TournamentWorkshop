namespace TournamentEngine.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Integration tests for the complete tournament series stack:
/// TournamentSeriesManager -> TournamentManager -> GroupStageTournamentEngine -> ScoringSystem
/// </summary>
[TestClass]
public class TournamentSeriesIntegrationTests
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
    public async Task FullSeries_WithThreeTournamentsSameGame_CompletesSuccessfully()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS, GameType.RPSLS },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert - Series completed
        Assert.IsNotNull(seriesInfo);
        Assert.IsNotNull(seriesInfo.SeriesId);
        Assert.IsNotNull(seriesInfo.EndTime);
        Assert.IsTrue((seriesInfo.EndTime.Value - seriesInfo.StartTime).TotalMilliseconds >= 0);

        // Assert - All tournaments completed
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        foreach (var tournament in seriesInfo.Tournaments)
        {
            Assert.AreEqual(TournamentState.Completed, tournament.State);
            Assert.IsNotNull(tournament.Champion);
            Assert.IsNotNull(tournament.EndTime);
            Assert.AreEqual(GameType.RPSLS, tournament.GameType);
        }

        // Assert - Series standings
        Assert.AreEqual(bots.Count, seriesInfo.SeriesStandings.Count);
        Assert.IsNotNull(seriesInfo.SeriesChampion);
        Assert.IsTrue(bots.Any(b => b.TeamName == seriesInfo.SeriesChampion));

        // Order standings by total score (desc) for deterministic checks
        var orderedStandings = seriesInfo.SeriesStandings
            .OrderByDescending(s => s.TotalSeriesScore)
            .ThenBy(s => s.BotName)
            .ToList();

        // Assert - Standings are sorted by total score (desc)
        for (int i = 0; i < orderedStandings.Count - 1; i++)
        {
            Assert.IsTrue(orderedStandings[i].TotalSeriesScore >= 
                         orderedStandings[i + 1].TotalSeriesScore);
        }

        // Assert - Series champion has highest score (allow ties)
        var maxScore = orderedStandings[0].TotalSeriesScore;
        Assert.IsTrue(orderedStandings.Where(s => s.TotalSeriesScore == maxScore)
            .Any(s => s.BotName == seriesInfo.SeriesChampion));

        // Assert - All bots have placements for all tournaments
        foreach (var standing in orderedStandings)
        {
            Assert.AreEqual(3, standing.TournamentPlacements.Count);
            Assert.IsTrue(standing.TotalSeriesScore >= 0);
        }

        // Assert - Statistics calculated
        Assert.IsTrue(seriesInfo.TotalMatches > 0);
        Assert.AreEqual(1, seriesInfo.MatchesByGameType.Count);
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.RPSLS));
        Assert.AreEqual(seriesInfo.TotalMatches, seriesInfo.MatchesByGameType[GameType.RPSLS]);
    }

    [TestMethod]
    public async Task FullSeries_WithMultipleGameTypes_CompletesSuccessfully()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> 
            { 
                GameType.RPSLS, 
                GameType.ColonelBlotto, 
                GameType.PenaltyKicks 
            },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert - All tournaments completed
        Assert.AreEqual(3, seriesInfo.Tournaments.Count);
        Assert.AreEqual(GameType.RPSLS, seriesInfo.Tournaments[0].GameType);
        Assert.AreEqual(GameType.ColonelBlotto, seriesInfo.Tournaments[1].GameType);
        Assert.AreEqual(GameType.PenaltyKicks, seriesInfo.Tournaments[2].GameType);

        foreach (var tournament in seriesInfo.Tournaments)
        {
            Assert.AreEqual(TournamentState.Completed, tournament.State);
            Assert.IsNotNull(tournament.Champion);
        }

        // Assert - Series standings aggregate across all games
        Assert.AreEqual(bots.Count, seriesInfo.SeriesStandings.Count);
        Assert.IsNotNull(seriesInfo.SeriesChampion);

        // Assert - Statistics include all game types
        Assert.AreEqual(3, seriesInfo.MatchesByGameType.Count);
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.RPSLS));
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.ColonelBlotto));
        Assert.IsTrue(seriesInfo.MatchesByGameType.ContainsKey(GameType.PenaltyKicks));

        // Assert - Total matches equals sum by game type
        Assert.AreEqual(seriesInfo.TotalMatches, seriesInfo.MatchesByGameType.Values.Sum());

        // Assert - ScoresByGame breakdown exists
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            Assert.IsTrue(standing.ScoresByGame.Count > 0);
            // Total score should equal sum of scores by game
            Assert.AreEqual(standing.TotalSeriesScore, standing.ScoresByGame.Values.Sum());
        }
    }

    [TestMethod]
    public async Task FullSeries_VerifiesSeriesStandingsAggregation()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(5);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert - Verify aggregation logic
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            // Find this bot in each tournament
            int expectedTotalScore = 0;
            int expectedTotalWins = 0;
            int expectedTotalLosses = 0;
            int expectedTournamentWins = 0;

            foreach (var tournament in seriesInfo.Tournaments)
            {
                var rankings = _scoringSystem.GetCurrentRankings(tournament);
                var botRanking = rankings.FirstOrDefault(r => 
                    r.BotName.Equals(standing.BotName, StringComparison.OrdinalIgnoreCase));

                if (botRanking != null)
                {
                    expectedTotalScore += botRanking.TotalScore;
                    expectedTotalWins += botRanking.Wins;
                    expectedTotalLosses += botRanking.Losses;
                    if (botRanking.FinalPlacement == 1)
                        expectedTournamentWins++;
                }
            }

            // Verify aggregated values match
            Assert.AreEqual(expectedTotalScore, standing.TotalSeriesScore,
                $"{standing.BotName} total score mismatch");
            Assert.AreEqual(expectedTotalWins, standing.TotalWins,
                $"{standing.BotName} total wins mismatch");
            Assert.AreEqual(expectedTotalLosses, standing.TotalLosses,
                $"{standing.BotName} total losses mismatch");
            Assert.AreEqual(expectedTournamentWins, standing.TournamentsWon,
                $"{standing.BotName} tournament wins mismatch");
        }
    }

    [TestMethod]
    public async Task FullSeries_WithLargeBotCount_CompletesSuccessfully()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(20);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert
        Assert.AreEqual(2, seriesInfo.Tournaments.Count);
        Assert.AreEqual(20, seriesInfo.SeriesStandings.Count);
        Assert.IsNotNull(seriesInfo.SeriesChampion);

        // Verify all tournaments completed with all bots
        foreach (var tournament in seriesInfo.Tournaments)
        {
            Assert.AreEqual(TournamentState.Completed, tournament.State);
            Assert.AreEqual(20, tournament.Bots.Count);
        }

        // Verify statistics are reasonable
        Assert.IsTrue(seriesInfo.TotalMatches > 0);
        Assert.IsTrue(seriesInfo.TotalMatches >= seriesInfo.Tournaments.Count * 20); // At least some matches per bot
    }

    [TestMethod]
    public async Task FullSeries_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.RPSLS, GameType.RPSLS },
            BaseConfig = _baseConfig
        };

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await _seriesManager.RunSeriesAsync(bots, seriesConfig, cts.Token);
        });
    }

    [TestMethod]
    public async Task FullSeries_PreservesIndividualTournamentResults()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(8);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert - Individual tournament data preserved
        Assert.AreEqual(2, seriesInfo.Tournaments.Count);

        foreach (var tournament in seriesInfo.Tournaments)
        {
            // Tournament has complete data
            Assert.IsNotNull(tournament.TournamentId);
            Assert.IsNotNull(tournament.Champion);
            Assert.IsTrue(tournament.MatchResults.Count > 0);
            Assert.AreEqual(bots.Count, tournament.Bots.Count);

            // All match results are valid
            foreach (var match in tournament.MatchResults)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(match.Bot1Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(match.Bot2Name));
                Assert.IsTrue(bots.Any(b => b.TeamName == match.Bot1Name));
                Assert.IsTrue(bots.Any(b => b.TeamName == match.Bot2Name));
            }

            // Can still get rankings from individual tournaments
            var rankings = _scoringSystem.GetCurrentRankings(tournament);
            Assert.AreEqual(bots.Count, rankings.Count);
        }
    }

    [TestMethod]
    public async Task FullSeries_TournamentWinsTracking_IsAccurate()
    {
        // Arrange
        var bots = await IntegrationTestHelpers.CreateDemoBots(10);
        var seriesConfig = new TournamentSeriesConfig
        {
            GameTypes = new List<GameType> 
            { 
                GameType.RPSLS, 
                GameType.ColonelBlotto, 
                GameType.PenaltyKicks,
                GameType.SecurityGame
            },
            BaseConfig = _baseConfig
        };

        // Act
        var seriesInfo = await _seriesManager.RunSeriesAsync(bots, seriesConfig);

        // Assert - Tournament wins add up correctly
        int totalTournamentWins = seriesInfo.SeriesStandings.Sum(s => s.TournamentsWon);
        Assert.AreEqual(4, totalTournamentWins, "Total tournament wins should equal number of tournaments");

        // Assert - Each tournament has exactly one champion
        var tournamentChampions = seriesInfo.Tournaments.Select(t => t.Champion).ToList();
        Assert.AreEqual(4, tournamentChampions.Count);
        Assert.IsTrue(tournamentChampions.All(c => !string.IsNullOrWhiteSpace(c)));

        // Assert - Placements are tracked correctly
        foreach (var standing in seriesInfo.SeriesStandings)
        {
            Assert.AreEqual(4, standing.TournamentPlacements.Count);
            
            // Count 1st place finishes
            int firstPlaces = standing.TournamentPlacements.Count(p => p == 1);
            Assert.AreEqual(firstPlaces, standing.TournamentsWon);
        }
    }


}
