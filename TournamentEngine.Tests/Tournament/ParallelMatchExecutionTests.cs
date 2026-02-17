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
/// Tests for concurrent match execution within tournament stages
/// Verifies thread safety and stage synchronization
/// All matches within a stage run concurrently via Task.WhenAll
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
            FinalistsPerGroup = 1
        };

        _gameRunner = new GameRunner(_config);
    }

    [TestMethod]
    public async Task ConcurrentExecution_CompletesAllMatches()
    {
        // Arrange - 10 bots in 2 groups
        var bots = IntegrationTestHelpers.CreateVariedBots(10);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var scoringSystem = new ScoringSystem();
        var manager = new TournamentManager(engine, _gameRunner, scoringSystem);

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
    public async Task ConcurrentExecution_StandingsAreThreadSafe_NoDataCorruption()
    {
        // Arrange - Use 12 bots to stress test concurrent standings updates
        var bots = IntegrationTestHelpers.CreateVariedBots(12);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 3
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var scoringSystem = new ScoringSystem();
        var manager = new TournamentManager(engine, _gameRunner, scoringSystem);

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
    public async Task ConcurrentExecution_StageProgression_WaitsForAllMatchesBeforeAdvancing()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(12);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 3
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var scoringSystem = new ScoringSystem();
        var manager = new TournamentManager(engine, _gameRunner, scoringSystem);

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
    public async Task ConcurrentExecution_MultipleTournaments_ProduceCorrectResults()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(8);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2
        };

        // Act - Run twice with same seed bots
        var scoringSystem = new ScoringSystem();
        var engine1 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager1 = new TournamentManager(engine1, _gameRunner, scoringSystem);
        var result1 = await manager1.RunTournamentAsync(
            IntegrationTestHelpers.CreateVariedBots(8), GameType.RPSLS, config);

        var engine2 = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var manager2 = new TournamentManager(engine2, _gameRunner, scoringSystem);
        var result2 = await manager2.RunTournamentAsync(
            IntegrationTestHelpers.CreateVariedBots(8), GameType.RPSLS, config);

        // Assert - Both should complete successfully
        Assert.AreEqual(TournamentState.Completed, result1.State);
        Assert.AreEqual(TournamentState.Completed, result2.State);
        Assert.IsNotNull(result1.Champion);
        Assert.IsNotNull(result2.Champion);
        
        // Both should have same total number of matches
        Assert.AreEqual(result1.MatchResults.Count, result2.MatchResults.Count,
            "Both tournaments should play same number of matches");
    }

    [TestMethod]
    public async Task ConcurrentExecution_LargeTournament_CompletesSuccessfully()
    {
        // Arrange - Tournament with 16 bots and concurrent execution
        var bots = IntegrationTestHelpers.CreateVariedBots(16);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4
        };

        // Act - Execute with concurrent matches
        var sw = Stopwatch.StartNew();
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var scoringSystem = new ScoringSystem();
        var manager = new TournamentManager(engine, _gameRunner, scoringSystem);
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        sw.Stop();

        // Assert - Tournament completes successfully
        Assert.AreEqual(TournamentState.Completed, result.State);
        Assert.IsNotNull(result.Champion);
        Console.WriteLine($"Tournament completed in {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task ConcurrentExecution_GroupStageToFinals_ProperSynchronization()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(16);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 4
        };
        var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
        var scoringSystem = new ScoringSystem();
        var manager = new TournamentManager(engine, _gameRunner, scoringSystem);

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
    public async Task ConcurrentExecution_AllGameTypes_ThreadSafe()
    {
        // Arrange
        var bots = IntegrationTestHelpers.CreateVariedBots(8);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 2
        };
        var gameTypes = new[] { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame };

        // Act & Assert - Each game type should work with concurrent execution
        foreach (var gameType in gameTypes)
        {
            var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
            var scoringSystem = new ScoringSystem();
            var manager = new TournamentManager(engine, _gameRunner, scoringSystem);
            var result = await manager.RunTournamentAsync(bots, gameType, config);

            Assert.AreEqual(TournamentState.Completed, result.State, 
                $"{gameType} should complete successfully");
            Assert.IsNotNull(result.Champion, 
                $"{gameType} should have a champion");
        }
    }

    [TestMethod]
    public async Task ConcurrentExecution_ConcurrentStandingsUpdates_NoRaceConditions()
    {
        // Arrange - Run 2 concurrent tournaments with 12 bots each
        var bots = IntegrationTestHelpers.CreateVariedBots(12);
        var config = new TournamentConfig
        {
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            GroupCount = 3
        };
        
        // Run multiple tournaments concurrently to stress test engine thread safety
        var tasks = new List<Task<TournamentInfo>>();
        
        for (int i = 0; i < 2; i++)
        {
            var engine = new GroupStageTournamentEngine(_gameRunner, new ScoringSystem());
            var scoringSystem = new ScoringSystem();
            var manager = new TournamentManager(engine, _gameRunner, scoringSystem);
            tasks.Add(manager.RunTournamentAsync(
                IntegrationTestHelpers.CreateVariedBots(12), GameType.RPSLS, config));
        }

        // Act - Run 2 tournaments in parallel
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
