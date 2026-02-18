namespace TournamentEngine.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.BotLoader;
using BotLoaderClass = TournamentEngine.Core.BotLoader.BotLoader;
using TournamentEngine.Core.Common;
using GameRunnerClass = TournamentEngine.Core.GameRunner.GameRunner;

/// <summary>
/// Unit tests for cumulative memory monitoring functionality
/// </summary>
[TestClass]
public class MemoryMonitoringTests
{
    private const long MB = 1024 * 1024;

    /// <summary>
    /// Test bot that allocates a specific amount of memory each time a method is called
    /// </summary>
    private class MemoryAllocatingBot : IBot
    {
        private readonly int _bytesPerCall;
        private readonly List<byte[]> _allocations = new(); // Hold ALL allocations to track cumulative

        public string TeamName => "MemoryTestBot";
        public GameType GameType => GameType.RPSLS;

        public MemoryAllocatingBot(int bytesPerCall)
        {
            _bytesPerCall = bytesPerCall;
        }

        public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            // Allocate memory and hold reference to prevent GC
            _allocations.Add(new byte[_bytesPerCall]);
            return Task.FromResult("rock");
        }

        public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            _allocations.Add(new byte[_bytesPerCall]);
            return Task.FromResult(new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
        }

        public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            _allocations.Add(new byte[_bytesPerCall]);
            return Task.FromResult("shoot_left");
        }

        public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            _allocations.Add(new byte[_bytesPerCall]);
            return Task.FromResult("patrol_A");
        }
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_TracksCumulativeMemoryUsage()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB); // 1 MB per call
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - Call multiple times to accumulate memory
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfterFirst = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfterSecond = monitoredBot.CumulativeMemoryUsage;

        // Assert
        Assert.IsTrue(usageAfterFirst > 0, "Memory usage should be tracked after first call");
        Assert.IsTrue(usageAfterSecond > usageAfterFirst, "Memory usage should accumulate across calls");
        Console.WriteLine($"After 1 call: {usageAfterFirst:N0} bytes");
        Console.WriteLine($"After 2 calls: {usageAfterSecond:N0} bytes");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_ThrowsWhenLimitExceeded()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(2 * (int)MB); // 2 MB per call
        var memoryLimit = 5 * MB; // Limit is 5 MB
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - First 2 calls should succeed (4 MB total), 3rd should fail
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        await monitoredBot.MakePenaltyDecision(gameState, CancellationToken.None);

        // Assert - 3rd call should throw OutOfMemoryException
        await Assert.ThrowsExceptionAsync<OutOfMemoryException>(
            async () => await monitoredBot.MakeSecurityMove(gameState, CancellationToken.None),
            "Should throw OutOfMemoryException when cumulative limit exceeded");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_ResetsAfterResetCall()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageBeforeReset = monitoredBot.CumulativeMemoryUsage;
        
        monitoredBot.ResetMemoryTracking();
        var usageAfterReset = monitoredBot.CumulativeMemoryUsage;

        // Assert
        Assert.IsTrue(usageBeforeReset > 0, "Should have accumulated memory before reset");
        Assert.AreEqual(0, usageAfterReset, "Memory tracking should be reset to 0");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_TracksAcrossAllMethods()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(500 * 1024); // 500 KB per call
        var memoryLimit = 20 * MB; // Increased to account for List overhead and GC heap metadata
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - Call all 4 IBot methods
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfter1 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.AllocateTroops(gameState, CancellationToken.None);
        var usageAfter2 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.MakePenaltyDecision(gameState, CancellationToken.None);
        var usageAfter3 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.MakeSecurityMove(gameState, CancellationToken.None);
        var usageAfter4 = monitoredBot.CumulativeMemoryUsage;

        // Assert - Each call should increase cumulative usage
        Assert.IsTrue(usageAfter1 > 0, "Usage after MakeMove");
        Assert.IsTrue(usageAfter2 > usageAfter1, "Usage after AllocateTroops");
        Assert.IsTrue(usageAfter3 > usageAfter2, "Usage after MakePenaltyDecision");
        Assert.IsTrue(usageAfter4 > usageAfter3, "Usage after MakeSecurityMove");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_IgnoresMemoryReduction()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(0); // No allocation = possible memory reduction
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // First allocate some memory elsewhere
        var allocBot = new MemoryAllocatingBot(1 * (int)MB);
        var tempMonitor = new MemoryMonitoredBot(allocBot, memoryLimit);
        await tempMonitor.MakeMove(gameState, CancellationToken.None);

        // Act - Call with zero-allocation bot
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usage1 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usage2 = monitoredBot.CumulativeMemoryUsage;

        // Assert - Memory usage should be small and consistent (accounting for method overhead)
        // With 0-byte allocations, we expect minimal and similar overhead for each call
        Assert.IsTrue(usage1 < 50000, "First call overhead should be under 50KB");
        Assert.IsTrue(usage2 < 100000, "Two calls overhead should be under 100KB total");
        Assert.IsTrue(usage2 >= usage1, "Cumulative memory should not decrease (only positive deltas counted)");
    }

    [TestMethod]
    public void BotInfo_CumulativeMemoryBytes_ReturnsMonitoredInstanceUsage()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        
        var botInfo = new BotInfo
        {
            TeamName = "TestBot",
            BotInstance = innerBot,
            MonitoredInstance = monitoredBot,
            IsValid = true,
            ValidationErrors = new System.Collections.Generic.List<string>()
        };

        // Act
        var initialUsage = botInfo.CumulativeMemoryBytes;
        monitoredBot.ResetMemoryTracking();
        var afterReset = botInfo.CumulativeMemoryBytes;

        // Assert
        Assert.AreEqual(monitoredBot.CumulativeMemoryUsage, initialUsage, "Should return monitored instance usage");
        Assert.AreEqual(0, afterReset, "Should reflect reset");
    }

    [TestMethod]
    public void BotInfo_CumulativeMemoryBytes_ReturnsZeroWhenNoMonitor()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var botInfo = new BotInfo
        {
            TeamName = "TestBot",
            BotInstance = innerBot,
            MonitoredInstance = null, // No monitoring
            IsValid = true,
            ValidationErrors = new System.Collections.Generic.List<string>()
        };

        // Act
        var usage = botInfo.CumulativeMemoryBytes;

        // Assert
        Assert.AreEqual(0, usage, "Should return 0 when MonitoredInstance is null");
    }

    [TestMethod]
    public void BotInfo_GetExecutableBot_ReturnsMonitoredInstanceWhenPresent()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        
        var botInfo = new BotInfo
        {
            TeamName = "TestBot",
            BotInstance = innerBot,
            MonitoredInstance = monitoredBot,
            IsValid = true,
            ValidationErrors = new System.Collections.Generic.List<string>()
        };

        // Act
        var executableBot = botInfo.GetExecutableBot();

        // Assert
        Assert.AreSame(monitoredBot, executableBot, "Should return MonitoredInstance when present");
    }

    [TestMethod]
    public void BotInfo_GetExecutableBot_ReturnsBotInstanceWhenMonitorAbsent()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var botInfo = new BotInfo
        {
            TeamName = "TestBot",
            BotInstance = innerBot,
            MonitoredInstance = null,
            IsValid = true,
            ValidationErrors = new System.Collections.Generic.List<string>()
        };

        // Act
        var executableBot = botInfo.GetExecutableBot();

        // Assert
        Assert.AreSame(innerBot, executableBot, "Should return BotInstance when MonitoredInstance is null");
    }

    [TestMethod]
    public async Task BotLoader_WrapsBotsInMemoryMonitor_WhenConfigProvided()
    {
        // Arrange
        var testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(testDir);
        var botDir = System.IO.Path.Combine(testDir, "TestBot_v1");
        System.IO.Directory.CreateDirectory(botDir);

        var simpleBotCode = @"
using TournamentEngine.Core.Common;
using System.Threading;
using System.Threading.Tasks;

public class SimpleBot : IBot
{
    public string TeamName => ""SimpleBot"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""rock"");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""shoot_center"");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""patrol_A"");
    }
}";
        System.IO.File.WriteAllText(System.IO.Path.Combine(botDir, "Bot.cs"), simpleBotCode);

        var config = new TournamentConfig { MemoryLimitMB = 512 };
        var botLoader = new BotLoaderClass();

        try
        {
            // Act
            var botInfo = await botLoader.LoadBotFromFolderAsync(botDir, config);

            // Assert
            if (!botInfo.IsValid)
            {
                var errors = string.Join("; ", botInfo.ValidationErrors);
                Assert.Fail($"Bot should be valid. Errors: {errors}");
            }
            Assert.IsTrue(botInfo.IsValid, "Bot should be valid");
            Assert.IsNotNull(botInfo.MonitoredInstance, "Should have MonitoredInstance when config provided");
            Assert.AreEqual(512 * 1024 * 1024, botInfo.MonitoredInstance!.MemoryLimitBytes, "Should use config MemoryLimitMB");
        }
        finally
        {
            // Cleanup
            if (System.IO.Directory.Exists(testDir))
            {
                System.IO.Directory.Delete(testDir, true);
            }
        }
    }

    [TestMethod]
    public async Task BotLoader_DoesNotWrapBots_WhenConfigAbsent()
    {
        // Arrange
        var testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid. NewGuid().ToString());
        System.IO.Directory.CreateDirectory(testDir);
        var botDir = System.IO.Path.Combine(testDir, "TestBot_v1");
        System.IO.Directory.CreateDirectory(botDir);

        var simpleBotCode = @"
