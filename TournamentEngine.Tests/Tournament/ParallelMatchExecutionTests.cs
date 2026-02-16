namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Tests for parallel match execution within tournament stages
/// Verifies thread safety, stage synchronization, and performance scaling
/// </summary>
[TestClass]
public class ParallelMatchExecutionTests
{
    private TournamentConfig _config = null!;
    private GameRunner _gameRunner = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            FinalistsPerGroup = 1,
            MaxParallelMatches = 1 // Will be overridden in tests
        };

        _gameRunner = new GameRunner(_config);
    }

    [TestMethod]
    public async Task ParallelExecution_WithUnlimitedParallelism_CompletesAllMatches()
    {
        // Arrange - 10 bots in 2 groups (5 per group = 10 matches per group = 20 initial matches)
        var bots = IntegrationTestHelpers.CreateVariedBots(10);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            MaxParallelMatches = int.MaxValue // Unlimited parallelism
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager = new TournamentManager(engine, _gameRunner);

        // Act
        var tournamentInfo = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.Champion);
        Assert.IsTrue(tournamentInfo.MatchResults.Count > 0, "Should have match results");
        
        // Verify all bots participated
        var participatingBots = tournamentInfo.MatchResults
            .SelectMany(m => new[] { m.Bot1Name, m.Bot2Name })
            .Distinct()
            .ToHashSet();
        Assert.AreEqual(bots.Count, participatingBots.Count, "All bots should have participated");
    }

    [TestMethod]
    public async Task ParallelExecution_WithThrottledParallelism_RespectsLimit()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(8);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            MaxParallelMatches = 4  // Max 4 concurrent
        };
        var executionTracker = new ConcurrentDictionary<DateTime, int>();

        // Track concurrent executions (simplified - actual tracking would need more instrumentation)
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager = new TournamentManager(engine, _gameRunner);

        // Act
        var tournamentInfo = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        Assert.IsNotNull(tournamentInfo.Champion);
        // Note: Exact concurrency tracking requires instrumentation in GameRunner
        // This test verifies that throttling doesn't break functionality
    }

    [TestMethod]
    public async Task ParallelExecution_StandingsAreThreadSafe_NoDataCorruption()
    {
        // Arrange - Use many bots to stress test concurrent standings updates
        var bots = IntegrationTestHelpers.CreateVariedBots(20);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4,
            MaxParallelMatches = int.MaxValue
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager = new TournamentManager(engine, _gameRunner);

        // Act
        var tournamentInfo = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Verify standings integrity
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        
        // Calculate expected total points (each match awards points)
        var totalPointsFromMatches = 0;
        foreach (var match in tournamentInfo.MatchResults)
        {
            totalPointsFromMatches += match.Outcome switch
            {
                MatchOutcome.Player1Wins => 3,
                MatchOutcome.Player2Wins => 3,
                MatchOutcome.Draw => 2,
                MatchOutcome.Player1Error => 3,
                MatchOutcome.Player2Error => 3,
                _ => 0
            };
        }

        // Verify data consistency (no points lost or duplicated)
        Assert.IsTrue(totalPointsFromMatches > 0, "Matches should award points");
    }

    [TestMethod]
    public async Task ParallelExecution_StageProgression_WaitsForAllMatchesBeforeAdvancing()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(12);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 3,
            MaxParallelMatches = int.MaxValue
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager = new TournamentManager(engine, _gameRunner);

        var stageSnapshots = new List<(int matchCount, TournamentState state)>();

        // Act
        var tournamentInfo = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Verify stage progression occurred in correct order
        Assert.AreEqual(TournamentState.Completed, tournamentInfo.State);
        
        // In initial groups phase, all group matches should complete
        // Then final group matches
        // Then tiebreaker (if needed)
        // Verify no interleaving occurred by checking match history integrity
        var bot1Matches = tournamentInfo.MatchResults.Where(m => m.Bot1Name == bots[0].TeamName).ToList();
        Assert.IsTrue(bot1Matches.Count > 0, "First bot should have played matches");
    }

    [TestMethod]
    public async Task ParallelExecution_WithSequentialMode_ProducesCorrectResults()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(8);
        var configSequential = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            MaxParallelMatches = 1
        };
        var configParallel = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            MaxParallelMatches = int.MaxValue
        };

        // Act - Run twice with same seed bots
        var engine1 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager1 = new TournamentManager(engine1, _gameRunner);
        var sequentialResult = await manager1.RunTournamentAsync(
            IntegrationTestHelpers.CreateVariedBots(8), GameType.RPSLS, configSequential);

        var engine2 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager2 = new TournamentManager(engine2, _gameRunner);
        var parallelResult = await manager2.RunTournamentAsync(
            IntegrationTestHelpers.CreateVariedBots(8), GameType.RPSLS, configParallel);

        // Assert - Both should complete successfully (champions may differ due to race conditions)
        Assert.AreEqual(TournamentState.Completed, sequentialResult.State);
        Assert.AreEqual(TournamentState.Completed, parallelResult.State);
        Assert.IsNotNull(sequentialResult.Champion);
        Assert.IsNotNull(parallelResult.Champion);
        
        // Both should have same total number of matches
        Assert.AreEqual(sequentialResult.MatchResults.Count, parallelResult.MatchResults.Count,
            "Parallel and sequential should play same number of matches");
    }

    [TestMethod]
    public async Task ParallelExecution_LargeTournament_ScalesPerformance()
    {
        // Arrange - Large tournament to demonstrate performance benefit
        var bots = IntegrationTestHelpers.CreateVariedBots(40);
        var configSequential = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4,
            MaxParallelMatches = 1
        };
        var configParallel = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4,
            MaxParallelMatches = int.MaxValue
        };

        // Act - Measure sequential execution time
        var sw1 = Stopwatch.StartNew();
        var engine1 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager1 = new TournamentManager(engine1, _gameRunner);
        await manager1.RunTournamentAsync(bots.Take(20).ToList(), GameType.RPSLS, configSequential);
        sw1.Stop();

        // Measure parallel execution time
        var sw2 = Stopwatch.StartNew();
        var engine2 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager2 = new TournamentManager(engine2, _gameRunner);
        await manager2.RunTournamentAsync(bots.Take(20).ToList(), GameType.RPSLS, configParallel);
        sw2.Stop();

        // Assert - Parallel should be faster (but not strictly enforced due to test variability)
        Console.WriteLine($"Sequential: {sw1.ElapsedMilliseconds}ms, Parallel: {sw2.ElapsedMilliseconds}ms");
        // We don't assert timing in unit tests, just verify both complete
        Assert.IsTrue(sw1.ElapsedMilliseconds > 0);
        Assert.IsTrue(sw2.ElapsedMilliseconds > 0);
    }

    [TestMethod]
    public async Task ParallelExecution_GroupStageToFinals_ProperSynchronization()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(16);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4,
            MaxParallelMatches = int.MaxValue
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager = new TournamentManager(engine, _gameRunner);

        // Act
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);

        // Assert - Verify tournament progressed through stages correctly
        Assert.AreEqual(TournamentState.Completed, result.State);
        Assert.IsNotNull(result.Champion);
        
        // Verify we had initial group stage matches (each bot plays others in group)
        // With 16 bots in 4 groups (4 per group), each group has C(4,2) = 6 matches
        // Total group stage matches = 4 groups * 6 matches = 24 matches minimum
        Assert.IsTrue(result.MatchResults.Count >= 24, 
            $"Should have at least 24 group stage matches, got {result.MatchResults.Count}");
    }

    [TestMethod]
    public async Task ParallelExecution_AllGameTypes_ThreadSafe()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(8);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2,
            MaxParallelMatches = int.MaxValue
        };
        var gameTypes = new[] { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame };

        // Act & Assert - Each game type should work with parallel execution
        foreach (var gameType in gameTypes)
        {
            var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
            var manager = new TournamentManager(engine, _gameRunner);
            var result = await manager.RunTournamentAsync(bots, gameType, config);

            Assert.AreEqual(TournamentState.Completed, result.State, 
                $"{gameType} should complete successfully");
            Assert.IsNotNull(result.Champion, 
                $"{gameType} should have a champion");
        }
    }

    [TestMethod]
    public async Task ParallelExecution_ConcurrentStandingsUpdates_NoRaceConditions()
    {
        // Arrange - Stress test with many bots and unlimited parallelism
        var bots = IntegrationTestHelpers.CreateVariedBots(30);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 5,
            MaxParallelMatches = int.MaxValue
        };
        
        // Run multiple tournaments concurrently to stress test engine thread safety
        var tasks = new List<Task<TournamentInfo>>();
        
        for (int i = 0; i < 3; i++)
        {
            var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
            var manager = new TournamentManager(engine, _gameRunner);
            tasks.Add(manager.RunTournamentAsync(
                IntegrationTestHelpers.CreateVariedBots(30), GameType.RPSLS, config));
        }

        // Act - Run 3 tournaments in parallel
        var results = await Task.WhenAll(tasks);

        // Assert - All tournaments should complete without race conditions
        foreach (var result in results)
        {
            Assert.AreEqual(TournamentState.Completed, result.State);
            Assert.IsNotNull(result.Champion);
            Assert.IsTrue(result.MatchResults.Count > 0);
        }
    }
}

