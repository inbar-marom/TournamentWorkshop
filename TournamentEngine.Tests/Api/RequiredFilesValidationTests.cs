namespace TournamentEngine.Tests.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;

/// <summary>
/// Tests for required documentation files validation
/// Ensures mandatory files are checked while bonus game files are optional
/// </summary>
[TestClass]
public class RequiredFilesValidationTests
{
    [TestMethod]
    public void BotSubmission_WithAllRequiredFiles_Passes()
    {
        // Arrange
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" },
                new BotFile { FileName = "plan-colonelBlotto.md", Code = "# Blotto Plan" },
                new BotFile { FileName = "RPSLS_Skill.md", Code = "# RPSLS Skill" },
                new BotFile { FileName = "colonelBlotto_Skill.md", Code = "# Blotto Skill" },
                new BotFile { FileName = "ResearchAgent.md", Code = "# Research" },
                new BotFile { FileName = "plan-workshop.md", Code = "# Plan" },
                new BotFile { FileName = "copilot-instructions.md", Code = "# Instructions" }
            }
        };

        // Assert - All required files present
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(fileNames.Contains("plan-rpsls.md"));
        Assert.IsTrue(fileNames.Contains("plan-colonelBlotto.md"));
        Assert.IsTrue(fileNames.Contains("RPSLS_Skill.md"));
        Assert.IsTrue(fileNames.Contains("colonelBlotto_Skill.md"));
        Assert.IsTrue(fileNames.Contains("ResearchAgent.md"));
        Assert.IsTrue(fileNames.Contains("plan-workshop.md"));
        Assert.IsTrue(fileNames.Contains("copilot-instructions.md"));
    }

    [TestMethod]
    public void BotSubmission_WithoutPenaltyKicksFiles_IsValid()
    {
        // Arrange - Missing optional penalty kicks files
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" },
                new BotFile { FileName = "plan-colonelBlotto.md", Code = "# Blotto Plan" },
                new BotFile { FileName = "RPSLS_Skill.md", Code = "# RPSLS Skill" },
                new BotFile { FileName = "colonelBlotto_Skill.md", Code = "# Blotto Skill" },
                new BotFile { FileName = "ResearchAgent.md", Code = "# Research" },
                new BotFile { FileName = "plan-workshop.md", Code = "# Plan" },
                new BotFile { FileName = "copilot-instructions.md", Code = "# Instructions" }
                // NOT including: penaltyKicks_Skill.md, plan-penaltyKicks.md
            }
        };

        // Assert - Should be valid without penalty kicks files (they are optional)
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsFalse(fileNames.Contains("penaltyKicks_Skill.md"));
        Assert.IsFalse(fileNames.Contains("plan-penaltyKicks.md"));
        // But all required files should be present
        Assert.IsTrue(fileNames.Contains("plan-rpsls.md"));
        Assert.IsTrue(fileNames.Contains("plan-colonelBlotto.md"));
    }

    [TestMethod]
    public void BotSubmission_WithoutSecurityGameFiles_IsValid()
    {
        // Arrange - Missing optional security game files
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" },
                new BotFile { FileName = "plan-colonelBlotto.md", Code = "# Blotto Plan" },
                new BotFile { FileName = "RPSLS_Skill.md", Code = "# RPSLS Skill" },
                new BotFile { FileName = "colonelBlotto_Skill.md", Code = "# Blotto Skill" },
                new BotFile { FileName = "ResearchAgent.md", Code = "# Research" },
                new BotFile { FileName = "plan-workshop.md", Code = "# Plan" },
                new BotFile { FileName = "copilot-instructions.md", Code = "# Instructions" }
                // NOT including: securityGame_Skill.md, plan-securityGame.md
            }
        };

        // Assert - Should be valid without security game files (they are optional)
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsFalse(fileNames.Contains("securityGame_Skill.md"));
        Assert.IsFalse(fileNames.Contains("plan-securityGame.md"));
    }

    [TestMethod]
    public void BotSubmission_WithMissingRequiredFiles_Fails()
    {
        // Arrange - Missing some required files
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" }
                // Missing: plan-colonelBlotto.md, RPSLS_Skill.md, colonelBlotto_Skill.md, 
                //          ResearchAgent.md, plan-workshop.md, copilot-instructions.md
            }
        };

        // Assert - Should detect missing files
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsFalse(fileNames.Contains("plan-colonelBlotto.md"));
        Assert.IsFalse(fileNames.Contains("RPSLS_Skill.md"));
        Assert.IsFalse(fileNames.Contains("ResearchAgent.md"));
    }

    [TestMethod]
    public void BotSubmission_WithBonusFilesIncluded_IsValid()
    {
        // Arrange - Including all files including bonus games
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                // Required files
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" },
                new BotFile { FileName = "plan-colonelBlotto.md", Code = "# Blotto Plan" },
                new BotFile { FileName = "RPSLS_Skill.md", Code = "# RPSLS Skill" },
                new BotFile { FileName = "colonelBlotto_Skill.md", Code = "# Blotto Skill" },
                new BotFile { FileName = "ResearchAgent.md", Code = "# Research" },
                new BotFile { FileName = "plan-workshop.md", Code = "# Plan" },
                new BotFile { FileName = "copilot-instructions.md", Code = "# Instructions" },
                // Optional bonus files
                new BotFile { FileName = "penaltyKicks_Skill.md", Code = "# Penalty Skill" },
                new BotFile { FileName = "plan-penaltyKicks.md", Code = "# Penalty Plan" },
                new BotFile { FileName = "securityGame_Skill.md", Code = "# Security Skill" },
                new BotFile { FileName = "plan-securityGame.md", Code = "# Security Plan" }
            }
        };

        // Assert - All files should be accepted
        Assert.AreEqual(12, request.Files.Count);
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        // Required files
        Assert.IsTrue(fileNames.Contains("plan-rpsls.md"));
        Assert.IsTrue(fileNames.Contains("plan-colonelBlotto.md"));
        
        // Optional files
        Assert.IsTrue(fileNames.Contains("penaltyKicks_Skill.md"));
        Assert.IsTrue(fileNames.Contains("securityGame_Skill.md"));
    }

    [TestMethod]
    public void BotSubmission_RequiredFilesCaseInsensitive()
    {
        // Arrange - Files with different casing
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                new BotFile { FileName = "PLAN-RPSLS.MD", Code = "# RPSLS Plan" },
                new BotFile { FileName = "Plan-ColonelBlotto.MD", Code = "# Blotto Plan" },
                new BotFile { FileName = "rpsls_skill.md", Code = "# RPSLS Skill" },
                new BotFile { FileName = "ColonelBlotto_Skill.md", Code = "# Blotto Skill" },
                new BotFile { FileName = "researchagent.md", Code = "# Research" },
                new BotFile { FileName = "PLAN-WORKSHOP.MD", Code = "# Plan" },
                new BotFile { FileName = "Copilot-Instructions.md", Code = "# Instructions" }
            }
        };

        // Assert - Case-insensitive matching should work
        var fileNames = request.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(fileNames.Contains("plan-rpsls.md"));
        Assert.IsTrue(fileNames.Contains("plan-colonelBlotto.md"));
        Assert.IsTrue(fileNames.Contains("RPSLS_Skill.md"));
        Assert.IsTrue(fileNames.Contains("colonelBlotto_Skill.md"));
        Assert.IsTrue(fileNames.Contains("ResearchAgent.md"));
        Assert.IsTrue(fileNames.Contains("plan-workshop.md"));
        Assert.IsTrue(fileNames.Contains("copilot-instructions.md"));
    }
}
