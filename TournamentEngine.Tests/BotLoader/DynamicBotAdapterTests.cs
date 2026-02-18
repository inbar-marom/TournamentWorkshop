using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.Common;
using GameRunnerClass = TournamentEngine.Core.GameRunner.GameRunner;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TournamentEngine.Tests.BotLoader;

/// <summary>
/// Tests for dynamic bot adapter system that allows bots with different namespaces
/// to compete in the same tournament
/// </summary>
[TestClass]
public class DynamicBotAdapterTests
{
    [TestMethod]
    public void BotAdapterFactory_DetectsNativeBot_NoAdapterNeeded()
    {
        // Arrange - Create a bot using TournamentEngine.Core.Common namespace
        var nativeBot = new TestNativeBot();
        
        // Act
        var result = BotAdapterFactory.CreateAdapterIfNeeded(nativeBot);
        
        // Assert
        Assert.AreSame(nativeBot, result); // Should return same instance
        Assert.IsFalse(result is IBotAdapter); // Should not be wrapped
    }
    
    [TestMethod]
    public void BotAdapterFactory_DetectsTypeSystem_Correctly()
    {
        // Arrange
        var nativeBotType = typeof(TestNativeBot);
        
        // Act
        var needsAdapter = BotAdapterFactory.NeedsAdapter(nativeBotType);
        var typeSystem = BotAdapterFactory.GetBotTypeSystem(nativeBotType);
        
        // Assert
        Assert.IsFalse(needsAdapter);
        Assert.AreEqual("TournamentEngine.Core.Common", typeSystem);
    }
    
