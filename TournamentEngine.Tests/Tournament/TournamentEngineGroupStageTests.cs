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
        // Arrange - 20 bots divided into 2 groups of 10 = 2 * (10*9/2) = 90 matches total
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(90, matches.Count, "20 bots in 2 groups of 10 should generate 90 round-robin matches");
    }

    [TestMethod]
    public void GetNextMatches_With60Bots_ShouldReturnCorrectNumberOfMatches()
    {
        // Arrange - 60 bots divided into 6 groups of 10 = 6 * (10*9/2) = 270 matches total
        var bots = TestHelpers.CreateDummyBotInfos(60, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(270, matches.Count, "60 bots in 6 groups of 10 should generate 270 round-robin matches");
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

    #region Group Creation Tests

    [TestMethod]
    public void CreateInitialGroups_With30Bots_ShouldCreate3Groups()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(30, GameType.RPSLS);

        // Act
        var groups = _engine.CreateInitialGroups(bots);

        // Assert
        Assert.AreEqual(3, groups.Count, "30 bots should create 3 groups");
        foreach (var group in groups)
        {
            Assert.AreEqual(10, group.Bots.Count, "Each group should have 10 bots");
        }
    }

    [TestMethod]
    public void CreateInitialGroups_With60Bots_ShouldCreate6Groups()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(60, GameType.RPSLS);

        // Act
        var groups = _engine.CreateInitialGroups(bots);

        // Assert
        Assert.AreEqual(6, groups.Count, "60 bots should create 6 groups");
        foreach (var group in groups)
        {
            Assert.AreEqual(10, group.Bots.Count, "Each group should have 10 bots");
        }
    }

    [TestMethod]
    public void CreateInitialGroups_With65Bots_ShouldDistributeEvenly()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBots(65, GameType.RPSLS);

        // Act
        var groups = _engine.CreateInitialGroups(bots);

        // Assert
        Assert.AreEqual(6, groups.Count, "65 bots should create 6 groups");
        var sizes = groups.Select(group => group.Bots.Count).ToList();
        var min = sizes.Min();
        var max = sizes.Max();
        Assert.IsTrue(max - min <= 1, "Group sizes should differ by at most 1 bot");
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
        // 30 bots in 3 groups of 10 = 3 * 45 = 135 matches
        Assert.AreEqual(135, matches.Count);
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
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
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

        // In round-robin with 10 bots, each bot should play 9 matches
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
        // Arrange - minimum viable tournament
        var bots = TestHelpers.CreateDummyBotInfos(2, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(1, matches.Count, "2 bots should result in exactly 1 match");
    }

    [TestMethod]
    public void GetNextMatches_With120Bots_ShouldGenerateCorrectNumberOfMatches()
    {
        // Arrange - maximum expected tournament size
        // 120 bots / 10 = 12 groups of 10 bots
        // 12 groups * 45 matches each = 540 matches total
        var bots = TestHelpers.CreateDummyBotInfos(120, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act
        var matches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(540, matches.Count);
    }

    #endregion
}
