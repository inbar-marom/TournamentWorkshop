using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.BotLoader;

namespace TournamentEngine.Tests;

[TestClass]
public class BotLoaderTests
{
    private string _testBotsDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create a temp directory for test bots
        _testBotsDirectory = Path.Combine(Path.GetTempPath(), $"BotLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testBotsDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up test directory
        if (Directory.Exists(_testBotsDirectory))
        {
            Directory.Delete(_testBotsDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadBotFromFolder_SingleFile_CompilesSuccessfully()
    {
        // Arrange
        var teamName = "TestTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create a simple valid bot that implements IBot and handles all game types
        var botCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""TestTeam"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Rock"";
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new int[] { 20, 20, 20, 20, 20 };
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Left"";
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Scan"";
    }
}";

        var botFilePath = Path.Combine(botFolder, "TestBot.cs");
        await File.WriteAllTextAsync(botFilePath, botCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsTrue(result.IsValid, $"Bot should be valid. Errors: {string.Join(", ", result.ValidationErrors)}");
        Assert.IsNotNull(result.BotInstance, "BotInstance should not be null for valid bot");
        Assert.AreEqual(teamName, result.TeamName);
        Assert.AreEqual(0, result.ValidationErrors.Count, "Valid bot should have no validation errors");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_MultipleFiles_CompilesAllTogether()
    {
        // Arrange
        var teamName = "MultiFileTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // File 1: Constants.cs - shared constants
        var constantsCode = @"
namespace MultiFileBot
{
    public static class BotConstants
    {
        public const string DefaultMove = ""Rock"";
        public const int TotalTroops = 100;
        public const string PreferredDirection = ""Left"";
    }
}";

        // File 2: StrategyHelper.cs - helper class with strategy logic
        var strategyCode = @"
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

namespace MultiFileBot
{
    public class StrategyHelper
    {
        public static async Task<string> GetBestMove(GameState gameState)
        {
            await Task.CompletedTask;
            return BotConstants.DefaultMove;
        }

        public static async Task<int[]> CalculateTroopAllocation(GameState gameState)
        {
            await Task.CompletedTask;
            var perField = BotConstants.TotalTroops / 5;
            return new int[] { perField, perField, perField, perField, perField };
        }
    }
}";

        // File 3: MainBot.cs - implements IBot and uses helper classes
        var mainBotCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

namespace MultiFileBot
{
    public class MultiFileBot : IBot
    {
        public string TeamName => ""MultiFileTeam"";
        public GameType GameType => GameType.RPSLS;

        public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            // Use helper class from another file
            return await StrategyHelper.GetBestMove(gameState);
        }

        public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            // Use helper class that references constants
            return await StrategyHelper.CalculateTroopAllocation(gameState);
        }

        public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            // Use constant from another file
            return BotConstants.PreferredDirection;
        }

        public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return ""Scan"";
        }
    }
}";

        // Write all 3 files
        await File.WriteAllTextAsync(Path.Combine(botFolder, "Constants.cs"), constantsCode);
        await File.WriteAllTextAsync(Path.Combine(botFolder, "StrategyHelper.cs"), strategyCode);
        await File.WriteAllTextAsync(Path.Combine(botFolder, "MainBot.cs"), mainBotCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsTrue(result.IsValid, $"Multi-file bot should be valid. Errors: {string.Join(", ", result.ValidationErrors)}");
        Assert.IsNotNull(result.BotInstance, "BotInstance should not be null for multi-file bot");
        Assert.AreEqual(teamName, result.TeamName);
        Assert.AreEqual(0, result.ValidationErrors.Count, "Multi-file bot should have no validation errors");

        // Verify bot instance actually works
        var bot = result.BotInstance;
        Assert.IsNotNull(bot);
        Assert.AreEqual("MultiFileTeam", bot.TeamName);

        // Test that the bot can actually call methods that use cross-file references
        var gameState = new GameState
        {
            CurrentRound = 1,
            MaxRounds = 10
        };

        var move = await bot.MakeMove(gameState, CancellationToken.None);
        Assert.AreEqual("Rock", move, "Bot should return move from helper class");

        var troops = await bot.AllocateTroops(gameState, CancellationToken.None);
        Assert.AreEqual(5, troops.Length, "Should allocate troops across 5 fields");
        Assert.AreEqual(20, troops[0], "Each field should get 20 troops (100/5)");
    }

    #region Step 1.2: Validation Tests

    [TestMethod]
    public async Task LoadBotFromFolder_InvalidBot_ReturnsValidationErrors()
    {
        // Arrange
        var teamName = "InvalidTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create a class that does NOT implement IBot
        var invalidBotCode = @"
using System;

namespace InvalidBot
{
    public class NotABot
    {
        public string SomeMethod()
        {
            return ""I'm not a bot!"";
        }
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "NotABot.cs"), invalidBotCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot without IBot implementation should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null for invalid bot");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("IBot")), 
            "Error message should mention IBot interface");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_MultipleIBotImplementations_ReturnsError()
    {
        // Arrange
        var teamName = "MultiImplTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // File 1: First IBot implementation
        var bot1Code = @"
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class Bot1 : IBot
{
    public string TeamName => ""Bot1"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Rock"";
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new int[] { 20, 20, 20, 20, 20 };
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Left"";
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Scan"";
    }
}";

        // File 2: Second IBot implementation (this should be an error)
        var bot2Code = @"
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class Bot2 : IBot
{
    public string TeamName => ""Bot2"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Paper"";
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new int[] { 20, 20, 20, 20, 20 };
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Right"";
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return ""Attack"";
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "Bot1.cs"), bot1Code);
        await File.WriteAllTextAsync(Path.Combine(botFolder, "Bot2.cs"), bot2Code);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot with multiple IBot implementations should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null when multiple implementations exist");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("multiple") || e.Contains("Multiple")), 
            "Error message should mention multiple implementations");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_ExceedsTotalSize_ReturnsError()
    {
        // Arrange
        var teamName = "LargeTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create 5 files, each 50KB (250KB total > 200KB limit)
        var largeCommentBlock = new string('/', 50 * 1024); // 50KB of comment characters

        for (int i = 1; i <= 5; i++)
        {
            var fileCode = $@"
// {largeCommentBlock}
namespace LargeBot
{{
    public class HelperClass{i}
    {{
        public string GetData()
        {{
            return ""Data from file {i}"";
        }}
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(botFolder, $"Helper{i}.cs"), fileCode);
        }

        // Add a small main bot file
        var mainBotCode = @"
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

namespace LargeBot
{
    public class LargeBot : IBot
    {
        public string TeamName => ""LargeTeam"";
        public GameType GameType => GameType.RPSLS;

        public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return ""Rock"";
        }

        public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new int[] { 20, 20, 20, 20, 20 };
        }

        public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return ""Left"";
        }

        public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return ""Scan"";
        }
    }
}";
        await File.WriteAllTextAsync(Path.Combine(botFolder, "MainBot.cs"), mainBotCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot exceeding 200KB total size should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null when size limit exceeded");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("size") || e.Contains("200")), 
            "Error message should mention size limit");
    }

    #endregion
}
