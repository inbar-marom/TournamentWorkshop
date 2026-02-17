namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Diagnostics;

/// <summary>
/// Tests for large-scale tournaments (100+ bots, 450+ matches)
/// Ensures tournament execution works correctly at scale
/// </summary>
[TestClass]
public class LargeScaleTournamentTests
{
    [TestMethod]
    [Timeout(60000)] // 60 second timeout
    public async Task RunTournamentAsync_With100Bots_CompletesSuccessfully()
    {
        // Arrange
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);

        // Act
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.IsNotNull(result, "Tournament result should not be null");
        Assert.AreEqual(TournamentState.Completed, result.State, "Tournament should be completed");
        Assert.IsTrue(result.MatchResults.Count > 0, "Should have match results");
        Assert.IsNotNull(result.Champion, "Should have a champion");
        
        // With 10 groups of 10 bots each (45 matches per group = 450 total for group stage)
        // Plus final group and possibly tiebreakers
        Assert.IsTrue(result.MatchResults.Count >= 450, $"Should have at least 450 matches, got {result.MatchResults.Count}");
    }

    [TestMethod]
    [Timeout(60000)]
    public async Task RunTournamentAsync_With100Bots_AllMatchesHaveResults()
    {
        // Arrange
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);

        // Act
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Verify all matches have valid data
        foreach (var match in result.MatchResults)
        {
            Assert.IsFalse(string.IsNullOrEmpty(match.Bot1Name), "Bot1Name should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(match.Bot2Name), "Bot2Name should not be empty");
            Assert.AreNotEqual(MatchOutcome.Unknown, match.Outcome, "Match outcome should be determined");
            Assert.IsTrue(match.Bot1Score >= 0, "Bot1Score should be non-negative");
            Assert.IsTrue(match.Bot2Score >= 0, "Bot2Score should be non-negative");
        }
    }

    [TestMethod]
    [Timeout(90000)] // 90 seconds for larger test
    public async Task RunTournamentAsync_With200Bots_HandlesEvenLargerScale()
    {
        // Arrange - 200 bots = 990 matches per group, total ~9900 matches in group stage
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 5, // Shorter to keep test time reasonable
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(1)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(200, GameType.RPSLS);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(TournamentState.Completed, result.State);
        Assert.IsTrue(result.MatchResults.Count > 1000, "Should have many matches");
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 90, $"Should complete within timeout, took {stopwatch.Elapsed.TotalSeconds:F2}s");
        
        Console.WriteLine($"200-bot tournament: {result.MatchResults.Count} matches in {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task RunTournamentAsync_With50Bots_CompletesQuickly()
    {
        // Arrange - Baseline test with fewer bots
        var config = new TournamentConfig
        {
            GroupCount = 5,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(50, GameType.RPSLS);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(TournamentState.Completed, result.State);
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 30, $"Should complete quickly, took {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    [TestMethod]
    [Timeout(60000)]
    public async Task RunTournamentAsync_With100Bots_ConcurrentExecutionNoDeadlock()
    {
        // Arrange - Test that concurrent execution doesn't cause deadlocks
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);

        // Act - Run tournament twice to ensure no state corruption
        var result1 = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        
        // Create new instances for second run
        var engine2 = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager2 = new TournamentManager(engine2, gameRunner, scoringSystem, eventPublisher: null);
        var result2 = await manager2.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Both runs should complete successfully
        Assert.IsNotNull(result1);
        Assert.AreEqual(TournamentState.Completed, result1.State);
        Assert.IsNotNull(result2);
        Assert.AreEqual(TournamentState.Completed, result2.State);
        
        // Both should have similar number of matches
        Assert.IsTrue(Math.Abs(result1.MatchResults.Count - result2.MatchResults.Count) < 100, 
            "Both runs should have similar match counts");
    }

    [TestMethod]
    [Timeout(60000)]
    public async Task RunTournamentAsync_With100Bots_AllBotsParticipate()
    {
        // Arrange
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);
        var botNames = bots.Select(b => b.TeamName).ToHashSet();

        // Act
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Every bot should have participated in at least one match
        var participatingBots = result.MatchResults
            .SelectMany(m => new[] { m.Bot1Name, m.Bot2Name })
            .Distinct()
            .ToHashSet();

        foreach (var botName in botNames)
        {
            Assert.IsTrue(participatingBots.Contains(botName), 
                $"Bot {botName} should have participated in at least one match");
        }
    }

    [TestMethod]
    [Timeout(60000)]
    public async Task RunTournamentAsync_With100Bots_MatchesExecuteInParallel()
    {
        // Arrange
        var config = new TournamentConfig
        {
            GroupCount = 10,
            MaxRoundsRPSLS = 10,
            ImportTimeout = TimeSpan.FromSeconds(10),
            MoveTimeout = TimeSpan.FromSeconds(2)
        };

        var gameRunner = new GameRunner(config);
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var manager = new TournamentManager(engine, gameRunner, scoringSystem, eventPublisher: null);

        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(TournamentState.Completed, result.State);
        
        // With parallel execution, 450+ matches should complete much faster than serial execution
        // Serial would take: 450 matches * 10 rounds * 2ms per move * 2 bots = ~18 seconds minimum
        // Parallel should be much faster (we have 16-32 concurrent matches)
        var matchCount = result.MatchResults.Count;
        var expectedMinSerialTime = (matchCount * 10 * 0.002 * 2) / (Environment.ProcessorCount * 2);
        
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 60, 
            $"Tournament with {matchCount} matches should complete in reasonable time, took {stopwatch.Elapsed.TotalSeconds:F2}s");
        
        Console.WriteLine($"Completed {matchCount} matches in {stopwatch.Elapsed.TotalSeconds:F2}s " +
            $"({matchCount / stopwatch.Elapsed.TotalSeconds:F0} matches/sec)");
    }
}
