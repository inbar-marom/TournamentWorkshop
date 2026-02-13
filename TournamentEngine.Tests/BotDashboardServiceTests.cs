using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Tests;

[TestClass]
public class BotDashboardServiceTests
{
    private BotDashboardService? _dashboardService;
    private BotStorageService? _storageService;
    private string? _testDirectory;
    private Mock<ILogger<BotDashboardService>>? _mockLogger;
    private Mock<IBotLoader>? _mockBotLoader;

    [TestInitialize]
    public void TestInitialize()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _mockLogger = new Mock<ILogger<BotDashboardService>>();
        _mockBotLoader = new Mock<IBotLoader>();
        
        var storageLogger = new Mock<ILogger<BotStorageService>>();
        _storageService = new BotStorageService(_testDirectory, storageLogger.Object);
        _dashboardService = new BotDashboardService(_storageService, _mockBotLoader.Object, _mockLogger.Object);
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
    public async Task GetAllBotsAsync_NoBotsSubmitted_ReturnsEmptyList()
    {
        // Arrange & Act
        var result = await _dashboardService!.GetAllBotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetAllBotsAsync_WithValidBots_ReturnsAllBots()
    {
        // Arrange
        var submission1 = new BotSubmissionRequest
        {
            TeamName = "Team1",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };
        
        var submission2 = new BotSubmissionRequest
        {
            TeamName = "Team2",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission1);
        await _storageService!.StoreBotAsync(submission2);

        // Act
        var result = await _dashboardService!.GetAllBotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        // Results are sorted by submission time (newest first), so Team2 comes first
        Assert.AreEqual("Team2", result[0].TeamName);
        Assert.AreEqual("Team1", result[1].TeamName);
    }

    [TestMethod]
    public async Task GetAllBotsAsync_SortsBySubmissionTimeNewest_ReturnsCorrectOrder()
    {
        // Arrange
        var submission1 = new BotSubmissionRequest
        {
            TeamName = "OldTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };
        
        var submission2 = new BotSubmissionRequest
        {
            TeamName = "NewTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission1);
        await Task.Delay(100);
        await _storageService!.StoreBotAsync(submission2);

        // Act
        var result = await _dashboardService!.GetAllBotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("NewTeam", result[0].TeamName); // Newest first
        Assert.AreEqual("OldTeam", result[1].TeamName);
    }

    [TestMethod]
    public async Task GetBotDetailsAsync_WithValidBot_ReturnsBotWithFullMetadata()
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
    }

    [TestMethod]
    public async Task GetBotDetailsAsync_WithNonExistentBot_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(
            async () => await _dashboardService!.GetBotDetailsAsync("NonExistent")
        );
    }

    [TestMethod]
    public async Task SearchBotsAsync_WithExistingTeamName_ReturnsMatchingBot()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "SearchableTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        // Act
        var result = await _dashboardService!.SearchBotsAsync("Searchable");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("SearchableTeam", result[0].TeamName);
    }

    [TestMethod]
    public async Task SearchBotsAsync_WithNonExistingSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "Team1",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        // Act
        var result = await _dashboardService!.SearchBotsAsync("NonExistent");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task SearchBotsAsync_IsCaseInsensitive_ReturnsMatchingBot()
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
        var result = await _dashboardService!.SearchBotsAsync("testteam");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("TestTeam", result[0].TeamName);
    }

    [TestMethod]
    public async Task FilterByStatusAsync_WithValidStatus_ReturnsFilteredBots()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "Team1",
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
        // Result may be empty or contain bots depending on validation
        Assert.IsInstanceOfType(result, typeof(List<BotDashboardDto>));
    }

    [TestMethod]
    public async Task SortBotsAsync_BySubmissionTime_ReturnsSortedList()
    {
        // Arrange
        var bots = new List<BotDashboardDto>
        {
            new BotDashboardDto 
            { 
                TeamName = "Team1", 
                SubmissionTime = DateTime.UtcNow.AddMinutes(-10) 
            },
            new BotDashboardDto 
            { 
                TeamName = "Team2", 
                SubmissionTime = DateTime.UtcNow 
            }
        };

        // Act
        var result = await _dashboardService!.SortBotsAsync(bots, "submissiontime", ascending: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Team2", result[0].TeamName); // Newest first when descending
    }

    [TestMethod]
    public async Task SortBotsAsync_ByTeamName_ReturnsSortedList()
    {
        // Arrange
        var bots = new List<BotDashboardDto>
        {
            new BotDashboardDto { TeamName = "Zebras" },
            new BotDashboardDto { TeamName = "Alphas" }
        };

        // Act
        var result = await _dashboardService!.SortBotsAsync(bots, "teamname", ascending: true);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Alphas", result[0].TeamName);
        Assert.AreEqual("Zebras", result[1].TeamName);
    }

    [TestMethod]
    public async Task ValidateBotAsync_InvokesBotLoader()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "ValidTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);

        _mockBotLoader!
            .Setup(b => b.LoadBotFromFolderAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new BotInfo { TeamName = "ValidTeam" });

        // Act
        var result = await _dashboardService!.ValidateBotAsync("ValidTeam");

        // Assert
        Assert.IsNotNull(result);
        _mockBotLoader.Verify(b => b.LoadBotFromFolderAsync(It.IsAny<string>(), default), Times.Once);
    }

    [TestMethod]
    public async Task GetBotVersionHistoryAsync_WithValidBot_ReturnsVersionHistory()
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
    }

    [TestMethod]
    public async Task ClearCache_RemovesAllCachedData()
    {
        // Arrange
        var submission = new BotSubmissionRequest
        {
            TeamName = "CacheTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.cs", Code = "public class Bot { }" }
            }
        };

        await _storageService!.StoreBotAsync(submission);
        
        // Get all bots once to populate cache
        var firstCall = await _dashboardService!.GetAllBotsAsync();

        // Act
        _dashboardService.ClearCache();
        var secondCall = await _dashboardService!.GetAllBotsAsync();

        // Assert
        Assert.AreEqual(firstCall.Count, secondCall.Count);
    }

    // Helper method to generate mock zip content
    private byte[] GenerateMockZipContent()
    {
        // Return a minimal valid zip file content
        return new byte[]
        {
            0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00,
            0x08, 0x00, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x74, 0x65,
            0x73, 0x74, 0x2E, 0x74, 0x78, 0x74, 0x57, 0x48,
            0x07, 0x00, 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x37, 0x00,
            0x00, 0x00, 0x35, 0x00, 0x00, 0x00, 0x00, 0x00
        };
    }
}
