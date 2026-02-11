namespace TournamentEngine.Tests.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.BotLoader;

/// <summary>
/// Shared utility methods for integration tests
/// </summary>
public static class IntegrationTestHelpers
{
    /// <summary>
    /// Creates demo bots in a temporary directory, compiles them using BotLoader,
    /// and returns valid bot instances. Cleans up temporary directory after.
    /// </summary>
    public static async Task<List<BotInfo>> CreateDemoBots(int count)
    {
        var testBotsDirectory = Path.Combine(Path.GetTempPath(), $"IntegrationTestBots_{Guid.NewGuid()}");
        Directory.CreateDirectory(testBotsDirectory);

        try
        {
            for (int i = 1; i <= count; i++)
            {
                var teamName = $"Team{i}";
                var botFolder = Path.Combine(testBotsDirectory, $"{teamName}_v1");
                Directory.CreateDirectory(botFolder);

                // Create bot code dynamically
                var botCode = GetBotCode(i, teamName);
                await File.WriteAllTextAsync(Path.Combine(botFolder, "Bot.cs"), botCode);
            }

            // Load bots using BotLoader
            var botLoader = new BotLoader();
            var bots = await botLoader.LoadBotsFromDirectoryAsync(testBotsDirectory);
            
            // Filter to valid bots only
            var validBots = bots.Where(b => b.IsValid).ToList();
            
            return validBots;
        }
        finally
        {
            // Cleanup test directory
            if (Directory.Exists(testBotsDirectory))
            {
                try
                {
                    Directory.Delete(testBotsDirectory, recursive: true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Generates C# bot implementation code with deterministic behavior.
    /// Returns a string containing a valid IBot implementation.
    /// </summary>
    public static string GetBotCode(int botNumber, string teamName)
    {
        return $@"using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class Bot{botNumber} : IBot
{{
    public string TeamName => ""{teamName}"";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {{
        await Task.CompletedTask;
        var moves = new[] {{ ""Rock"", ""Paper"", ""Scissors"" }};
        return moves[gameState.CurrentRound % 3];
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
        return ""Scan"";
    }}
}}";
    }

    /// <summary>
    /// Creates a standard tournament configuration for integration testing.
    /// </summary>
    public static TournamentConfig CreateConfig()
    {
        return new TournamentConfig
        {
            Games = new List<GameType> { GameType.RPSLS },
            ImportTimeout = TimeSpan.FromSeconds(5),
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            LogLevel = "INFO",
            LogFilePath = "tournament.log",
            BotsDirectory = "demo_bots",
            ResultsFilePath = "results.json"
        };
    }

    /// <summary>
    /// Creates varied bots with different strategies for realistic tournament simulations.
    /// Each bot has a unique seed that determines its move patterns, troop allocations, and penalty decisions.
    /// </summary>
    public static List<BotInfo> CreateVariedBots(int count)
    {
        var bots = new List<BotInfo>(count);
        for (int i = 1; i <= count; i++)
        {
            var teamName = $"Team{i}";
            bots.Add(new BotInfo
            {
                TeamName = teamName,
                IsValid = true,
                BotInstance = new DummyBots.VariedBot(teamName, i)
            });
        }

        return bots;
    }
}
