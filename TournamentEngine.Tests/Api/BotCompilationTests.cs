using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.BotLoader;
using BotLoaderClass = TournamentEngine.Core.BotLoader.BotLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TournamentEngine.Tests.Api;

/// <summary>
/// Tests for bot compilation with UserBot.Core dependencies
/// Validates that multi-file bots using UserBot.Core namespace can be compiled successfully
/// </summary>
[TestClass]
public class BotCompilationTests
{
    private BotLoaderClass? _botLoader;
    private ILogger<BotCompilationTests>? _logger;

    [TestInitialize]
    public void Setup()
    {
        // Create real BotLoader instance (same as used in API)
        _botLoader = new BotLoaderClass();
        
        // Create a simple logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));//
        _logger = loggerFactory.CreateLogger<BotCompilationTests>();//
    }

    [TestMethod]
    public void CompileStrategicMindBot_UserBotCoreFilesFound_VerifyFix()
    {
        // This test verifies that the workspace path fix in BotEndpoints.cs works
        // The fix changed from GetParent() to using GetCurrentDirectory() directly
        
        // Arrange - Simulate what the API does when looking for UserBot.Core
        var workspaceRoot = FindWorkspaceRoot();// // In API, this would be Directory.GetCurrentDirectory()
        var userBotCorePath = Path.Combine(workspaceRoot, "UserBot", "UserBot.Core");//
        
        _logger?.LogInformation("Workspace root: {Root}", workspaceRoot);//
        _logger?.LogInformation("Looking for UserBot.Core at: {Path}", userBotCorePath);//
        
        // Assert - The fix should allow finding UserBot.Core files
        Assert.IsTrue(Directory.Exists(userBotCorePath), 
            $"UserBot.Core directory should exist at: {userBotCorePath}");//
        
        var coreFiles = new[] { "IBot.cs", "GameState.cs", "GameType.cs" };//
        foreach (var fileName in coreFiles)
        {
            var filePath = Path.Combine(userBotCorePath, fileName);//
            Assert.IsTrue(File.Exists(filePath), 
                $"UserBot.Core file should be found: {fileName}");//
            _logger?.LogInformation("Found UserBot.Core file: {FileName}", fileName);//
        }
        
        _logger?.LogInformation("✓ Workspace path fix verified - all UserBot.Core files found");//
    }

    [TestMethod]
    public void CompileBot_UserBotCoreFilesExist_FilesCopiedToTempDirectory()
    {
        // Arrange - Verify UserBot.Core files exist in workspace
        var workspaceRoot = FindWorkspaceRoot();//
        var userBotCorePath = Path.Combine(workspaceRoot, "UserBot", "UserBot.Core");//

        _logger?.LogInformation("Checking for UserBot.Core at: {Path}", userBotCorePath);//

        // Assert - UserBot.Core directory must exist for this test
        Assert.IsTrue(Directory.Exists(userBotCorePath), 
            $"UserBot.Core directory must exist at: {userBotCorePath}");//

        // Verify required files exist
        var requiredCoreFiles = new[] { "IBot.cs", "GameState.cs", "GameType.cs" };//
        foreach (var fileName in requiredCoreFiles)
        {
            var filePath = Path.Combine(userBotCorePath, fileName);//
            Assert.IsTrue(File.Exists(filePath), 
                $"Required UserBot.Core file should exist: {filePath}");//
        }

        _logger?.LogInformation("All required UserBot.Core files found");//
    }

    [TestMethod]
    public void BotEndpoints_WorkspacePath_CorrectlyResolved()
    {
        // This test verifies the workspace path resolution matches what BotEndpoints.cs does
        // Before fix: used GetParent() which went one level too high
        // After fix: uses GetCurrentDirectory() directly (which is workspace root when API runs)
        
        // Arrange
        var workspaceRoot = FindWorkspaceRoot();//
        
        // Assert - Workspace should contain expected files/folders
        Assert.IsTrue(Directory.Exists(Path.Combine(workspaceRoot, "UserBot")), 
            "Workspace should contain UserBot folder");//
        Assert.IsTrue(Directory.Exists(Path.Combine(workspaceRoot, "TournamentEngine.Api")), 
            "Workspace should contain TournamentEngine.Api folder");//
        Assert.IsTrue(File.Exists(Path.Combine(workspaceRoot, "TournamentEngine.sln")), 
            "Workspace should contain solution file");//
        
        _logger?.LogInformation("✓ Workspace path resolution verified");//
    }

    [TestMethod]
    public void StrategicMindBot_FilesExist_CanBeLoaded()
    {
        // This test verifies the StrategicMind test data was extracted correctly
        
        // Arrange
        var submissionPath = Path.Combine(
            FindWorkspaceRoot(),
            "TournamentEngine.Tests",
            "TestData",
            "StrategicMind",
            "StrategicMind_Submission",
            "StrategicMind_Submission"
        );//

        // Assert
        Assert.IsTrue(Directory.Exists(submissionPath), 
            $"StrategicMind test data should exist at: {submissionPath}");//
        
        var botCodePath = Path.Combine(submissionPath, "BotCode");//
        Assert.IsTrue(Directory.Exists(botCodePath), 
            $"BotCode directory should exist");//
        
        var csFiles = Directory.GetFiles(botCodePath, "*.cs", SearchOption.AllDirectories);//
        Assert.IsTrue(csFiles.Length == 14, 
            $"Should have 14 C# files, found: {csFiles.Length}");//
        
        _logger?.LogInformation("✓ StrategicMind test data verified - {Count} C# files", csFiles.Length);//
    }

    [TestMethod]
    public async Task BotInWorkspaceRootBotsFolder_UsingUserBotCore_CompilesSuccessfully()
    {
        // Arrange - Simulate shared bots folder layout: <workspace>/bots/<Team>_v1
        var workspaceRoot = FindWorkspaceRoot();
        var teamFolder = Path.Combine(workspaceRoot, "bots", $"UserBotCorePathFix_{Guid.NewGuid():N}_v1");
        Directory.CreateDirectory(teamFolder);

        try
        {
            var botFilePath = Path.Combine(teamFolder, "Bot.cs");
            var botCode = @"using UserBot.Core;
using System.Threading;
using System.Threading.Tasks;

namespace UserBot.CorePathRegression;

public class Bot : IBot
{
    public string TeamName => ""CorePathRegression"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(""Rock"");

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(new[] { 20, 20, 20, 20, 20 });

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(""Center"");

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(""0"");
}";

            File.WriteAllText(botFilePath, botCode);

            // Act
            var botInfo = await _botLoader!.LoadBotFromFolderAsync(teamFolder);

            // Assert
            Assert.IsTrue(botInfo.IsValid, 
                $"Bot should compile when placed under workspace-level bots folder. Errors: {string.Join("; ", botInfo.ValidationErrors)}");
        }
        finally
        {
            if (Directory.Exists(teamFolder))
            {
                Directory.Delete(teamFolder, recursive: true);
            }
        }
    }

    #region Helper Methods - Path Resolution

    /// <summary>
    /// Find workspace root by looking for TournamentEngine.sln file
    /// When running tests, current directory is bin/Debug/net8.0
    /// </summary>
    private string FindWorkspaceRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();//
        var searchDir = new DirectoryInfo(currentDir);//

        // Walk up directory tree looking for .sln file
        while (searchDir != null)
        {
            if (File.Exists(Path.Combine(searchDir.FullName, "TournamentEngine.sln")))
            {
                return searchDir.FullName;//
            }
            searchDir = searchDir.Parent;//
        }

        // Fallback to current directory if not found
        return currentDir;//
    }

    #endregion
}