    [TestMethod]
    public async Task DynamicAdapter_WrapsCustomNamespaceBot_Successfully()
    {
        // Arrange - Compile a bot with custom namespace at runtime
        var customBotCode = @"
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            namespace CustomBot.Core
            {
                public enum GameType { RPSLS, Territory, Penalty, Security }
                
                public class GameState
                {
                    public int RoundNumber { get; set; }
                    public int TotalRounds { get; set; }
                    public int CurrentScore { get; set; }
                    public int OpponentScore { get; set; }
                    public List<string> MyMoveHistory { get; set; } = new();
                    public List<string> OpponentMoveHistory { get; set; } = new();
                    public string MatchId { get; set; } = string.Empty;
                    public string OpponentName { get; set; } = string.Empty;
                }
                
                public interface IBot
                {
                    string TeamName { get; }
                    GameType GameType { get; }
                    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
                    Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
                }
            }
            
            namespace CustomBot
            {
                using CustomBot.Core;
                
                public class CustomRandomBot : IBot
                {
                    private readonly Random _random = new();
                    public string TeamName => ""CustomNamespaceBot"";
                    public GameType GameType => CustomBot.Core.GameType.RPSLS;
                    
                    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        var moves = new[] { ""Rock"", ""Paper"", ""Scissors"", ""Lizard"", ""Spock"" };
                        return Task.FromResult(moves[_random.Next(moves.Length)]);
                    }
                    
                    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }";
        
        var customBot = CompileBotFromSource(customBotCode, "CustomBot.CustomRandomBot");
        
        // Act - Create adapter
        var adaptedBot = BotAdapterFactory.CreateAdapterIfNeeded(customBot);
        
        // Assert
        Assert.IsNotNull(adaptedBot);
        Assert.IsInstanceOfType(adaptedBot, typeof(IBotAdapter));
        Assert.AreEqual("CustomNamespaceBot", adaptedBot.TeamName);
        Assert.AreEqual(GameType.RPSLS, adaptedBot.GameType);
        
        // Test that MakeMove works through adapter
        var gameState = new GameState
        {
            CurrentRound = 1,
            MaxRounds = 10,
            RoundHistory = new()
        };
        
        var move = await adaptedBot.MakeMove(gameState, CancellationToken.None);
        Assert.IsNotNull(move);
        CollectionAssert.Contains(new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" }, move);
    }
    
    [TestMethod]
    public async Task MultipleBotsFromDifferentNamespaces_CanCompeteInSameTournament()
    {
        // Arrange - Create three bots from different namespaces
        
        // Bot 1: Native TournamentEngine.Core.Common bot
        var nativeBot = new TestNativeBot();
        
        // Bot 2: UserBot.Core namespace
        var userBotCode = @"
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            namespace UserBot.Core
            {
                public enum GameType { RPSLS, Territory, Penalty, Security }
                
                public class GameState
                {
                    public int RoundNumber { get; set; }
                    public int TotalRounds { get; set; }
                    public int CurrentScore { get; set; }
                    public int OpponentScore { get; set; }
                    public List<string> MyMoveHistory { get; set; } = new();
                    public List<string> OpponentMoveHistory { get; set; } = new();
                    public string MatchId { get; set; } = string.Empty;
                    public string OpponentName { get; set; } = string.Empty;
                }
                
                public interface IBot
                {
                    string TeamName { get; }
                    GameType GameType { get; }
                    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
                    Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
                }
                
                public class AlwaysRockBot : IBot
                {
                    public string TeamName => ""UserBotRock"";
                    public GameType GameType => UserBot.Core.GameType.RPSLS;
                    
                    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        return Task.FromResult(""Rock"");
                    }
                    
                    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }";
        
        var userBot = CompileBotFromSource(userBotCode, "UserBot.Core.AlwaysRockBot");
        
        // Bot 3: ThirdParty.BotSystem namespace
        var thirdPartyBotCode = @"
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            
            namespace ThirdParty.BotSystem
            {
                public enum GameType { RPSLS, Territory, Penalty, Security }
                
                public class GameState
                {
                    public int RoundNumber { get; set; }
                    public int TotalRounds { get; set; }
                    public int CurrentScore { get; set; }
                    public int OpponentScore { get; set; }
                    public List<string> MyMoveHistory { get; set; } = new();
                    public List<string> OpponentMoveHistory { get; set; } = new();
                    public string MatchId { get; set; } = string.Empty;
                    public string OpponentName { get; set; } = string.Empty;
                }
                
                public interface IBot
                {
                    string TeamName { get; }
                    GameType GameType { get; }
                    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
                    Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
                }
                
                public class AlwaysPaperBot : IBot
                {
                    public string TeamName => ""ThirdPartyPaper"";
                    public GameType GameType => ThirdParty.BotSystem.GameType.RPSLS;
                    
                    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        return Task.FromResult(""Paper"");
                    }
                    
                    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }";
        
        var thirdPartyBot = CompileBotFromSource(thirdPartyBotCode, "ThirdParty.BotSystem.AlwaysPaperBot");
        
        // Act - Create adapters for all bots
        var bot1 = BotAdapterFactory.CreateAdapterIfNeeded(nativeBot);
        var bot2 = BotAdapterFactory.CreateAdapterIfNeeded(userBot);
        var bot3 = BotAdapterFactory.CreateAdapterIfNeeded(thirdPartyBot);
        
        // Assert - All bots are valid IBot instances
        Assert.IsInstanceOfType(bot1, typeof(IBot));
        Assert.IsInstanceOfType(bot2, typeof(IBot));
        Assert.IsInstanceOfType(bot3, typeof(IBot));
        
        // Verify team names
        Assert.AreEqual("TestNativeBot", bot1.TeamName);
        Assert.AreEqual("UserBotRock", bot2.TeamName);
        Assert.AreEqual("ThirdPartyPaper", bot3.TeamName);
        
        // Verify all return GameType.RPSLS
        Assert.AreEqual(GameType.RPSLS, bot1.GameType);
        Assert.AreEqual(GameType.RPSLS, bot2.GameType);
        Assert.AreEqual(GameType.RPSLS, bot3.GameType);
        
        // Test matches between different namespace bots
        var config = new TournamentConfig { MemoryLimitMB = 512, MoveTimeout = TimeSpan.FromSeconds(1) };
        var gameRunner = new GameRunnerClass(config);
        
        // Match 1: Native vs UserBot
        var match1 = await gameRunner.ExecuteMatch(bot1, bot2, GameType.RPSLS, CancellationToken.None);
        Assert.IsNotNull(match1);
        Assert.IsTrue(match1.Bot1Score >= 0);
        Assert.IsTrue(match1.Bot2Score >= 0);
        
        // Match 2: UserBot vs ThirdParty
        var match2 = await gameRunner.ExecuteMatch(bot2, bot3, GameType.RPSLS, CancellationToken.None);
        Assert.IsNotNull(match2);
        // UserBot always plays Rock, ThirdParty always plays Paper -> Paper wins
        Assert.IsTrue(match2.Bot2Score > match2.Bot1Score); // Paper beats Rock
        
        // Match 3: Native vs ThirdParty
        var match3 = await gameRunner.ExecuteMatch(bot1, bot3, GameType.RPSLS, CancellationToken.None);
        Assert.IsNotNull(match3);
        Assert.IsTrue(match3.Bot1Score >= 0);
        Assert.IsTrue(match3.Bot2Score >= 0);
    }
    
    [TestMethod]
    public async Task Adapter_ConvertsGameStateProperties_Correctly()
    {
        // Arrange - Create bot that tracks game state history
        var smartBotCode = @"
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            
            namespace SmartBot.Core
            {
                public enum GameType { RPSLS, Territory, Penalty, Security }
                
                public class GameState
                {
                    public int RoundNumber { get; set; }
                    public int TotalRounds { get; set; }
                    public int CurrentScore { get; set; }
                    public int OpponentScore { get; set; }
                    public List<string> MyMoveHistory { get; set; } = new();
                    public List<string> OpponentMoveHistory { get; set; } = new();
                    public string MatchId { get; set; } = string.Empty;
                    public string OpponentName { get; set; } = string.Empty;
                }
                
                public interface IBot
                {
                    string TeamName { get; }
                    GameType GameType { get; }
                    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
                    Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
                    Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
                }
                
                public class HistoryAwareBot : IBot
                {
                    public string TeamName => ""HistoryBot"";
                    public GameType GameType => SmartBot.Core.GameType.RPSLS;
                    public int LastSeenRound { get; private set; }
                    public int LastHistoryCount { get; private set; }
                    
                    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        // Store values to verify they were passed correctly
                        LastSeenRound = gameState.RoundNumber;
                        LastHistoryCount = gameState.OpponentMoveHistory.Count;
                        
                        var moves = new[] { ""Rock"", ""Paper"", ""Scissors"", ""Lizard"", ""Spock"" };
                        return Task.FromResult(moves[gameState.RoundNumber % 5]);
                    }
                    
                    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                    
                    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }";
        
        var botInstance = CompileBotFromSource(smartBotCode, "SmartBot.Core.HistoryAwareBot");
        var adaptedBot = BotAdapterFactory.CreateAdapterIfNeeded(botInstance);
        
        // Act - Make move with specific game state
        var gameState = new GameState
        {
            CurrentRound = 5,
            MaxRounds = 10,
            State = new Dictionary<string, object>
            {
                ["CurrentScore"] = 3,
                ["OpponentScore"] = 2,
                ["MatchId"] = "test-match-123",
                ["OpponentName"] = "TestOpponent"
            },
            RoundHistory = new List<RoundHistory>
            {
                new() { Round = 1, MyMove = "Rock", OpponentMove = "Paper" },
                new() { Round = 2, MyMove = "Paper", OpponentMove = "Scissors" },
                new() { Round = 3, MyMove = "Scissors", OpponentMove = "Rock" },
                new() { Round = 4, MyMove = "Lizard", OpponentMove = "Spock" }
            }
        };
        
        var move = await adaptedBot.MakeMove(gameState, CancellationToken.None);
        
        // Assert
        Assert.IsNotNull(move);
        
        // Verify that game state was correctly converted and passed to the wrapped bot
        // We can check this by accessing the wrapped bot's public properties
        var wrappedBot = (adaptedBot as IBotAdapter)?.WrappedBot;
        Assert.IsNotNull(wrappedBot);
        
        var lastRoundProp = wrappedBot.GetType().GetProperty("LastSeenRound");
        var lastHistoryProp = wrappedBot.GetType().GetProperty("LastHistoryCount");
        
        Assert.IsNotNull(lastRoundProp);
        Assert.IsNotNull(lastHistoryProp);
        
        var lastRound = (int)lastRoundProp.GetValue(wrappedBot)!;
        var lastHistoryCount = (int)lastHistoryProp.GetValue(wrappedBot)!;
        
        Assert.AreEqual(5, lastRound);
        Assert.AreEqual(4, lastHistoryCount);
    }
    
    // Helper method to compile bot code at runtime
    private object CompileBotFromSource(string sourceCode, string botTypeName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location)
        };
        
        var compilation = CSharpCompilation.Create(
            "DynamicBot_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }
        
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
        
        var type = assembly.GetType(botTypeName);
        if (type == null)
            throw new InvalidOperationException($"Type {botTypeName} not found in compiled assembly");
        
        var instance = Activator.CreateInstance(type);
        if (instance == null)
            throw new InvalidOperationException($"Failed to create instance of {botTypeName}");
        
        return instance;
    }
    
    // Test helper bot using native namespace
    private class TestNativeBot : IBot
    {
        public string TeamName => "TestNativeBot";
        public GameType GameType => GameType.RPSLS;
        
        public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            return Task.FromResult("Scissors");
        }
        
        public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        
        public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        
        public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
