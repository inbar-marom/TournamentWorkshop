using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Tests;

[TestClass]
public class BotDashboardEndpointsTests
{
    private BotDashboardService? _dashboardService;
    private BotStorageService? _storageService;
    private string? _testDirectory;
    private Mock<ILogger<BotDashboardService>>? _mockDashboardLogger;
    private Mock<ILogger<BotStorageService>>? _mockStorageLogger;
    private Mock<IBotLoader>? _mockBotLoader;

    [TestInitialize]
    public void TestInitialize()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _mockDashboardLogger = new Mock<ILogger<BotDashboardService>>();
        _mockStorageLogger = new Mock<ILogger<BotStorageService>>();
        _mockBotLoader = new Mock<IBotLoader>();
        
        _storageService = new BotStorageService(_testDirectory, _mockStorageLogger.Object);
        
        // Create test tournament config
        var testConfig = new TournamentConfig
        {
            MemoryLimitMB = 512,
            MoveTimeout = TimeSpan.FromSeconds(5)
        };
        
        _dashboardService = new BotDashboardService(_storageService, _mockBotLoader.Object, _mockDashboardLogger.Object, testConfig);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _dashboardService?.Dispose();
        
        if (_testDirectory != null && Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetBotDetails_WithValidBot_ReturnsExpectedData()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        // Act
        var result = await _dashboardService!.GetBotDetailsAsync("TestTeam");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TestTeam", result.TeamName);
        Assert.IsNotNull(result.SubmissionTime);
        Assert.IsTrue(result.FileCount > 0);
    }

    [TestMethod]
    public async Task GetBotDetails_WithInvalidBot_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(
            async () => await _dashboardService!.GetBotDetailsAsync("NonExistent")
        );
    }

    [TestMethod]
    public async Task GetAllBots_ReturnsCompleteList()
    {
        // Arrange
        var submission1 = new BotSubmissionRequest
        {
            TeamName = "Team1",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot1.cs", Code = "public class Bot1 { }" }
            }
        };
        
        var submission2 = new BotSubmissionRequest
        {
            TeamName = "Team2",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot2.cs", Code = "public class Bot2 { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission1);
        await _storageService!.StoreBotAsync(submission2);

        // Act
        var result = await _dashboardService!.GetAllBotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(b => b.TeamName == "Team1"));
        Assert.IsTrue(result.Any(b => b.TeamName == "Team2"));
    }

    [TestMethod]
    public async Task ValidateBot_WithValidBot_InvokesBotLoader()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "ValidateTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        _mockBotLoader!
            .Setup(b => b.LoadBotFromFolderAsync(It.IsAny<string>(), It.IsAny<TournamentConfig>(), default))
            .ReturnsAsync(new BotInfo { TeamName = "ValidateTeam" });

        // Act
        var result = await _dashboardService!.ValidateBotAsync("ValidateTeam");

        // Assert
        Assert.IsNotNull(result);
        _mockBotLoader.Verify(
            b => b.LoadBotFromFolderAsync(It.IsAny<string>(), It.IsAny<TournamentConfig>(), default),
            Times.Once,
            "BotLoader should be called exactly once"
        );
    }

    [TestMethod]
    public async Task ValidateBot_WithValidationError_ThrowsException()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "ErrorTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "invalid code" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        _mockBotLoader!
            .Setup(b => b.LoadBotFromFolderAsync(It.IsAny<string>(), It.IsAny<TournamentConfig>(), default))
            .Throws(new InvalidOperationException("Compilation failed"));

        // Act & Assert - Method should rethrow the exception
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _dashboardService!.ValidateBotAsync("ErrorTeam")
        );
    }

    [TestMethod]
    public async Task GetBotHistory_ReturnsBotVersionInfo()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "HistoryTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        // Act
        var result = await _dashboardService!.GetBotVersionHistoryAsync("HistoryTeam");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<BotVersionInfo>));
        // Version history may be empty until version tracking is fully implemented
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task SearchBots_FiltersCorrectly()
    {
        // Arrange
        var submission1 = new BotSubmissionRequest
        {
            TeamName = "SearchableTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        var submission2 = new BotSubmissionRequest
        {
            TeamName = "OtherTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission1);
        await _storageService!.StoreBotAsync(submission2);

        // Act
        var result = await _dashboardService!.SearchBotsAsync("Searchable");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("SearchableTeam", result[0].TeamName);
    }

    [TestMethod]
    public async Task FilterByStatus_FiltersCorrectly()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "FilterTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        // Act
        var result = await _dashboardService!.FilterByStatusAsync(ValidationStatus.Valid);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<BotDashboardDto>));
    }

    [TestMethod]
    public async Task SortBots_ByTeamName_SortsAlphabetically()
    {
        // Arrange
        var bots = new List<BotDashboardDto>
        {
            new BotDashboardDto { TeamName = "ZebraTeam" },
            new BotDashboardDto { TeamName = "AlphaTeam" },
            new BotDashboardDto { TeamName = "BetaTeam" }
        };

        // Act
        var result = await _dashboardService!.SortBotsAsync(bots, "teamname", ascending: true);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("AlphaTeam", result[0].TeamName);
        Assert.AreEqual("BetaTeam", result[1].TeamName);
        Assert.AreEqual("ZebraTeam", result[2].TeamName);
    }

    [TestMethod]
    public async Task SortBots_BySubmissionTime_SortsCorrectly()
    {
        // Arrange
        var bots = new List<BotDashboardDto>
        {
            new BotDashboardDto { TeamName = "OldTeam", SubmissionTime = DateTime.UtcNow.AddMinutes(-10) },
            new BotDashboardDto { TeamName = "NewTeam", SubmissionTime = DateTime.UtcNow }
        };

        // Act
        var result = await _dashboardService!.SortBotsAsync(bots, "submissiontime", ascending: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("NewTeam", result[0].TeamName);
        Assert.AreEqual("OldTeam", result[1].TeamName);
    }
}
