namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Unit tests for TournamentEngine - Step 1: Core Structure and Initialization
/// </summary>
[TestClass]
public class TournamentEngineInitializationTests
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
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Arrange & Act
        var manager = new GroupStageTournamentEngine(_mockGameRunner, _mockScoringSystem);

        // Assert
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullGameRunner_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        new GroupStageTournamentEngine(null!, _mockScoringSystem);

        // Assert - Expects exception
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullScoringSystem_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        new GroupStageTournamentEngine(_mockGameRunner, null!);

        // Assert - Expects exception
    }

    [TestMethod]
    public void InitializeTournament_With12Bots_ShouldReturnValidTournamentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsNotNull(tournamentInfo);
        Assert.IsFalse(string.IsNullOrEmpty(tournamentInfo.TournamentId));
        Assert.AreEqual(GameType.RPSLS, tournamentInfo.GameType);
        Assert.AreEqual(TournamentState.InProgress, tournamentInfo.State);
        Assert.AreEqual(12, tournamentInfo.Bots.Count);
        Assert.AreEqual(1, tournamentInfo.CurrentRound);
        Assert.IsNull(tournamentInfo.Champion);
        Assert.IsTrue(tournamentInfo.StartTime <= DateTime.UtcNow);
    }

    [TestMethod]
    public void InitializeTournament_With30Bots_ShouldReturnValidTournamentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(30, GameType.ColonelBlotto);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.ColonelBlotto, config);

        // Assert
        Assert.IsNotNull(tournamentInfo);
        Assert.AreEqual(GameType.ColonelBlotto, tournamentInfo.GameType);
        Assert.AreEqual(30, tournamentInfo.Bots.Count);
        Assert.AreEqual(TournamentState.InProgress, tournamentInfo.State);
    }

    [TestMethod]
    public void InitializeTournament_With60Bots_ShouldReturnValidTournamentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(60, GameType.PenaltyKicks);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.PenaltyKicks, config);

        // Assert
        Assert.IsNotNull(tournamentInfo);
        Assert.AreEqual(60, tournamentInfo.Bots.Count);
        Assert.AreEqual(TournamentState.InProgress, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.TournamentId);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void InitializeTournament_WithNullBots_ShouldThrowArgumentException()
    {
        // Arrange
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        _engine.InitializeTournament(null!, GameType.RPSLS, config);

        // Assert - Expects exception
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void InitializeTournament_WithLessThan2Bots_ShouldThrowArgumentException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(1, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Assert - Expects exception
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void InitializeTournament_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);

        // Act
        _engine.InitializeTournament(bots, GameType.RPSLS, null!);

        // Assert - Expects exception
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetNextMatches_BeforeInit_ShouldThrowInvalidOperationException()
    {
        // Act
        _engine.GetNextMatches();
    }

    [TestMethod]
    public void InitializeTournament_ShouldGenerateUniqueTournamentId()
    {
        // Arrange
        var bots1 = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var bots2 = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var manager1 = new GroupStageTournamentEngine(_mockGameRunner, _mockScoringSystem);
        var manager2 = new GroupStageTournamentEngine(_mockGameRunner, _mockScoringSystem);
        var tournament1 = manager1.InitializeTournament(bots1, GameType.RPSLS, config);
        var tournament2 = manager2.InitializeTournament(bots2, GameType.RPSLS, config);

        // Assert
        Assert.AreNotEqual(tournament1.TournamentId, tournament2.TournamentId);
    }

    [TestMethod]
    public void GetTournamentInfo_AfterInitialization_ShouldReturnCurrentInfo()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var initialInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var retrievedInfo = _engine.GetTournamentInfo();

        // Assert
        Assert.AreEqual(initialInfo.TournamentId, retrievedInfo.TournamentId);
        Assert.AreEqual(initialInfo.GameType, retrievedInfo.GameType);
        Assert.AreEqual(initialInfo.State, retrievedInfo.State);
    }

    [TestMethod]
    public void IsTournamentComplete_AfterInitialization_ShouldReturnFalse()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var isComplete = _engine.IsTournamentComplete();

        // Assert
        Assert.IsFalse(isComplete);
    }

    [TestMethod]
    public void GetCurrentRound_AfterInitialization_ShouldReturn1()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var currentRound = _engine.GetCurrentRound();

        // Assert
        Assert.AreEqual(1, currentRound);
    }

    [TestMethod]
    public void InitializeTournament_ShouldSetStartTimeToNow()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        var beforeInit = DateTime.UtcNow;

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var afterInit = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(tournamentInfo.StartTime >= beforeInit);
        Assert.IsTrue(tournamentInfo.StartTime <= afterInit);
    }

    [TestMethod]
    public void InitializeTournament_ShouldLeaveEndTimeNull()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsNull(tournamentInfo.EndTime);
    }

    [TestMethod]
    public void InitializeTournament_ShouldInitializeEmptyMatchResults()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(12, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        var tournamentInfo = _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsNotNull(tournamentInfo.MatchResults);
        Assert.AreEqual(0, tournamentInfo.MatchResults.Count);
    }
}
