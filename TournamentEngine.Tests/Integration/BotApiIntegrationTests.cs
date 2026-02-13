namespace TournamentEngine.Tests.Integration;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Integration tests for Step 13 API - Bot Submission → Storage → Loading
/// Tests the complete workflow without HTTP layer complexity
/// </summary>
[TestClass]
public class BotApiIntegrationTests
{
    private string _tempDirectory = null!;
    private BotStorageService _botStorage = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"BotIntegrationTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<BotStorageService>>();
        _botStorage = new BotStorageService(_tempDirectory, mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Test the complete workflow: Submit bot → Store files → Can retrieve and execute
    /// </summary>
    [TestMethod]
    public async Task Workflow_SubmitBotAndLoadFromStorage_Works()
    {
        // Arrange - Create a bot that can handle all game types
        var botCode = @"
using TournamentEngine.Core;
using System.Collections.Generic;

public class IntegrationTestBot : IBot
{
    public BotAction GetAction(GameType game, BotGameState state)
    {
        // For testing, always play Rock in RPSLS
        return new BotAction { Action = ""Rock"", TargetPlayerId = null };
    }
    
    public void SetTeamInfo(string teamName, List<string> allies, List<string> opponents) { }
}
";

        var request = new BotSubmissionRequest
        {
            TeamName = "IntegrationBotTeam",
            Files = new()
            {
                new() { FileName = "IntegrationBot.cs", Code = botCode }
            },
            Overwrite = true
        };

        // Act 1: Submit bot through API
        var submitResult = await _botStorage.StoreBotAsync(request);

        // Assert 1: Submission successful
        Assert.IsTrue(submitResult.Success, "Bot submission should succeed");
        Assert.AreEqual("IntegrationBotTeam", submitResult.TeamName);
        Assert.IsTrue(!string.IsNullOrEmpty(submitResult.SubmissionId));

        // Act 2: Retrieve bot metadata
        var metadata = _botStorage.GetSubmission("IntegrationBotTeam");

        // Assert 2: Metadata correct
        Assert.IsNotNull(metadata);
        Assert.AreEqual("IntegrationBotTeam", metadata.TeamName);
        Assert.AreEqual(1, metadata.FileCount);
        Assert.AreEqual(1, metadata.Version);
        Assert.IsTrue(metadata.TotalSizeBytes > 0);

        // Act 3: Verify files exist
        var botFolder = Path.Combine(_tempDirectory, "IntegrationBotTeam_v1");
        var botFile = Path.Combine(botFolder, "IntegrationBot.cs");

        // Assert 3: Files stored correctly
        Assert.IsTrue(Directory.Exists(botFolder), "Bot folder should exist");
        Assert.IsTrue(File.Exists(botFile), "Bot file should exist");
        var storedCode = File.ReadAllText(botFile);
        Assert.IsTrue(storedCode.Contains("IntegrationTestBot"), "Stored code should match submitted code");
    }

    /// <summary>
    /// Test submitting multiple bots sequentially and verifying all are stored correctly
    /// </summary>
    [TestMethod]
    public async Task Workflow_SubmitMultipleBotsSequentially_AllStored()
    {
        // Arrange
        var botTemplates = new[]
        {
            ("Team1", "public class Bot1 : IBot { public BotAction GetAction(GameType g, BotGameState s) => null; public void SetTeamInfo(string t, List<string> a, List<string> o){} }"),
            ("Team2", "public class Bot2 : IBot { public BotAction GetAction(GameType g, BotGameState s) => null; public void SetTeamInfo(string t, List<string> a, List<string> o){} }"),
            ("Team3", "public class Bot3 : IBot { public BotAction GetAction(GameType g, BotGameState s) => null; public void SetTeamInfo(string t, List<string> a, List<string> o){} }")
        };

        // Act - Submit all bots
        var results = new List<BotSubmissionResult>();
        foreach (var (teamName, code) in botTemplates)
        {
            var request = new BotSubmissionRequest
            {
                TeamName = teamName,
                Files = new() { new() { FileName = "Bot.cs", Code = code } },
                Overwrite = true
            };
            var result = await _botStorage.StoreBotAsync(request);
            results.Add(result);
        }

        // Assert - All submitted successfully
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.TrueForAll(r => r.Success), "All submissions should succeed");

        // Act 2 - Retrieve all
        var allBots = _botStorage.GetAllSubmissions();

        // Assert 2 - All stored
        Assert.AreEqual(3, allBots.Count);
        Assert.IsTrue(allBots.Exists(b => b.TeamName == "Team1"));
        Assert.IsTrue(allBots.Exists(b => b.TeamName == "Team2"));
        Assert.IsTrue(allBots.Exists(b => b.TeamName == "Team3"));
    }

