using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using TournamentEngine.Api.Utilities;
using BotLoaderClass = TournamentEngine.Core.BotLoader.BotLoader;

namespace TournamentEngine.Tests.Api;

[TestClass]
public class BotFilePathNormalizationTests
{
    private string _tempDirectory = null!;
    private ILogger<BotStorageService> _storageLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"bot-path-normalization-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Warning));
        _storageLogger = loggerFactory.CreateLogger<BotStorageService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FlattenedAdaptiveMastermindSubmission_ServerNormalization_StoresAndCompilesSuccessfully()
    {
        var flattenedFiles = CreateFlattenedAdaptiveMastermindFiles();

        var warnings = new List<string>();
        var normalizedFiles = BotFilePathNormalizer.NormalizeAndEnsureUnique(
            flattenedFiles,
            "AdaptiveMastermind",
            warnings);

        Assert.AreEqual(flattenedFiles.Count, normalizedFiles.Count, "All submitted files should be retained after normalization");
        Assert.IsTrue(normalizedFiles.Any(f => f.FileName.EndsWith("/Strategies/StrategySelector.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(normalizedFiles.Any(f => f.FileName.EndsWith("/MetaLearning/WeightedEnsemble.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(normalizedFiles.Any(f => f.FileName.EndsWith("/Core/SecureRandom.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(warnings.Count > 0, "Normalization should emit warnings for path changes when files are flattened");

        var storageService = new BotStorageService(_tempDirectory, _storageLogger);
        var submissionResult = await storageService.StoreBotAsync(new BotSubmissionRequest
        {
            TeamName = "AdaptiveMastermind",
            Files = flattenedFiles,
            Overwrite = true
        });

        Assert.IsTrue(submissionResult.Success, string.Join(Environment.NewLine, submissionResult.Errors));

        var submission = storageService.GetSubmission("AdaptiveMastermind");
        Assert.IsNotNull(submission);
        Assert.IsTrue(submission.FilePaths.Any(f => f.EndsWith("/Strategies/StrategySelector.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(submission.FilePaths.Any(f => f.EndsWith("/MetaLearning/WeightedEnsemble.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(submission.FilePaths.Any(f => f.EndsWith("/Core/SecureRandom.cs", StringComparison.OrdinalIgnoreCase)));

        var botLoader = new BotLoaderClass();
        var botInfo = await botLoader.LoadBotFromFolderAsync(submission.FolderPath);

        Assert.IsTrue(botInfo.IsValid, $"Compilation failed: {string.Join("; ", botInfo.ValidationErrors)}");
    }

    [TestMethod]
    public async Task NestedFilesPersistAcrossStorageResync_FilePathsRemainRelativeAndRecursive()
    {
        var storageService = new BotStorageService(_tempDirectory, _storageLogger);
        var flattenedFiles = CreateFlattenedAdaptiveMastermindFiles();

        var submitResult = await storageService.StoreBotAsync(new BotSubmissionRequest
        {
            TeamName = "AdaptiveMastermind",
            Files = flattenedFiles,
            Overwrite = true
        });

        Assert.IsTrue(submitResult.Success, string.Join(Environment.NewLine, submitResult.Errors));

        var reloadedStorageService = new BotStorageService(_tempDirectory, _storageLogger);
        var reloadedSubmission = reloadedStorageService.GetSubmission("AdaptiveMastermind");

        Assert.IsNotNull(reloadedSubmission);
        Assert.IsTrue(reloadedSubmission.FilePaths.All(path => !Path.IsPathRooted(path)), "Stored paths should remain relative");
        Assert.IsTrue(reloadedSubmission.FilePaths.Any(path => path.Contains("/Strategies/", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(reloadedSubmission.FilePaths.Any(path => path.Contains("/MetaLearning/", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(reloadedSubmission.FilePaths.Any(path => path.Contains("/Core/", StringComparison.OrdinalIgnoreCase)));
    }

    private static List<BotFile> CreateFlattenedAdaptiveMastermindFiles()
    {
        return new List<BotFile>
        {
            new()
            {
                FileName = "Bot.cs",
                Code = @"using TournamentEngine.Core.Common;
using UserBot.AdaptiveMastermind.Strategies;
using UserBot.AdaptiveMastermind.MetaLearning;
using UserBot.AdaptiveMastermind.Core;

namespace UserBot.AdaptiveMastermind;

public class Bot : IBot
{
    private readonly StrategySelector _selector = new();
    private readonly WeightedEnsemble _ensemble = new();
    private readonly SecureRandom _random = new();

    public string TeamName => ""AdaptiveMastermind"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        var strategy = _selector.Select();
        return Task.FromResult(strategy.NextMove(gameState, _ensemble, _random));
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(new[] { 20, 20, 20, 20, 20 });

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(""Center"");

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        => Task.FromResult(""0"");
}"
            },
            new()
            {
                FileName = "IStrategy.cs",
                Code = @"using TournamentEngine.Core.Common;
using UserBot.AdaptiveMastermind.MetaLearning;
using UserBot.AdaptiveMastermind.Core;

namespace UserBot.AdaptiveMastermind.Strategies;

public interface IStrategy
{
    string NextMove(GameState gameState, WeightedEnsemble ensemble, SecureRandom random);
}"
            },
            new()
            {
                FileName = "NashEquilibriumStrategy.cs",
                Code = @"using TournamentEngine.Core.Common;
using UserBot.AdaptiveMastermind.MetaLearning;
using UserBot.AdaptiveMastermind.Core;

namespace UserBot.AdaptiveMastermind.Strategies;

public class NashEquilibriumStrategy : IStrategy
{
    public string NextMove(GameState gameState, WeightedEnsemble ensemble, SecureRandom random)
        => ""Rock"";
}"
            },
            new()
            {
                FileName = "StrategySelector.cs",
                Code = @"namespace UserBot.AdaptiveMastermind.Strategies;

public class StrategySelector
{
    public IStrategy Select() => new NashEquilibriumStrategy();
}"
            },
            new()
            {
                FileName = "WeightedEnsemble.cs",
                Code = @"namespace UserBot.AdaptiveMastermind.MetaLearning;

public class WeightedEnsemble
{
    public double Weight => 1.0;
}"
            },
            new()
            {
                FileName = "SecureRandom.cs",
                Code = @"using System;

namespace UserBot.AdaptiveMastermind.Core;

public class SecureRandom
{
    private readonly Random _random = new(42);

    public int Next(int max) => _random.Next(max);
}"
            }
        };
    }
}
