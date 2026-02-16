namespace TournamentEngine.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using Microsoft.Extensions.Logging;

[TestClass]
public class BotStorageServiceTests
{
    private BotStorageService _service = null!;
    private string _tempDirectory = null!;
    private ILogger<BotStorageService> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create temporary directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"bot-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Create mock logger
        var loggerFactory = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
        _logger = loggerFactory.CreateLogger<BotStorageService>();

        // Initialize service
        _service = new BotStorageService(_tempDirectory, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Delete temporary directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region Step 2.1: Submit Single Bot Tests

    [TestMethod]
    public async Task SubmitBot_ValidSingleFileBot_StoresSuccessfully()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamRocket",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "public class RocketBot : IBot { }" }
            }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsTrue(result.Success, "Bot submission should succeed");
        Assert.AreEqual("TeamRocket", result.TeamName);
        Assert.IsNotNull(result.SubmissionId);
        Assert.IsTrue(result.Message.Contains("submitted successfully"));

        // Verify files exist
        var expectedFolder = Path.Combine(_tempDirectory, "TeamRocket_v1");
        Assert.IsTrue(Directory.Exists(expectedFolder), "Team folder should exist");
        Assert.IsTrue(File.Exists(Path.Combine(expectedFolder, "Bot.cs")), "Bot.cs file should exist");
    }

    [TestMethod]
    public async Task SubmitBot_MultiFileBot_StoresAllFiles()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamBlue",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "public class BlueBot : IBot { }" },
                new() { FileName = "AI.cs", Code = "public class GameAI { }" },
                new() { FileName = "Utils.cs", Code = "public static class Utils { }" }
            }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("TeamBlue", result.TeamName);

        var expectedFolder = Path.Combine(_tempDirectory, "TeamBlue_v1");
        Assert.IsTrue(File.Exists(Path.Combine(expectedFolder, "Bot.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(expectedFolder, "AI.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(expectedFolder, "Utils.cs")));

        // Verify file content
        var botContent = File.ReadAllText(Path.Combine(expectedFolder, "Bot.cs"));
        Assert.AreEqual("public class BlueBot : IBot { }", botContent);
    }

    [TestMethod]
    public async Task SubmitBot_EmptyTeamName_ReturnsFail()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "",
            Files = new List<BotFile> { new() { FileName = "Bot.cs", Code = "code" } }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Message.Contains("Team name is required"));
    }

    [TestMethod]
    public async Task SubmitBot_NoFiles_ReturnsFail()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamRocket",
            Files = new List<BotFile>()
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Message.Contains("At least one file", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SubmitBot_NullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.StoreBotAsync(null!);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Message.Contains("null"));
    }

    [TestMethod]
    public async Task SubmitBot_InvalidTeamName_ReturnsFail()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "Team@Invalid!",
            Files = new List<BotFile> { new() { FileName = "Bot.cs", Code = "code" } }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("alphanumeric")));
    }

    #endregion

    #region Step 2.2: Multi-File Bot Tests

    [TestMethod]
    public async Task SubmitBot_LargeFileExceedsLimit_ReturnsFail()
    {
        // Arrange - 55KB code (exceeds 50KB limit per file)
        var largeCode = new string('a', 55_000);
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamLarge",
            Files = new List<BotFile>
            {
                new() { FileName = "Large.cs", Code = largeCode }
            }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("exceeds maximum size")));
    }

    [TestMethod]
    public async Task SubmitBot_TotalSizeExceedsLimit_ReturnsFail()
    {
        // Arrange - Total 600KB (exceeds 500KB limit)
        var code = new string('a', 200_000);
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamTooLarge",
            Files = new List<BotFile>
            {
                new() { FileName = "File1.cs", Code = code },
                new() { FileName = "File2.cs", Code = code },
                new() { FileName = "File3.cs", Code = code }
            }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Total submission size")));
    }

    [TestMethod]
    public async Task SubmitBot_DuplicateFileNames_ReturnsFail()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamDupe",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "code1" },
                new() { FileName = "Bot.cs", Code = "code2" }  // Duplicate
            }
        };

        // Act
        var result = await _service.StoreBotAsync(request);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Duplicate file name")));
    }

    #endregion

    #region Step 2.3: Thread Safety Tests

    [TestMethod]
    public async Task SubmitBot_ConcurrentSubmissions_AllSucceed()
    {
        // Arrange - Submit 10 different bots concurrently
        var tasks = new List<Task<BotSubmissionResult>>();
        
        for (int i = 0; i < 10; i++)
        {
            var teamName = $"Team{i}";
            var request = new BotSubmissionRequest
            {
                TeamName = teamName,
                Files = new List<BotFile>
                {
                    new() { FileName = "Bot.cs", Code = $"// Team {i}" }
                }
            };

            tasks.Add(_service.StoreBotAsync(request));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(10, results.Length);
        Assert.IsTrue(results.All(r => r.Success), "All submissions should succeed");
        
        // Verify all folders exist
        for (int i = 0; i < 10; i++)
        {
            var folder = Path.Combine(_tempDirectory, $"Team{i}_v1");
            Assert.IsTrue(Directory.Exists(folder), $"Team{i} folder should exist");
        }
    }

    [TestMethod]
    public async Task SubmitBot_SameBotTwice_SecondFails()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamRocket",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "v1" }
            }
        };

        // Act - First submission
        var result1 = await _service.StoreBotAsync(request);

        // Immediately try second submission with same team (before cleanup)
        var request2 = new BotSubmissionRequest
        {
            TeamName = "TeamRocket",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "v1" }
            }
        };
        var result2 = await _service.StoreBotAsync(request2);

        // Assert - Both should succeed but second should be v2
        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
        Assert.IsTrue(result2.Message.Contains("v2"));
    }

    #endregion

    #region Step 2.4: Overwrite Logic Tests

    [TestMethod]
    public async Task SubmitBot_SameTeamTwice_KeepsLatestVersion()
    {
        // Arrange
        var request1 = new BotSubmissionRequest
        {
            TeamName = "TeamA",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "v1 code" }
            }
        };

        // Act - First submission
        var result1 = await _service.StoreBotAsync(request1);
        Assert.IsTrue(result1.Success);

        // Second submission with same team
        var request2 = new BotSubmissionRequest
        {
            TeamName = "TeamA",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "v2 code" },
                new() { FileName = "Helper.cs", Code = "helper" }
            }
        };
        var result2 = await _service.StoreBotAsync(request2);
        Assert.IsTrue(result2.Success);

        // Assert - extract version from message format "Bot submitted successfully (v2)"
        var versionMatch = System.Text.RegularExpressions.Regex.Match(result2.Message, @"v(\d+)");
        Assert.IsTrue(versionMatch.Success, "Version not found in message");
        var version = int.Parse(versionMatch.Groups[1].Value);
        Assert.AreEqual(2, version);
        
        // Verify v2 folder exists and contains both files
        var v2Folder = Path.Combine(_tempDirectory, "TeamA_v2");
        Assert.IsTrue(Directory.Exists(v2Folder));
        Assert.IsTrue(File.Exists(Path.Combine(v2Folder, "Bot.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(v2Folder, "Helper.cs")));

        // Verify v1 folder is deleted
        var v1Folder = Path.Combine(_tempDirectory, "TeamA_v1");
        Assert.IsFalse(Directory.Exists(v1Folder), "Old version folder should be deleted");
    }

    [TestMethod]
    public async Task SubmitBot_VersionIncrementsCorrectly()
    {
        // Arrange & Act
        for (int i = 1; i <= 3; i++)
        {
            var request = new BotSubmissionRequest
            {
                TeamName = "TeamVersion",
                Files = new List<BotFile>
                {
                    new() { FileName = "Bot.cs", Code = $"v{i}" }
                }
            };
            var result = await _service.StoreBotAsync(request);
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Message.Contains($"v{i}"));

            var expectedFolder = Path.Combine(_tempDirectory, $"TeamVersion_v{i}");
            Assert.IsTrue(Directory.Exists(expectedFolder));
        }
    }

    #endregion

    #region Step 2.5: List and Delete Operations Tests

    [TestMethod]
    public async Task GetAllSubmissions_EmptyService_ReturnsEmptyList()
    {
        // Act
        var submissions = _service.GetAllSubmissions();

        // Assert
        Assert.AreEqual(0, submissions.Count);
    }

    [TestMethod]
    public async Task GetAllSubmissions_MultipleSubmissions_ReturnsAll()
    {
        // Arrange - Submit 3 bots
        for (int i = 0; i < 3; i++)
        {
            var request = new BotSubmissionRequest
            {
                TeamName = $"Team{i}",
                Files = new List<BotFile>
                {
                    new() { FileName = "Bot.cs", Code = $"team{i}" }
                }
            };
            await _service.StoreBotAsync(request);
        }

        // Act
        var submissions = _service.GetAllSubmissions();

        // Assert
        Assert.AreEqual(3, submissions.Count);
        Assert.IsTrue(submissions.Any(s => s.TeamName == "Team0"));
        Assert.IsTrue(submissions.Any(s => s.TeamName == "Team1"));
        Assert.IsTrue(submissions.Any(s => s.TeamName == "Team2"));
    }

    [TestMethod]
    public async Task GetSubmission_ExistingTeam_ReturnsMetadata()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamRocket",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "code" }
            }
        };
        await _service.StoreBotAsync(request);

        // Act
        var submission = _service.GetSubmission("TeamRocket");

        // Assert
        Assert.IsNotNull(submission);
        Assert.AreEqual("TeamRocket", submission.TeamName);
        Assert.AreEqual(1, submission.Version);
    }

    [TestMethod]
    public async Task GetSubmission_NonExistentTeam_ReturnsNull()
    {
        // Act
        var submission = _service.GetSubmission("NonExistent");

        // Assert
        Assert.IsNull(submission);
    }

    [TestMethod]
    public async Task DeleteBot_ExistingBot_DeletesSuccessfully()
    {
        // Arrange
        var request = new BotSubmissionRequest
        {
            TeamName = "TeamDelete",
            Files = new List<BotFile>
            {
                new() { FileName = "Bot.cs", Code = "code" }
            }
        };
        await _service.StoreBotAsync(request);

        var folder = Path.Combine(_tempDirectory, "TeamDelete_v1");
        Assert.IsTrue(Directory.Exists(folder), "Folder should exist before delete");

        // Act
        var result = await _service.DeleteBotAsync("TeamDelete");

        // Assert
        Assert.IsTrue(result);
        Assert.IsFalse(Directory.Exists(folder), "Folder should be deleted");
        Assert.IsNull(_service.GetSubmission("TeamDelete"));
    }

    [TestMethod]
    public async Task DeleteBot_NonExistentBot_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteBotAsync("NonExistent");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteBot_EmptyTeamName_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteBotAsync("");

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public async Task FullWorkflow_SubmitListDelete_WorksCorrectly()
    {
        // Arrange & Act - Submit 3 bots
        var teamNames = new[] { "Alpha", "Beta", "Gamma" };
        foreach (var teamName in teamNames)
        {
            var request = new BotSubmissionRequest
            {
                TeamName = teamName,
                Files = new List<BotFile>
                {
                    new() { FileName = "Bot.cs", Code = $"public class {teamName}Bot {{}}" }
                }
            };
            var result = await _service.StoreBotAsync(request);
            Assert.IsTrue(result.Success);
        }

        // Assert - List should have 3
        var submissions = _service.GetAllSubmissions();
        Assert.AreEqual(3, submissions.Count);

        // Act - Delete one
        var deleteResult = await _service.DeleteBotAsync("Beta");
        Assert.IsTrue(deleteResult);

        // Assert - List should have 2
        submissions = _service.GetAllSubmissions();
        Assert.AreEqual(2, submissions.Count);
        Assert.IsNull(_service.GetSubmission("Beta"));
        Assert.IsNotNull(_service.GetSubmission("Alpha"));
        Assert.IsNotNull(_service.GetSubmission("Gamma"));
    }

    #endregion
}
