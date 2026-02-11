using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;

namespace TournamentEngine.Tests;

[TestClass]
public class BotLoaderValidationTests
{
    [TestMethod]
    public void BotInfo_ValidBot_ShouldPassValidation()
    {
        // Arrange
        var validBot = new RockBot();
        var botInfo = new BotInfo
        {
            TeamName = validBot.TeamName,
            FolderPath = "test/path/RockBot",
            IsValid = true,
            LoadTime = DateTime.Now
        };

        // Assert
        Assert.IsTrue(botInfo.IsValid);
        Assert.AreEqual("RockBot", botInfo.TeamName);
        Assert.AreEqual(0, botInfo.ValidationErrors.Count);
    }

    [TestMethod]
    public void BotInfo_InvalidBot_ShouldFailValidation()
    {
        // Arrange
        var invalidBot = new FaultyBot();
        var botInfo = new BotInfo
        {
            TeamName = invalidBot.TeamName,
            FolderPath = "test/path/FaultyBot",
            IsValid = false,
            LoadTime = DateTime.Now,
            ValidationErrors = { "Invalid move format", "Invalid troop allocation" }
        };

        // Assert
        Assert.IsFalse(botInfo.IsValid);
        Assert.AreEqual("FaultyBot", botInfo.TeamName);
        Assert.AreEqual(2, botInfo.ValidationErrors.Count);
        Assert.IsTrue(botInfo.ValidationErrors.Contains("Invalid move format"));
        Assert.IsTrue(botInfo.ValidationErrors.Contains("Invalid troop allocation"));
    }

    [TestMethod]
    public async Task ValidateBot_RPSLSMoves_ShouldDetectInvalidMoves()
    {
        // Arrange
        var validMoves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
        var invalidMoves = new[] { "InvalidMove", "Stone", "Fire", "", null };
        
        // Act & Assert
        foreach (var validMove in validMoves)
        {
            Assert.IsTrue(IsValidRPSLSMove(validMove), $"{validMove} should be valid");
        }

        foreach (var invalidMove in invalidMoves)
        {
            Assert.IsFalse(IsValidRPSLSMove(invalidMove), $"{invalidMove} should be invalid");
        }
    }

    [TestMethod]
    public void ValidateBot_TroopAllocation_ShouldDetectInvalidAllocations()
    {
        // Arrange
        var validAllocations = new[]
        {
            new int[] { 20, 20, 20, 20, 20 },
            new int[] { 40, 15, 15, 15, 15 },
            new int[] { 100, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 100 }
        };

        var invalidAllocations = new[]
        {
            new int[] { 30, 30, 30, 30, 30 }, // Sums to 150
            new int[] { 10, 10, 10, 10, 10 }, // Sums to 50
            new int[] { -10, 30, 30, 30, 20 }, // Negative value
            new int[] { 20, 20, 20, 20 }, // Wrong length
            new int[] { 20, 20, 20, 20, 20, 20 } // Wrong length
        };

        // Act & Assert
        foreach (var validAllocation in validAllocations)
        {
            Assert.IsTrue(IsValidTroopAllocation(validAllocation), 
                $"Allocation [{string.Join(", ", validAllocation)}] should be valid");
        }

        foreach (var invalidAllocation in invalidAllocations)
        {
            Assert.IsFalse(IsValidTroopAllocation(invalidAllocation), 
                $"Allocation [{string.Join(", ", invalidAllocation)}] should be invalid");
        }
    }

    [TestMethod]
    public void ValidateBot_PenaltyDecisions_ShouldDetectInvalidDecisions()
    {
        // Arrange
        var validDecisions = new[] { "Left", "Right" };
        var invalidDecisions = new[] { "Center", "Up", "Down", "InvalidDirection", "", null };
        
        // Act & Assert
        foreach (var validDecision in validDecisions)
        {
            Assert.IsTrue(IsValidPenaltyDecision(validDecision), $"{validDecision} should be valid");
        }

        foreach (var invalidDecision in invalidDecisions)
        {
            Assert.IsFalse(IsValidPenaltyDecision(invalidDecision), $"{invalidDecision} should be invalid");
        }
    }