    /// <summary>
    /// Test resubmission workflow with versioning
    /// </summary>
    [TestMethod]
    public async Task Workflow_ResubmitBotWithUpdates_VersionsCorrectly()
    {
        // Arrange - First submission
        var firstRequest = new BotSubmissionRequest
        {
            TeamName = "VersionTeam",
            Files = new()
            {
                new() { FileName = "Bot.cs", Code = "v1 code" }
            },
            Overwrite = true
        };

        // Act 1 - Submit v1
        var result1 = await _botStorage.StoreBotAsync(firstRequest);
        Assert.IsTrue(result1.Success);

        // Act 2 - Resubmit with improvements
        var secondRequest = new BotSubmissionRequest
        {
            TeamName = "VersionTeam",
            Files = new()
            {
                new() { FileName = "Bot.cs", Code = "v2 improved code"},
                new() { FileName = "Strategy.cs", Code = "v2 strategy logic" }
            },
            Overwrite = true
        };
        var result2 = await _botStorage.StoreBotAsync(secondRequest);
        Assert.IsTrue(result2.Success);

        // Act 3 - Get latest version
        var latest = _botStorage.GetSubmission("VersionTeam");

        // Assert - Version incremented and files updated
        Assert.IsNotNull(latest);
        Assert.AreEqual(2, latest.Version);
        Assert.AreEqual(2, latest.FileCount); // v2 has 2 files
        
        // Verify v1 folder is deleted
        var v1Folder = Path.Combine(_tempDirectory, "VersionTeam_v1");
        Assert.IsFalse(Directory.Exists(v1Folder), "Old version folder should be cleaned up");
        
        // Verify v2 folder exists with new files
        var v2Folder = Path.Combine(_tempDirectory, "VersionTeam_v2");
        Assert.IsTrue(Directory.Exists(v2Folder));
        Assert.IsTrue(File.Exists(Path.Combine(v2Folder, "Bot.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(v2Folder, "Strategy.cs")));
    }

    /// <summary>
    /// Test batch-like submission scenario (sequential calls)
    /// </summary>
    [TestMethod]
    public async Task Workflow_BatchLikeSubmission_HandlesMultipleTeams()
    {
        // Simulate batch submission behavior
        var batchRequests = new[]
        {
            new BotSubmissionRequest { TeamName = "A", Files = new() { new() { FileName = "A.cs", Code = "a" } }, Overwrite = true },
            new BotSubmissionRequest { TeamName = "B", Files = new() { new() { FileName = "B.cs", Code = "b" } }, Overwrite = true },
            new BotSubmissionRequest { TeamName = "C", Files = new() { new() { FileName = "C.cs", Code = "c" } }, Overwrite = true }
        };

        // Act - Submit all
        var successCount = 0;
        var failureCount = 0;
        foreach (var request in batchRequests)
        {
            var result = await _botStorage.StoreBotAsync(request);
            if (result.Success) successCount++;
            else failureCount++;
        }

        // Assert
        Assert.AreEqual(3, successCount);
        Assert.AreEqual(0, failureCount);
        Assert.AreEqual(3, _botStorage.GetAllSubmissions().Count);
    }

    /// <summary>
    /// Test deletion workflow
    /// </summary>
    [TestMethod]
    public async Task Workflow_SubmitThenDelete_RemovesSuccessfully()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "DeleteTest",
            Files = new() { new() { FileName = "Bot.cs", Code = "to delete" } },
            Overwrite = true
        };

        // Act 1 - Submit
        var submitResult = await _botStorage.StoreBotAsync(request);
        Assert.IsTrue(submitResult.Success);

        // Act 2 - Verify it exists
        var before = _botStorage.GetSubmission("DeleteTest");
        Assert.IsNotNull(before);

        // Act 3 - Delete
        var deleteSuccess = await _botStorage.DeleteBotAsync("DeleteTest");
        Assert.IsTrue(deleteSuccess);

        // Act 4 - Verify it's gone
        var after = _botStorage.GetSubmission("DeleteTest");
        Assert.IsNull(after);
        
        var allBots = _botStorage.GetAllSubmissions();
        Assert.IsFalse(allBots.Any(b => b.TeamName == "DeleteTest"));
    }

    /// <summary>
    /// Test concurrent submissions don't corrupt data
    /// </summary>
    [TestMethod]
    public async Task Workflow_ConcurrentSubmissions_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task<BotSubmissionResult>>();
        for (int i = 1; i <= 5; i++)
        {
            var teamName = $"ConcurrentTeam{i}";
            var request = new BotSubmissionRequest
            {
                TeamName = teamName,
                Files = new() { new() { FileName = "Bot.cs", Code = $"bot for {teamName}" } },
                Overwrite = true
            };
            tasks.Add(_botStorage.StoreBotAsync(request));
        }

        // Act - Submit all concurrently
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(5, results.Length);
        Assert.IsTrue(results.All(r => r.Success), "All concurrent submissions should succeed");
        Assert.AreEqual(5, _botStorage.GetAllSubmissions().Count);
    }
}
