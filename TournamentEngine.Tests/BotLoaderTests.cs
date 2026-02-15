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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
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
            var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
            return role == ""Attacker"" ? ""0"" : ""10,10,10"";
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
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
            var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
            return role == ""Attacker"" ? ""0"" : ""10,10,10"";
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

    #region Step 1.3: Namespace Restriction Tests

    [TestMethod]
    public async Task LoadBotFromFolder_UsesSystemIO_ReturnsError()
    {
        // Arrange
        var teamName = "FileAccessTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create a bot that tries to use System.IO
        var botCode = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class FileBot : IBot
{
    public string TeamName => ""FileAccessTeam"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Trying to access file system (blocked)
        var data = File.ReadAllText(""some-file.txt"");
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "FileBot.cs"), botCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot using System.IO should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null for bot with blocked namespace");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("System.IO") || e.Contains("namespace")), 
            "Error message should mention blocked namespace System.IO");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_UsesSystemNet_ReturnsError()
    {
        // Arrange
        var teamName = "NetworkTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create a bot that tries to use System.Net
        var botCode = @"
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class NetworkBot : IBot
{
    public string TeamName => ""NetworkTeam"";
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "NetworkBot.cs"), botCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot using System.Net should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null for bot with blocked namespace");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("System.Net") || e.Contains("namespace")), 
            "Error message should mention blocked namespace System.Net");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_BlockedNamespaceInSecondFile_ReturnsError()
    {
        // Arrange
        var teamName = "MultiFileBlockedTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // File 1: Clean main bot
        var mainBotCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

namespace MultiFileBlockedBot
{
    public class MainBot : IBot
    {
        public string TeamName => ""MultiFileBlockedTeam"";
        public GameType GameType => GameType.RPSLS;

        public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            // Uses helper from second file
            var helper = new SneakyHelper();
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
            var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
            return role == ""Attacker"" ? ""0"" : ""10,10,10"";
        }
    }
}";

        // File 2: Helper with blocked namespace (System.Reflection)
        var helperCode = @"
using System;
using System.Reflection;

namespace MultiFileBlockedBot
{
    public class SneakyHelper
    {
        public void DoSomethingSneaky()
        {
            // Trying to use reflection (blocked)
            var assembly = Assembly.GetExecutingAssembly();
        }
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "MainBot.cs"), mainBotCode);
        await File.WriteAllTextAsync(Path.Combine(botFolder, "SneakyHelper.cs"), helperCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsFalse(result.IsValid, "Bot with blocked namespace in any file should be invalid");
        Assert.IsNull(result.BotInstance, "BotInstance should be null when blocked namespace found");
        Assert.IsTrue(result.ValidationErrors.Count > 0, "Should have validation errors");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("System.Reflection") || e.Contains("namespace")), 
            "Error message should mention blocked namespace");
        Assert.IsTrue(result.ValidationErrors.Any(e => e.Contains("SneakyHelper.cs")), 
            "Error message should indicate which file has the blocked namespace");
    }

    [TestMethod]
    public async Task LoadBotFromFolder_AllowedNamespaces_CompilesSuccessfully()
    {
        // Arrange
        var teamName = "CleanTeam";
        var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
        Directory.CreateDirectory(botFolder);

        // Create a bot with only allowed namespaces
        var botCode = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class CleanBot : IBot
{
    public string TeamName => ""CleanTeam"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Using allowed namespaces
        var moves = new List<string> { ""Rock"", ""Paper"", ""Scissors"" };
        var selectedMove = moves.FirstOrDefault();
        await Task.CompletedTask;
        return selectedMove ?? ""Rock"";
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }
}";

        await File.WriteAllTextAsync(Path.Combine(botFolder, "CleanBot.cs"), botCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var result = await botLoader.LoadBotFromFolderAsync(botFolder);

        // Assert
        Assert.IsTrue(result.IsValid, "Bot with only allowed namespaces should be valid");
        Assert.IsNotNull(result.BotInstance, "BotInstance should be created for clean bot");
        Assert.AreEqual(0, result.ValidationErrors.Count, "Clean bot should have no validation errors");
    }

    #endregion

    #region Step 1.4: Batch Directory Loading Tests

    [TestMethod]
    public async Task LoadBotsFromDirectory_MultipleBots_LoadsAll()
    {
        // Arrange - Create 5 bot folders (3 valid, 2 invalid)
        var validBotCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class {0} : IBot
{{
    public string TeamName => ""{1}"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return ""Rock"";
    }}

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return new int[] {{ 20, 20, 20, 20, 20 }};
    }}

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return ""Left"";
    }}

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }}
}}";

        // Create 3 valid bots
        for (int i = 1; i <= 3; i++)
        {
            var teamName = $"ValidTeam{i}";
            var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
            Directory.CreateDirectory(botFolder);
            var code = string.Format(validBotCode, $"ValidBot{i}", teamName);
            await File.WriteAllTextAsync(Path.Combine(botFolder, $"Bot{i}.cs"), code);
        }

        // Create 2 invalid bots (no IBot implementation)
        var invalidBotCode = @"
