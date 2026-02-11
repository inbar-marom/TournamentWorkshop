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
}