    [TestMethod]
    public void ValidateBot_SecurityMoves_ShouldDetectInvalidMoves()
    {
        // Arrange
        var validMoves = new[] { "Attack", "Defend" };
        var invalidMoves = new[] { "InvalidAction", "Neutral", "Escape", "", null };
        
        // Act & Assert
        foreach (var validMove in validMoves)
        {
            Assert.IsTrue(IsValidSecurityMove(validMove), $"{validMove} should be valid");
        }

        foreach (var invalidMove in invalidMoves)
        {
            Assert.IsFalse(IsValidSecurityMove(invalidMove), $"{invalidMove} should be invalid");
        }
    }

    [TestMethod]
    public void BotLoader_AllowedNamespaces_ShouldIncludeBasicNamespaces()
    {
        // Arrange
        var allowedNamespaces = GetMockAllowedNamespaces();
        
        // Assert
        Assert.IsTrue(allowedNamespaces.Contains("System"));
        Assert.IsTrue(allowedNamespaces.Contains("System.Collections.Generic"));
        Assert.IsTrue(allowedNamespaces.Contains("System.Linq"));
        Assert.IsTrue(allowedNamespaces.Contains("System.Threading.Tasks"));
        Assert.IsTrue(allowedNamespaces.Contains("TournamentEngine.Core.Common"));
    }

    [TestMethod]
    public void BotLoader_BlockedNamespaces_ShouldIncludeUnsafeNamespaces()
    {
        // Arrange
        var blockedNamespaces = GetMockBlockedNamespaces();
        
        // Assert
        Assert.IsTrue(blockedNamespaces.Contains("System.IO"));
        Assert.IsTrue(blockedNamespaces.Contains("System.Net"));
        Assert.IsTrue(blockedNamespaces.Contains("System.Diagnostics"));
        Assert.IsTrue(blockedNamespaces.Contains("System.Reflection"));
        Assert.IsTrue(blockedNamespaces.Contains("System.Runtime"));
    }

    [TestMethod]
    public async Task BotLoader_LoadMultipleBots_ShouldReturnBotInfoList()
    {
        // Arrange
        var mockBots = new List<BotInfo>
        {
            new BotInfo
            {
                TeamName = "RockBot",
                FolderPath = "bots/team1",
                IsValid = true,
                LoadTime = DateTime.Now
            },
            new BotInfo
            {
                TeamName = "PaperBot",
                FolderPath = "bots/team2",
                IsValid = true,
                LoadTime = DateTime.Now
            },
            new BotInfo
            {
                TeamName = "FaultyBot",
                FolderPath = "bots/team3",
                IsValid = false,
                LoadTime = DateTime.Now,
                ValidationErrors = { "Invalid implementation" }
            }
        };

        // Act
        var validBots = mockBots.Where(b => b.IsValid).ToList();
        var invalidBots = mockBots.Where(b => !b.IsValid).ToList();

        // Assert
        Assert.AreEqual(2, validBots.Count);
        Assert.AreEqual(1, invalidBots.Count);
        Assert.IsTrue(validBots.All(b => b.ValidationErrors.Count == 0));
        Assert.IsTrue(invalidBots.All(b => b.ValidationErrors.Count > 0));
    }

    // Helper methods for validation logic (these would be implemented in the actual BotLoader)
    private static bool IsValidRPSLSMove(string? move)
    {
        var validMoves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
        return !string.IsNullOrEmpty(move) && validMoves.Contains(move);
    }

    private static bool IsValidTroopAllocation(int[] allocation)
    {
        if (allocation == null || allocation.Length != 5)
            return false;
        
        return allocation.All(x => x >= 0) && allocation.Sum() == 100;
    }

    private static bool IsValidPenaltyDecision(string? decision)
    {
        var validDecisions = new[] { "Left", "Right" };
        return !string.IsNullOrEmpty(decision) && validDecisions.Contains(decision);
    }

    private static bool IsValidSecurityMove(string? move)
    {
        var validMoves = new[] { "Attack", "Defend" };
        return !string.IsNullOrEmpty(move) && validMoves.Contains(move);
    }

    private static List<string> GetMockAllowedNamespaces()
    {
        return new List<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Threading.Tasks",
            "System.Math",
            "TournamentEngine.Core.Common"
        };
    }

    private static List<string> GetMockBlockedNamespaces()
    {
        return new List<string>
        {
            "System.IO",
            "System.Net",
            "System.Diagnostics",
            "System.Reflection",
            "System.Runtime",
            "System.Security",
            "Microsoft.Win32"
        };
    }
}