namespace InvalidBot
{
    public class NotABot
    {
        public string DoNothing() => ""Nothing"";
    }
}";

        for (int i = 1; i <= 2; i++)
        {
            var teamName = $"InvalidTeam{i}";
            var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
            Directory.CreateDirectory(botFolder);
            await File.WriteAllTextAsync(Path.Combine(botFolder, $"NotBot{i}.cs"), invalidBotCode);
        }

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var results = await botLoader.LoadBotsFromDirectoryAsync(_testBotsDirectory);

        // Assert
        Assert.IsNotNull(results, "Results should not be null");
        Assert.AreEqual(5, results.Count, "Should load all 5 bot folders (valid and invalid)");

        var validResults = results.Where(r => r.IsValid).ToList();
        var invalidResults = results.Where(r => !r.IsValid).ToList();

        Assert.AreEqual(3, validResults.Count, "Should have 3 valid bots");
        Assert.AreEqual(2, invalidResults.Count, "Should have 2 invalid bots");

        // Verify valid bots have instances
        foreach (var validBot in validResults)
        {
            Assert.IsNotNull(validBot.BotInstance, $"Bot {validBot.TeamName} should have instance");
            Assert.AreEqual(0, validBot.ValidationErrors.Count, $"Valid bot {validBot.TeamName} should have no errors");
        }

        // Verify invalid bots have errors
        foreach (var invalidBot in invalidResults)
        {
            Assert.IsNull(invalidBot.BotInstance, $"Invalid bot {invalidBot.TeamName} should not have instance");
            Assert.IsTrue(invalidBot.ValidationErrors.Count > 0, $"Invalid bot {invalidBot.TeamName} should have errors");
        }
    }

    [TestMethod]
    public async Task LoadBotsFromDirectory_SomeInvalidBots_LoadsValidOnesAndReportsInvalid()
    {
        // Arrange - Mix of valid and invalid bots with different error types
        var validTeam1 = "GoodTeam1";
        var folder1 = Path.Combine(_testBotsDirectory, $"{validTeam1}_v1");
        Directory.CreateDirectory(folder1);
        var validCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class GoodBot : IBot
{
    public string TeamName => ""GoodTeam1"";
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }
}";
        await File.WriteAllTextAsync(Path.Combine(folder1, "GoodBot.cs"), validCode);

        // Invalid bot 1: No IBot implementation
        var invalidTeam1 = "NoInterfaceTeam";
        var folder2 = Path.Combine(_testBotsDirectory, $"{invalidTeam1}_v1");
        Directory.CreateDirectory(folder2);
        await File.WriteAllTextAsync(Path.Combine(folder2, "Bad.cs"), "public class Bad { }");

        // Invalid bot 2: Uses blocked namespace
        var invalidTeam2 = "BlockedNamespaceTeam";
        var folder3 = Path.Combine(_testBotsDirectory, $"{invalidTeam2}_v1");
        Directory.CreateDirectory(folder3);
        var blockedCode = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class BlockedBot : IBot
{
    public string TeamName => ""BlockedNamespaceTeam"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        File.ReadAllText(""data.txt"");
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
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }
}";
        await File.WriteAllTextAsync(Path.Combine(folder3, "BlockedBot.cs"), blockedCode);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var results = await botLoader.LoadBotsFromDirectoryAsync(_testBotsDirectory);

        // Assert
        Assert.AreEqual(3, results.Count, "Should attempt to load all 3 bots");

        var validBots = results.Where(r => r.IsValid).ToList();
        var invalidBots = results.Where(r => !r.IsValid).ToList();

        Assert.AreEqual(1, validBots.Count, "Should have 1 valid bot");
        Assert.AreEqual(2, invalidBots.Count, "Should have 2 invalid bots");

        // Verify the valid bot
        var goodBot = validBots.First();
        Assert.AreEqual("GoodTeam1", goodBot.TeamName);
        Assert.IsNotNull(goodBot.BotInstance);
        Assert.AreEqual(0, goodBot.ValidationErrors.Count);

        // Verify invalid bots have appropriate errors
        foreach (var invalidBot in invalidBots)
        {
            Assert.IsNull(invalidBot.BotInstance, $"Invalid bot {invalidBot.TeamName} should not have instance");
            Assert.IsTrue(invalidBot.ValidationErrors.Count > 0, $"Invalid bot {invalidBot.TeamName} should have errors");
        }
    }

    [TestMethod]
    public async Task LoadBotsFromDirectory_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange - Empty directory (cleanup already done in Setup)
        var emptyDir = Path.Combine(_testBotsDirectory, "EmptySubDir");
        Directory.CreateDirectory(emptyDir);

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var results = await botLoader.LoadBotsFromDirectoryAsync(emptyDir);

        // Assert
        Assert.IsNotNull(results, "Results should not be null");
        Assert.AreEqual(0, results.Count, "Empty directory should return empty list");
    }

    #endregion

    #region Step 1.5: Parallel Loading Tests

    [TestMethod]
    public async Task LoadBotsFromDirectory_ParallelLoading_LoadsAllBots()
    {
        // Arrange - Create 10 bot folders to test parallel loading
        var validBotCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class {0} : IBot
{{
    public string TeamName => ""{1}"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {{
        // Simulate some work
        await Task.Delay(10, cancellationToken);
        return ""Rock"";
    }}

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return new int[] {{ 20, 20, 20, 20, 20 }};
    }}

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return ""Left"";
    }}

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        var role = gameState.State.TryGetValue(""Role"", out var r) ? r?.ToString() : ""Attacker"";
        return role == ""Attacker"" ? ""0"" : ""10,10,10"";
    }}
}}";

        // Create 10 valid bots
        for (int i = 1; i <= 10; i++)
        {
            var teamName = $"ParallelTeam{i}";
            var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
            Directory.CreateDirectory(botFolder);
            var code = string.Format(validBotCode, $"ParallelBot{i}", teamName);
            await File.WriteAllTextAsync(Path.Combine(botFolder, $"Bot{i}.cs"), code);
        }

        var botLoader = new Core.BotLoader.BotLoader();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await botLoader.LoadBotsFromDirectoryAsync(_testBotsDirectory);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(results, "Results should not be null");
        Assert.AreEqual(10, results.Count, "Should load all 10 bots");

        // Verify all bots are valid
        var validBots = results.Where(r => r.IsValid).ToList();
        Assert.AreEqual(10, validBots.Count, "All 10 bots should be valid");

        // Verify all have bot instances
        foreach (var bot in validBots)
        {
            Assert.IsNotNull(bot.BotInstance, $"Bot {bot.TeamName} should have instance");
            Assert.AreEqual(0, bot.ValidationErrors.Count, $"Bot {bot.TeamName} should have no errors");
        }

        // Note: We can't strictly test for parallel execution speed improvement
        // without comparing to sequential, but we can verify it completed reasonably
        Console.WriteLine($"Loaded 10 bots in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task LoadBotsFromDirectory_ParallelLoading_IsThreadSafe()
    {
        // Arrange - Create 20 bots to test thread safety with concurrent loading
        var validBotCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class {0} : IBot
{{
    public string TeamName => ""{1}"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {{
        // Simulate varying workload
        await Task.Delay({2}, cancellationToken);
        return ""Paper"";
    }}

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return new int[] {{ 20, 20, 20, 20, 20 }};
    }}

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return ""Right"";
    }}

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        return ""Defend"";
    }}
}}";

        // Create 20 bots with varying compilation complexity
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 1; i <= 20; i++)
        {
            var teamName = $"ThreadSafeTeam{i}";
            var botFolder = Path.Combine(_testBotsDirectory, $"{teamName}_v1");
            Directory.CreateDirectory(botFolder);
            
            var delay = random.Next(5, 20); // Random delay 5-20ms
            var code = string.Format(validBotCode, $"ThreadSafeBot{i}", teamName, delay);
            await File.WriteAllTextAsync(Path.Combine(botFolder, $"Bot{i}.cs"), code);
        }

        var botLoader = new Core.BotLoader.BotLoader();

        // Act - Load all bots (should be done in parallel)
        var results = await botLoader.LoadBotsFromDirectoryAsync(_testBotsDirectory);

        // Assert - Verify all bots loaded correctly
        Assert.IsNotNull(results, "Results should not be null");
        Assert.AreEqual(20, results.Count, "Should load all 20 bots");

        // Verify all are valid (no race conditions causing corruption)
        var validBots = results.Where(r => r.IsValid).ToList();
        Assert.AreEqual(20, validBots.Count, "All 20 bots should be valid (no race conditions)");

        // Verify each bot has unique instance (no shared state issues)
        var botInstances = validBots.Select(b => b.BotInstance).ToList();
        Assert.AreEqual(20, botInstances.Distinct().Count(), "Each bot should have unique instance");

        // Verify team names are all unique (no name collision from concurrent access)
        var teamNames = validBots.Select(b => b.TeamName).ToList();
        Assert.AreEqual(20, teamNames.Distinct().Count(), "All team names should be unique");

        // Verify no validation errors (thread safety maintained)
        foreach (var bot in validBots)
        {
            Assert.AreEqual(0, bot.ValidationErrors.Count, 
                $"Bot {bot.TeamName} should have no validation errors (thread safety check)");
            Assert.IsNotNull(bot.BotInstance, 
                $"Bot {bot.TeamName} should have instance (no null from race condition)");
        }

        // Verify bots actually work (instances are properly initialized)
        var testGameState = new GameState
        {
            CurrentRound = 1,
            MaxRounds = 10
        };

        // Test a few random bots to ensure they're functional
        for (int i = 0; i < 5; i++)
        {
            var bot = validBots[i].BotInstance;
            var move = await bot.MakeMove(testGameState, CancellationToken.None);
            Assert.IsNotNull(move, $"Bot should return a valid move");
        }
    }

    #endregion
}
