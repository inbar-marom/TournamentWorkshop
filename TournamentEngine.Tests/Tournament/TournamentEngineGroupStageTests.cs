namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Linq;

/// <summary>
/// TDD Tests for TournamentEngine - Step 2: Group Management
/// These tests are written BEFORE implementation to drive the design
/// </summary>
[TestClass]
public class TournamentEngineGroupStageTests
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

    #region GetNextMatches Tests

    [TestMethod]
    public void GetNextMatches_AfterInitialization_ShouldReturnInitialGroupMatches()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.IsNotNull(matches);
        Assert.IsTrue(matches.Count > 0, "Should return at least one match");
    }

    [TestMethod]
    public void GetNextMatches_With20Bots_ShouldReturnCorrectNumberOfMatches()
    {
        // Arrange - 20 bots with GroupCount=10 → 10 groups of 2 bots each = 10 * 1 match = 10 matches total
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(10, matches.Count, "20 bots in 10 groups of 2 should generate 10 round-robin matches");
    }

    [TestMethod]
    public void GetNextMatches_With60Bots_ShouldReturnCorrectNumberOfMatches()
    {
        // Arrange - 60 bots with GroupCount=10 → 10 groups of 6 bots each = 10 * 15 matches = 150 matches total
        var bots = TestHelpers.CreateDummyBotInfos(60, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(150, matches.Count, "60 bots in 10 groups of 6 should generate 150 round-robin matches");
    }

    [TestMethod]
    public void GetNextMatches_ShouldReturnUniqueMatchPairs()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(30, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        var matchSet = new HashSet<string>();
        foreach (var (bot1, bot2) in matches)
        {
            var matchKey1 = $"{bot1.TeamName}-{bot2.TeamName}";
            var matchKey2 = $"{bot2.TeamName}-{bot1.TeamName}";
            
            Assert.IsFalse(matchSet.Contains(matchKey1) || matchSet.Contains(matchKey2), 
                $"Duplicate match found: {bot1.TeamName} vs {bot2.TeamName}");
            
            matchSet.Add(matchKey1);
        }
    }

    [TestMethod]
    public void GetNextMatches_ShouldNotMatchBotAgainstItself()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        foreach (var (bot1, bot2) in matches)
        {
            Assert.AreNotEqual(bot1.TeamName, bot2.TeamName, 
                "Bot should not be matched against itself");
        }
    }

    #endregion

    #region Round-Robin Match Generation Tests

    [TestMethod]
    public void GenerateGroupMatches_WithGroupOf10Bots_ShouldGenerate45Matches()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(10, GameType.RPSLS);
        var group = new Group
        {
            GroupId = "Test-Group-1",
            Bots = bots
        };

        // Act
        var matches = _engine.GenerateGroupMatches(group);

        // Assert
        // Round-robin for 10 bots = 10 * 9 / 2 = 45 matches
        Assert.AreEqual(45, matches.Count);
    }

    [TestMethod]
    public void GenerateGroupMatches_WithGroupOf5Bots_ShouldGenerate10Matches()
    {
        // Arrange - Round-robin for 5 bots = 5 * 4 / 2 = 10 matches
        var bots = TestHelpers.CreateDummyBots(5, GameType.RPSLS);
        var group = new Group
        {
            GroupId = "Test-Group-1",
            Bots = bots
        };

        // Act
        var matches = _engine.GenerateGroupMatches(group);

        // Assert
        Assert.AreEqual(10, matches.Count);
    }

    [TestMethod]
    public void GenerateGroupMatches_ShouldCreateAllPossiblePairings()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(4, GameType.RPSLS);
        var group = new Group
        {
            GroupId = "Test-Group-1",
            Bots = bots
        };

        // Act
        var matches = _engine.GenerateGroupMatches(group);

        // Assert
        Assert.AreEqual(6, matches.Count);
        var pairSet = new HashSet<string>();
        foreach (var (bot1, bot2) in matches)
        {
            var key1 = $"{bot1.TeamName}-{bot2.TeamName}";
            var key2 = $"{bot2.TeamName}-{bot1.TeamName}";
            Assert.IsFalse(pairSet.Contains(key1) || pairSet.Contains(key2), "Duplicate pairing found");
            pairSet.Add(key1);
        }
    }

    #endregion

    #region Integration Tests for Group Management

    [TestMethod]
    public void InitializeTournament_ShouldCreateGroupsAndGenerateAllMatches()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(30, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();

        // Act
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.IsNotNull(matches);
        // 30 bots with GroupCount=10 → 10 groups of 3 bots = 10 * 3 = 30 matches
        Assert.AreEqual(30, matches.Count);
    }

    [TestMethod]
    public void GetNextMatches_CalledTwice_ShouldReturnSameMatches()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches1 = _engine.GetNextMatches();
        var matches2 = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(matches1.Count, matches2.Count, 
            "Calling GetNextMatches should be idempotent until matches are recorded");
    }

    [TestMethod]
    public void GetNextMatches_AllBotsInGroup_ShouldPlayEachOtherExactlyOnce()
    {
        // Arrange - Use GroupCount=1 to put all 10 bots in one group
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

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        var botMatchCount = new Dictionary<string, int>();
        foreach (var bot in bots)
        {
            botMatchCount[bot.TeamName] = 0;
        }

        foreach (var (bot1, bot2) in matches)
        {
            botMatchCount[bot1.TeamName]++;
            botMatchCount[bot2.TeamName]++;
        }

        // In round-robin with 10 bots in 1 group, each bot should play 9 matches
        foreach (var count in botMatchCount.Values)
        {
            Assert.AreEqual(9, count, "Each bot should play exactly 9 matches in a group of 10");
        }
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void GetNextMatches_With2Bots_ShouldReturnSingleMatch()
    {
        // Arrange - minimum viable tournament - use GroupCount=1 to put both bots in one group
        var bots = TestHelpers.CreateDummyBotInfos(2, GameType.RPSLS);
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

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(1, matches.Count, "2 bots in 1 group should result in exactly 1 match");
    }

    [TestMethod]
    public void GetNextMatches_With120Bots_ShouldGenerateCorrectNumberOfMatches()
    {
        // Arrange - maximum expected tournament size
        // 120 bots with GroupCount=10 → 10 groups of 12 bots each
        // 10 groups * (12*11/2) matches each = 10 * 66 = 660 matches total
        var bots = TestHelpers.CreateDummyBotInfos(120, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(660, matches.Count);
    }

    #endregion
}
