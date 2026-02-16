namespace TournamentEngine.Tests.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.Common;

/// <summary>
/// Tests for Bot Verification Endpoint - Phase 1.2
/// Tests the /api/bots/verify endpoint for pre-submission validation
/// </summary>
[TestClass]
public class BotVerificationTests
{
    [TestMethod]
    public void BotVerificationRequest_ValidBot_HasRequiredFields()
    {
        // Arrange
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "bot.py",
                    Code = @"
class Bot:
    def get_move(self, game_state):
        return 'Rock'
"
                }
            },
            GameType = GameType.RPSLS
        };

        // Assert
        Assert.IsNotNull(request.TeamName);
        Assert.IsTrue(request.Files.Count > 0);
        Assert.IsNotNull(request.GameType);
    }

    [TestMethod]
    public void BotVerificationRequest_NoGameType_StillValid()
    {
        // Arrange - Optional GameType
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.py", Code = "class Bot: pass" }
            }
        };

        // Assert
        Assert.IsNotNull(request.TeamName);
        Assert.IsNull(request.GameType, "GameType should be optional");
    }

    [TestMethod]
    public void BotVerificationResult_FailureCase_HasErrors()
    {
        // Arrange
        var result = new BotVerificationResult
        {
            IsValid = false,
            Errors = new List<string>
            {
                "Missing get_move method",
                "Invalid class structure"
            },
            Warnings = new List<string>
            {
                "Bot may timeout"
            },
            Message = "Bot validation failed"
        };

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(2, result.Errors.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        Assert.IsTrue(result.Message.Contains("failed"));
    }

    [TestMethod]
    public void BotVerificationResult_SuccessCase_NoErrors()
    {
        // Arrange
        var result = new BotVerificationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            Message = "Bot is valid"
        };

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void BotVerificationResult_SuccessWithWarnings_IsStillValid()
    {
        // Arrange - Valid but with warnings
        var result = new BotVerificationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>
            {
                "Large file size detected",
                "Consider optimization"
            },
            Message = "Bot is valid with warnings"
        };

        // Assert
        Assert.IsTrue(result.IsValid, "Bot can be valid with warnings");
        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual(2, result.Warnings.Count);
    }

    [TestMethod]
    public void BotVerificationResult_TimeoutScenario_HasAppropriateError()
    {
        // Arrange
        var result = new BotVerificationResult
        {
            IsValid = false,
            Errors = new List<string>
            {
                "Bot timed out during test execution"
            },
            Message = "Bot failed validation due to timeout"
        };

        // Assert
        Assert.IsFalse(result.IsValid);
        var hasTimeoutError = result.Errors.Any(e => 
            e.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(hasTimeoutError, "Should have timeout-related error");
    }

    [TestMethod]
    public void BotVerificationRequest_MultipleFiles_AllIncluded()
    {
        // Arrange
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.py", Code = "class Bot: pass" },
                new BotFile { FileName = "utils.py", Code = "def helper(): pass" },
                new BotFile { FileName = "config.py", Code = "CONFIG = {}" }
            },
            GameType = GameType.ColonelBlotto
        };

        // Assert
        Assert.AreEqual(3, request.Files.Count);
        Assert.IsTrue(request.Files.Any(f => f.FileName == "bot.py"));
        Assert.IsTrue(request.Files.Any(f => f.FileName == "utils.py"));
        Assert.IsTrue(request.Files.Any(f => f.FileName == "config.py"));
    }
}
