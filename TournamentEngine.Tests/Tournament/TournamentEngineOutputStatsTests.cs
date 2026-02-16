namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Tests for TournamentEngine - Step 7: Output and stats helpers
/// </summary>
[TestClass]
public class TournamentEngineOutputStatsTests
{
    private MockGameRunner _mockGameRunner = null!;
    private GroupStageTournamentEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGameRunner = new MockGameRunner();
        _engine = new GroupStageTournamentEngine(_mockGameRunner, new RecordingScoringSystem());
    }

    [TestMethod]
    public void GetRemainingBots_AfterInitialization_ShouldReturnAllBots()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var remaining = _engine.GetRemainingBots();

        // Assert
        Assert.AreEqual(20, remaining.Count);
    }

    [TestMethod]
    public void GetRemainingBots_AfterAdvanceToFinalGroup_ShouldReturnFinalists()
    {
        // Arrange - 20 bots with GroupCount=2 -> 2 groups of 10 -> 2 winners
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
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
            GroupCount = 2 //
        };
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();

        // Act
        _engine.AdvanceToNextRound();
        var remaining = _engine.GetRemainingBots();

        // Assert - 2 groups -> 2 winners
        Assert.AreEqual(2, remaining.Count);
    }

    [TestMethod]
    public void GetFinalRankings_WhenTournamentComplete_ShouldReturnRankedBots()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
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
            GroupCount = 2 //
        };
        var scoringSystem = new RecordingScoringSystem();
        _engine = new GroupStageTournamentEngine(_mockGameRunner, scoringSystem);
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in finalGroupMatches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }
        _engine.AdvanceToNextRound();

        // Act
        var rankings = _engine.GetFinalRankings();

        // Assert
        Assert.AreEqual(2, rankings.Count, "Expected rankings for the final group bots");
        Assert.AreEqual(1, rankings[0].placement, "Top bot should have placement 1");
    }

    private void RecordAllCurrentMatchesAsPlayer1Wins()
    {
        var matches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in matches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }
    }

    private sealed class RecordingScoringSystem : IScoringSystem
    {
        public (int player1Score, int player2Score) CalculateMatchScore(MatchResult matchResult)
        {
            return matchResult.Outcome switch
            {
                MatchOutcome.Player1Wins => (3, 0),
                MatchOutcome.Player2Wins => (0, 3),
                MatchOutcome.Draw => (1, 1),
                _ => (0, 0)
            };
        }

        public Dictionary<string, TournamentStanding> UpdateStandings(MatchResult matchResult, Dictionary<string, TournamentStanding> currentStandings)
        {
            return currentStandings;
        }

        public List<BotRanking> GenerateFinalRankings(TournamentInfo tournamentInfo)
        {
            // Return a small deterministic list to validate it is passed through
            return new List<BotRanking>
            {
                new BotRanking { BotName = "Rank1", FinalPlacement = 1, Wins = 0, Losses = 0, TotalScore = 0, EliminationRound = 0, TotalPlayTime = TimeSpan.Zero },
                new BotRanking { BotName = "Rank2", FinalPlacement = 2, Wins = 0, Losses = 0, TotalScore = 0, EliminationRound = 0, TotalPlayTime = TimeSpan.Zero }
            };
        }

        public TournamentStatistics CalculateStatistics(TournamentInfo tournamentInfo)
        {
            return new TournamentStatistics
            {
                TotalMatches = tournamentInfo.MatchResults.Count,
                TotalRounds = tournamentInfo.TotalRounds,
                TournamentDuration = TimeSpan.Zero,
                AverageMatchDuration = TimeSpan.Zero
            };
        }

        public List<BotRanking> GetCurrentRankings(TournamentInfo tournamentInfo)
        {
            return new List<BotRanking>();
        }
    }
}
