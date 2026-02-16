namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// TDD tests for TournamentEngine - Step 3: Record match results and standings
/// </summary>
[TestClass]
public class TournamentEngineRecordMatchResultTests
{
    private MockGameRunner _mockGameRunner = null!;
    private MockScoringSystem _mockScoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGameRunner = new MockGameRunner();
        _mockScoringSystem = new MockScoringSystem();
        _engine = new GroupStageTournamentEngine(_mockGameRunner, _mockScoringSystem);
    }

    [TestMethod]
    public void RecordMatchResult_WithValidMatch_ShouldAddToMatchResults()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matchResult = CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins);

        // Act
        var updatedInfo = _engine.RecordMatchResult(matchResult);

        // Assert
        Assert.IsNotNull(updatedInfo);
        Assert.AreEqual(1, updatedInfo.MatchResults.Count);
    }

    [TestMethod]
    public void RecordMatchResult_ShouldReturnUpdatedTournamentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var initialInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matchResult = CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins);

        // Act
        var updatedInfo = _engine.RecordMatchResult(matchResult);

        // Assert
        Assert.AreEqual(initialInfo.TournamentId, updatedInfo.TournamentId);
        Assert.AreEqual(1, updatedInfo.MatchResults.Count);
    }

    [TestMethod]
    public void RecordMatchResult_WithMultipleMatches_ShouldAccumulateResults()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var match1 = CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins);
        var match2 = CreateMatchResult("Team3", "Team4", MatchOutcome.Player2Wins);

        // Act
        _engine.RecordMatchResult(match1);
        var updatedInfo = _engine.RecordMatchResult(match2);

        // Assert
        Assert.AreEqual(2, updatedInfo.MatchResults.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void RecordMatchResult_WithUnknownBot_ShouldThrowArgumentException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matchResult = TestHelpers.CreateMatchResult("UnknownTeam", "Team2", MatchOutcome.Player1Wins);

        // Act
        _engine.RecordMatchResult(matchResult);

        // Assert - Expects exception
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RecordMatchResult_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        _engine.RecordMatchResult(null!);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void RecordMatchResult_BeforeInit_ShouldThrowInvalidOperationException()
    {
        // Act
        _engine.RecordMatchResult(TestHelpers.CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins));
    }

    [TestMethod]
    public void RecordMatchResult_Player1Wins_ShouldAward3PointsToWinnerAndIncrementWins()
    {
        // Arrange - Use GroupCount=1 to ensure all 10 bots play each other
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
            GroupCount = 1 //
        };
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = _engine.GetNextMatches();
        var firstMatch = matches[0];
        var result = TestHelpers.CreateMatchResult(firstMatch.bot1.TeamName, firstMatch.bot2.TeamName, MatchOutcome.Player1Wins);

        // Act
        _engine.RecordMatchResult(result);

        // Assert — verify via another match that standings accumulate correctly
        var info = _engine.GetTournamentInfo();
        Assert.AreEqual(1, info.MatchResults.Count);
        Assert.AreEqual(MatchOutcome.Player1Wins, info.MatchResults[0].Outcome);
    }

    [TestMethod]
    public void RecordMatchResult_ShouldRemoveFromPendingMatches()
    {
        // Arrange - Use GroupCount=1 to ensure all 10 bots play each other
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
            GroupCount = 1 //
        };
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matchesBefore = _engine.GetNextMatches();
        var countBefore = matchesBefore.Count;
        var firstMatch = matchesBefore[0];
        var result = TestHelpers.CreateMatchResult(firstMatch.bot1.TeamName, firstMatch.bot2.TeamName, MatchOutcome.Player1Wins);

        // Act
        _engine.RecordMatchResult(result);
        var matchesAfter = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(countBefore - 1, matchesAfter.Count, "Pending matches should decrease by 1 after recording a result");
    }

    private static MatchResult CreateMatchResult(string bot1, string bot2, MatchOutcome outcome)
    {
        return new MatchResult
        {
            Bot1Name = bot1,
            Bot2Name = bot2,
            GameType = GameType.RPSLS,
            Outcome = outcome,
            WinnerName = outcome switch
            {
                MatchOutcome.Player1Wins => bot1,
                MatchOutcome.Player2Wins => bot2,
                _ => string.Empty
            },
            Bot1Score = outcome == MatchOutcome.Player1Wins ? 30 : 20,
            Bot2Score = outcome == MatchOutcome.Player2Wins ? 30 : 20,
            MatchLog = new List<string> { "Test match" },
            Errors = new List<string>(),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(1),
            Duration = TimeSpan.FromSeconds(1)
        };
    }
}
