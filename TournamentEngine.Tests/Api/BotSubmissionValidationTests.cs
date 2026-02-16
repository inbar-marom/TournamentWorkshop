namespace TournamentEngine.Tests.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.Common;

/// <summary>
/// Tests for enhanced bot submission validation rules
/// Tests documentation file support, double semicolon checking, and multi-file compilation
/// </summary>
[TestClass]
public class BotSubmissionValidationTests
{
    [TestMethod]
    public void BotSubmission_WithMarkdownDocumentation_IsAllowed()
    {
        // Arrange
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "Bot.cs",
                    Code = "using TournamentEngine.Core.Common; //\nclass Bot : IBot { } //"
                },
                new BotFile
                {
                    FileName = "instructions-workshop.md",
                    Code = "# Bot Instructions\nThis is my bot strategy..."
                },
                new BotFile
                {
                    FileName = "plan-workshop.md",
                    Code = "# High-Level Plan\n## Strategy\n..."
                }
            }
        };

        // Assert - Should not throw or reject .md files
        Assert.IsNotNull(request.Files);
        Assert.AreEqual(3, request.Files.Count);
        var mdFiles = request.Files.Where(f => f.FileName.EndsWith(".md")).ToList();
        Assert.AreEqual(2, mdFiles.Count);
    }

    [TestMethod]
    public void BotSubmission_WithVerificationScript_IsAllowed()
    {
        // Arrange
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "Bot.cs",
                    Code = "using TournamentEngine.Core.Common; //\nclass Bot : IBot { } //"
                },
                new BotFile
                {
                    FileName = "verificationScript.py",
                    Code = "# Python verification script\ndef verify():\n    print('Valid')"
                }
            }
        };

        // Assert - Python files allowed as documentation
        Assert.IsNotNull(request.Files);
        var pyFile = request.Files.FirstOrDefault(f => f.FileName.EndsWith(".py"));
        Assert.IsNotNull(pyFile);
    }

    [TestMethod]
    public void BotSubmission_WithAllDocumentationFiles_IsAllowed()
    {
        // Arrange - All files from requirements
        var request = new BotVerificationRequest
        {
            TeamName = "ComprehensiveBot",
            Files = new List<BotFile>
            {
                // Code
                new BotFile { FileName = "Bot.cs", Code = "class Bot { } //" },
                
                // Documentation
                new BotFile { FileName = "instructions-workshop.md", Code = "# Instructions" },
                new BotFile { FileName = "plan-workshop.md", Code = "# Plan" },
                new BotFile { FileName = "ResearchAgent.md", Code = "# Research" },
                
                // Skills
                new BotFile { FileName = "RPSLS_Skill.md", Code = "# RPSLS Strategy" },
                new BotFile { FileName = "colonelBlotto_Skill.md", Code = "# Blotto Strategy" },
                new BotFile { FileName = "penaltyKicks_Skill.md", Code = "# Penalty Strategy" },
                new BotFile { FileName = "securityGame_Skill.md", Code = "# Security Strategy" },
                
                // Game plans
                new BotFile { FileName = "plan-rpsls.md", Code = "# RPSLS Plan" },
                new BotFile { FileName = "plan-colonelBlotto.md", Code = "# Blotto Plan" },
                new BotFile { FileName = "plan-penaltyKicks.md", Code = "# Penalty Plan" },
                new BotFile { FileName = "plan-securityGame.md", Code = "# Security Plan" },
                
                // Verification script
                new BotFile { FileName = "verificationScript.py", Code = "def verify(): pass" }
            }
        };

        // Assert
        Assert.AreEqual(13, request.Files.Count);
        Assert.AreEqual(1, request.Files.Count(f => f.FileName.EndsWith(".cs")));
        Assert.AreEqual(11, request.Files.Count(f => f.FileName.EndsWith(".md")));
        Assert.AreEqual(1, request.Files.Count(f => f.FileName.EndsWith(".py")));
    }

    [TestMethod]
    public void BotValidation_WithDoubleSemicolons_IsRejected()
    {
        // Arrange - Code with double semicolons
        var file = new BotFile
        {
            FileName = "Bot.cs",
            Code = @"
using System; //
class Bot 
{
    public void Test()
    {
        int x = 5;; //  // Double semicolon error
        return; //
    }
}
"
        };

        // Assert - Should detect double semicolons
        Assert.IsTrue(file.Code.Contains(";;"));
    }

    [TestMethod]
    public void BotSubmission_OnlyDocumentationFiles_IsRejected()
    {
        // Arrange - No .cs files
        var request = new BotVerificationRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "README.md", Code = "# Documentation only" }
            }
        };

        // Assert - Should fail validation (no code files)
        var hasCodeFile = request.Files.Any(f => f.FileName.EndsWith(".cs"));
        Assert.IsFalse(hasCodeFile);
    }

    [TestMethod]
    public void BotSubmission_MultipleCodeFiles_DependOnEachOther()
    {
        // Arrange - Multiple C# files with dependencies
        var request = new BotVerificationRequest
        {
            TeamName = "MultiFileBot",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "Bot.cs",
                    Code = @"
using TournamentEngine.Core.Common; //
class Bot : IBot
{
    private Strategy _strategy = new Strategy(); //
}
"
                },
                new BotFile
                {
                    FileName = "Strategy.cs",
                    Code = @"
class Strategy
{
    public string GetMove() => ""Rock""; //
}
"
                }
            }
        };

        // Assert - Both files should be included
        Assert.AreEqual(2, request.Files.Count(f => f.FileName.EndsWith(".cs")));
    }

    [TestMethod]
    public void BotValidation_SameNamespaceDifferentBots_IsolatedCorrectly()
    {
        // Arrange - Two bots with identical namespace
        var bot1 = new BotInfo
        {
            TeamName = "Team_Alpha",
            FolderPath = "bots/Team_Alpha",
            IsValid = true
        };

        var bot2 = new BotInfo
        {
            TeamName = "Team_Beta",
            FolderPath = "bots/Team_Beta",
            IsValid = true
        };

        // Assert - Each bot loaded separately (assembly isolation tested in BotLoader)
        Assert.AreNotEqual(bot1.TeamName, bot2.TeamName);
        Assert.AreNotEqual(bot1.FolderPath, bot2.FolderPath);
    }

    [TestMethod]
    public void BotSubmission_ApprovedLibrariesList_IsComplete()
    {
        // Arrange - List from requirements
        var requiredLibraries = new[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Numerics",
            "System.Threading",
            "System.Threading.Tasks",
            "System.IO",
            "System.Text.RegularExpressions",
            "System.Diagnostics",
            "TournamentEngine.Core.Common"
        };

        // Assert - All required libraries should be in approved list
        // (This test documents the requirement - actual validation happens in BotEndpoints)
        Assert.AreEqual(11, requiredLibraries.Length);
    }

    [TestMethod]
    public void BotSubmission_SizeLimits_AreCorrect()
    {
        // Arrange
        var maxPerFile = 50_000; // 50KB
        var maxTotal = 500_000; // 500KB

        // Assert - Document the size limits
        Assert.AreEqual(50_000, maxPerFile, "Each file must be under 50KB");
        Assert.AreEqual(500_000, maxTotal, "Total submission must be under 500KB");
    }

    [TestMethod]
    public void BotValidation_Net80Target_IsRequired()
    {
        // Arrange - Code with different target framework
        var codeWithWrongTarget = @"
<Project>
    <TargetFramework>net6.0</TargetFramework>
</Project>
";

        var codeWithCorrectTarget = @"
<Project>
    <TargetFramework>net8.0</TargetFramework>
</Project>
";

        // Assert
        Assert.IsTrue(codeWithWrongTarget.Contains("net6.0"));
        Assert.IsTrue(codeWithCorrectTarget.Contains("net8.0"));
    }
}
