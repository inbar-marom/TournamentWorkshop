namespace TournamentEngine.Tests.Integration;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;
using System.Diagnostics;

/// <summary>
/// Diagnostic test to reproduce and diagnose the 100-bot tournament hanging issue
/// </summary>
[TestClass]
public class LargeTournamentDiagnosticTest
{
    [TestMethod]
    [Timeout(60000)] // 60 second timeout
    public async Task RunTournament_With100Bots_ShouldComplete()
    {
        // Arrange
        Console.WriteLine("\n=== Starting 100-bot tournament diagnostic test ===");
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

        // Create 100 simple bots using test helper
        var bots = TestHelpers.CreateDummyBotInfos(100, GameType.RPSLS);

        Console.WriteLine($"Created {bots.Count} bots");

        var stopwatch = Stopwatch.StartNew();
        
        // Act
        Console.WriteLine("Starting tournament...");
        var result = await manager.RunTournamentAsync(bots, GameType.RPSLS, config);
        
        stopwatch.Stop();
        Console.WriteLine($"Tournament completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Assert
        Assert.IsNotNull(result, "Tournament result should not be null");
        Assert.AreEqual(TournamentState.Completed, result.State, "Tournament should be completed");
        Assert.IsTrue(result.MatchResults.Count > 0, "Should have match results");
        
        Console.WriteLine($"Total matches: {result.MatchResults.Count}");
        Console.WriteLine($"Champion: {result.Champion}");
        Console.WriteLine("=== Test completed successfully ===\n");
    }
}