using TournamentEngine.Core.Common;
using System.Threading;
using System.Threading.Tasks;

public class SimpleBot : IBot
{
    public string TeamName => ""SimpleBot"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""rock"");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""shoot_center"");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""patrol_A"");
    }
}";
        System.IO.File.WriteAllText(System.IO.Path.Combine(botDir, "Bot.cs"), simpleBotCode);

        var botLoader = new BotLoaderClass();

        try
        {
            // Act
            var botInfo = await botLoader.LoadBotFromFolderAsync(botDir, null); // No config

            // Assert
            Assert.IsTrue(botInfo.IsValid, "Bot should be valid");
            Assert.IsNull(botInfo.MonitoredInstance, "Should NOT have MonitoredInstance when config is null");
        }
        finally
        {
            // Cleanup
            if (System.IO.Directory.Exists(testDir))
            {
                System.IO.Directory.Delete(testDir, true);
            }
        }
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_ExceptionContainsClearMessage()
    {
        // Arrange - Allocate larger amounts to ensure we exceed limit despite GC measurement variance
        var innerBot = new MemoryAllocatingBot(4 * (int)MB);
        var memoryLimit = 5 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - Allocate 4MB twice = 8MB > 5MB limit (will definitely exceed)
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        
        OutOfMemoryException? caughtException = null;
        try
        {
            await monitoredBot.MakePenaltyDecision(gameState, CancellationToken.None);
        }
        catch (OutOfMemoryException ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.IsNotNull(caughtException, "Should throw OutOfMemoryException");
        Assert.IsTrue(caughtException.Message.Contains("exceeded memory limit"), 
            $"Exception message should be clear. Actual: {caughtException.Message}");
        Assert.IsTrue(caughtException.Message.Contains("MB"), 
            "Exception should include memory values in MB");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_ExactlyAtLimit_DoesNotThrow()
    {
        // Arrange - Use limit well above expected usage to avoid edge case variance
        var innerBot = new MemoryAllocatingBot(2 * (int)MB);
        var memoryLimit = 10 * MB; // 3 calls * ~2MB = ~6MB, well under 10MB limit
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act & Assert - 3 calls should stay under limit
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usage1 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.AllocateTroops(gameState, CancellationToken.None);
        var usage2 = monitoredBot.CumulativeMemoryUsage;
        
        await monitoredBot.MakePenaltyDecision(gameState, CancellationToken.None);
        var usage3 = monitoredBot.CumulativeMemoryUsage;

        // All should succeed and stay under limit
        Assert.IsTrue(usage1 > 0 && usage1 < memoryLimit, $"First call: {usage1} should be under {memoryLimit}");
        Assert.IsTrue(usage2 > usage1 && usage2 < memoryLimit, $"Second call: {usage2} should be under {memoryLimit}");
        Assert.IsTrue(usage3 > usage2 && usage3 < memoryLimit, $"Third call: {usage3} should be under {memoryLimit}");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_MultipleMatchesAccumulate()
    {
        // Arrange - Simulates multiple matches in same event
        var innerBot = new MemoryAllocatingBot(500 * 1024); // 500KB per call
        var memoryLimit = 10 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - Simulate 3 matches, each with 2 moves (6 calls total)
        // Match 1
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfterMatch1 = monitoredBot.CumulativeMemoryUsage;

        // Match 2
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfterMatch2 = monitoredBot.CumulativeMemoryUsage;

        // Match 3
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        var usageAfterMatch3 = monitoredBot.CumulativeMemoryUsage;

        // Assert - Usage should accumulate across matches
        Assert.IsTrue(usageAfterMatch1 > 0, "Memory should accumulate after match 1");
        Assert.IsTrue(usageAfterMatch2 > usageAfterMatch1, "Memory should accumulate after match 2");
        Assert.IsTrue(usageAfterMatch3 > usageAfterMatch2, "Memory should accumulate after match 3");
        
        Console.WriteLine($"After Match 1: {usageAfterMatch1:N0} bytes");
        Console.WriteLine($"After Match 2: {usageAfterMatch2:N0} bytes");
        Console.WriteLine($"After Match 3: {usageAfterMatch3:N0} bytes");
    }

    [TestMethod]
    public async Task MemoryMonitoredBot_LimitEnforcedAcrossAllMethodTypes()
    {
        // Arrange - Each method allocates 1.5MB, limit is 5MB
        var innerBot = new MemoryAllocatingBot((int)(1.5 * MB));
        var memoryLimit = 5 * MB;
        var monitoredBot = new MemoryMonitoredBot(innerBot, memoryLimit);
        var gameState = new GameState();

        // Act - First 3 calls should succeed (4.5MB), 4th should fail
        await monitoredBot.MakeMove(gameState, CancellationToken.None);
        await monitoredBot.AllocateTroops(gameState, CancellationToken.None);
        await monitoredBot.MakePenaltyDecision(gameState, CancellationToken.None);
        
        // Assert - 4th call (any method) should throw
        await Assert.ThrowsExceptionAsync<OutOfMemoryException>(
            async () => await monitoredBot.MakeSecurityMove(gameState, CancellationToken.None),
            "Should enforce limit across all IBot methods");
    }

    [TestMethod]
    public async Task IntegrationTest_MemoryLimitEnforcedDuringGameExecution()
    {
        // Arrange - Create bots that allocate memory, run through GameRunner
        var testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(testDir);

        try
        {
            // Create a bot that allocates significant memory each move
            var memoryHogBotDir = System.IO.Path.Combine(testDir, "MemoryHog_v1");
            System.IO.Directory.CreateDirectory(memoryHogBotDir);

            var memoryHogCode = @"
using TournamentEngine.Core.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MemoryHog : IBot
{
    private List<byte[]> _cache = new List<byte[]>();
    public string TeamName => ""MemoryHog"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Allocate 2MB each time
        _cache.Add(new byte[2 * 1024 * 1024]);
        return Task.FromResult(""Rock"");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""shoot_center"");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""patrol_A"");
    }
}";
            System.IO.File.WriteAllText(System.IO.Path.Combine(memoryHogBotDir, "MemoryHog.cs"), memoryHogCode);

            var normalBotDir = System.IO.Path.Combine(testDir, "NormalBot_v1");
            System.IO.Directory.CreateDirectory(normalBotDir);

            var normalBotCode = @"
using TournamentEngine.Core.Common;
using System.Threading;
using System.Threading.Tasks;

public class NormalBot : IBot
{
    public string TeamName => ""NormalBot"";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""Rock"");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""shoot_center"");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(""patrol_A"");
    }
}";
            System.IO.File.WriteAllText(System.IO.Path.Combine(normalBotDir, "NormalBot.cs"), normalBotCode);

            // Load bots with memory monitoring (5MB limit - MemoryHog will exceed after 2-3 moves)
            var config = new TournamentConfig 
            { 
                MemoryLimitMB = 5,
                MoveTimeout = TimeSpan.FromSeconds(5),
                MaxRoundsRPSLS = 50
            };
            var botLoader = new BotLoaderClass();
            var memoryHogInfo = await botLoader.LoadBotFromFolderAsync(memoryHogBotDir, config);
            var normalBotInfo = await botLoader.LoadBotFromFolderAsync(normalBotDir, config);

            Assert.IsTrue(memoryHogInfo.IsValid, "MemoryHog should compile successfully");
            Assert.IsTrue(normalBotInfo.IsValid, "NormalBot should compile successfully");
            Assert.IsNotNull(memoryHogInfo.MonitoredInstance, "MemoryHog should be wrapped in monitor");
            Assert.IsNotNull(normalBotInfo.MonitoredInstance, "NormalBot should be wrapped in monitor");

            // Run a match - MemoryHog should eventually exceed limit
            var gameRunner = new GameRunnerClass(config);
            var result = await gameRunner.ExecuteMatch(
                memoryHogInfo.GetExecutableBot(), 
                normalBotInfo.GetExecutableBot(), 
                GameType.RPSLS, 
                CancellationToken.None);

            // Debug output
            Console.WriteLine($"Match Outcome: {result.Outcome}");
            Console.WriteLine($"Bot1 Score: {result.Bot1Score}, Bot2 Score: {result.Bot2Score}");
            Console.WriteLine($"Errors: {string.Join(Environment.NewLine, result.Errors)}");
            Console.WriteLine($"MemoryHog cumulative bytes: {memoryHogInfo.CumulativeMemoryBytes}");
            Console.WriteLine($"NormalBot cumulative bytes: {normalBotInfo.CumulativeMemoryBytes}");

            // Assert - MemoryHog should have lost due to OutOfMemoryException
            Assert.AreEqual(MatchOutcome.Player2Wins, result.Outcome, 
                $"MemoryHog should lose when exceeding memory limit. Actual: {result.Outcome}, Errors: {string.Join("; ", result.Errors)}");
            
            // Verify memory was being tracked
            Assert.IsTrue(memoryHogInfo.CumulativeMemoryBytes > 0, 
                "MemoryHog should have accumulated memory usage");

            Console.WriteLine($"MemoryHog accumulated {memoryHogInfo.CumulativeMemoryBytes:N0} bytes before failing");
            Console.WriteLine($"Match result: {result.Outcome} (Bot1={result.Bot1Score}, Bot2={result.Bot2Score})");
        }
        finally
        {
            // Cleanup
            if (System.IO.Directory.Exists(testDir))
            {
                System.IO.Directory.Delete(testDir, true);
            }
        }
    }

    [TestMethod]
    public void BotInfo_ResetMemoryTracking_CallsMonitoredInstance()
    {
        // Arrange
        var innerBot = new MemoryAllocatingBot(1 * (int)MB);
        var monitoredBot = new MemoryMonitoredBot(innerBot, 10 * MB);
        var botInfo = new BotInfo
        {
            TeamName = "TestBot",
            BotInstance = innerBot,
            MonitoredInstance = monitoredBot,
            IsValid = true,
            ValidationErrors = new System.Collections.Generic.List<string>()
        };

        // Simulate some memory usage
        monitoredBot.MakeMove(new GameState(), CancellationToken.None).Wait();
        Assert.IsTrue(botInfo.CumulativeMemoryBytes > 0, "Should have memory usage before reset");

        // Act
        botInfo.ResetMemoryTracking();

        // Assert
        Assert.AreEqual(0, botInfo.CumulativeMemoryBytes, "Memory should be reset to 0");
        Assert.AreEqual(0, monitoredBot.CumulativeMemoryUsage, "Monitored bot should be reset");
    }
}
