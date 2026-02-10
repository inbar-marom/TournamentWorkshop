namespace TournamentEngine.Tests.Tournament;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Thread-safety tests for GroupStageTournamentEngine
/// Verifies that concurrent operations don't corrupt shared state
/// </summary>
[TestClass]
public class GroupStageTournamentEngineThreadSafetyTests
{
    [TestMethod]
    public async Task RecordMatchResult_ConcurrentCalls_AllResultsRecorded()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        // Act - Record all match results concurrently
        var tasks = matches.Select(match => Task.Run(() =>
        {
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Player1Wins,
                GameType.RPSLS);
            engine.RecordMatchResult(result);
        })).ToList();

        await Task.WhenAll(tasks);

        // Assert
        var tournamentInfo = engine.GetTournamentInfo();
        Assert.AreEqual(matches.Count, tournamentInfo.MatchResults.Count, 
            "All match results should be recorded");

        // Verify no duplicates
        var uniqueResults = tournamentInfo.MatchResults
            .Select(r => $"{r.Bot1Name}|{r.Bot2Name}")
            .Distinct()
            .Count();
        Assert.AreEqual(matches.Count, uniqueResults, 
            "No duplicate match results should exist");
    }

    [TestMethod]
    public async Task RecordMatchResult_ConcurrentCalls_NoLostUpdatesToStandings()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        // Create predictable outcomes: all Player1 wins
        var results = matches.Select(match =>
            TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Player1Wins,
                GameType.RPSLS)).ToList();

        // Act - Record concurrently
        var tasks = results.Select(result => Task.Run(() =>
            engine.RecordMatchResult(result))).ToList();

        await Task.WhenAll(tasks);

        // Assert - Count expected wins/losses
        var expectedWins = results.GroupBy(r => r.Bot1Name).ToDictionary(g => g.Key, g => g.Count());
        var expectedLosses = results.GroupBy(r => r.Bot2Name).ToDictionary(g => g.Key, g => g.Count());

        var summary = engine.GetCurrentPhaseSummary();
        var standings = summary.Groups.SelectMany(g => g.Standings).ToList();

        foreach (var standing in standings)
        {
            var wins = expectedWins.GetValueOrDefault(standing.BotName, 0);
            var losses = expectedLosses.GetValueOrDefault(standing.BotName, 0);
            var expectedPoints = wins * 3;

            Assert.AreEqual(expectedPoints, standing.Points, 
                $"{standing.BotName} should have {expectedPoints} points");
            Assert.AreEqual(wins, standing.Wins, 
                $"{standing.BotName} should have {wins} wins");
            Assert.AreEqual(losses, standing.Losses, 
                $"{standing.BotName} should have {losses} losses");
        }
    }

    [TestMethod]
    public async Task RecordMatchResult_ConcurrentCalls_MatchHistoryIntact()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        // Act - Record concurrently
        var tasks = matches.Select(match => Task.Run(() =>
        {
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Draw,
                GameType.RPSLS);
            engine.RecordMatchResult(result);
        })).ToList();

        await Task.WhenAll(tasks);

        // Assert - All matches should be in history
        var tournamentInfo = engine.GetTournamentInfo();
        Assert.AreEqual(matches.Count, tournamentInfo.MatchResults.Count);

        // Verify each match was recorded exactly once
        var matchKeys = new HashSet<string>();
        foreach (var result in tournamentInfo.MatchResults)
        {
            var key = $"{result.Bot1Name}|{result.Bot2Name}";
            Assert.IsTrue(matchKeys.Add(key), 
                $"Match {key} recorded more than once");
        }
    }

    [TestMethod]
    public async Task GetTournamentInfo_ConcurrentWithRecordMatchResult_NoCorruption()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(6);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        var recordTasks = new List<Task>();
        var readTasks = new List<Task<TournamentInfo>>();

        // Act - Mix reads and writes
        foreach (var match in matches)
        {
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Player1Wins,
                GameType.RPSLS);

            recordTasks.Add(Task.Run(() => engine.RecordMatchResult(result)));
            
            // Concurrent reads
            readTasks.Add(Task.Run(() => engine.GetTournamentInfo()));
            readTasks.Add(Task.Run(() => engine.GetTournamentInfo()));
        }

        await Task.WhenAll(recordTasks.Concat(readTasks.Cast<Task>()));

        // Assert - All reads should succeed without exceptions
        Assert.AreEqual(matches.Count * 2, readTasks.Count);
        foreach (var task in readTasks)
        {
            Assert.IsNotNull(task.Result);
            Assert.IsNotNull(task.Result.MatchResults);
        }

        // Final state should be consistent
        var finalInfo = engine.GetTournamentInfo();
        Assert.AreEqual(matches.Count, finalInfo.MatchResults.Count);
    }

    [TestMethod]
    public async Task StressTest_HighConcurrency_StateRemainsConsistent()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(10);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        // Act - Simulate 100+ concurrent operations (reads + writes)
        var allTasks = new List<Task>();

        // Record all matches concurrently
        foreach (var match in matches)
        {
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Player1Wins,
                GameType.RPSLS);

            allTasks.Add(Task.Run(() => engine.RecordMatchResult(result)));
        }

        // Spawn many concurrent readers
        for (int i = 0; i < 50; i++)
        {
            allTasks.Add(Task.Run(() => engine.GetTournamentInfo()));
            allTasks.Add(Task.Run(() => engine.GetCurrentPhaseSummary()));
        }

        await Task.WhenAll(allTasks);

        // Assert
        var finalInfo = engine.GetTournamentInfo();
        Assert.AreEqual(matches.Count, finalInfo.MatchResults.Count, 
            "All matches should be recorded exactly once");

        var uniqueMatches = finalInfo.MatchResults
            .Select(r => $"{r.Bot1Name}|{r.Bot2Name}")
            .Distinct()
            .Count();
        Assert.AreEqual(matches.Count, uniqueMatches, 
            "No duplicate matches despite high concurrency");
    }

    [TestMethod]
    public async Task RecordMatchResult_ConcurrentCalls_PendingMatchesDecrementCorrectly()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var initialMatches = engine.GetNextMatches();
        var initialCount = initialMatches.Count;

        // Act - Record all matches concurrently
        var tasks = initialMatches.Select(match => Task.Run(() =>
        {
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Draw,
                GameType.RPSLS);
            engine.RecordMatchResult(result);
        })).ToList();

        await Task.WhenAll(tasks);

        // Assert - Pending matches should be empty
        var remainingMatches = engine.GetNextMatches();
        Assert.AreEqual(0, remainingMatches.Count, 
            "All pending matches should be dequeued");
        
        var tournamentInfo = engine.GetTournamentInfo();
        Assert.AreEqual(initialCount, tournamentInfo.MatchResults.Count,
            "Recorded count should match initial match count");
    }

    [TestMethod]
    public async Task RecordMatchResult_ConcurrentDifferentOutcomes_PointsCalculatedCorrectly()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(4);
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        var config = TestHelpers.CreateDefaultConfig();

        engine.InitializeTournament(bots, GameType.RPSLS, config);
        var matches = engine.GetNextMatches();

        // Create mixed outcomes: wins, losses, draws
        var outcomes = new[] { MatchOutcome.Player1Wins, MatchOutcome.Player2Wins, MatchOutcome.Draw };
        var results = matches.Select((match, index) =>
            TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                outcomes[index % outcomes.Length],
                GameType.RPSLS)).ToList();

        // Act - Record concurrently
        var tasks = results.Select(result => Task.Run(() =>
            engine.RecordMatchResult(result))).ToList();

        await Task.WhenAll(tasks);

        // Assert - Total points should equal expected
        var summary = engine.GetCurrentPhaseSummary();
        var standings = summary.Groups.SelectMany(g => g.Standings).ToList();
        
        var totalPoints = standings.Sum(s => s.Points);
        var totalWins = standings.Sum(s => s.Wins);
        var totalDraws = standings.Sum(s => s.Draws);
        var totalLosses = standings.Sum(s => s.Losses);

        // Wins + Draws/2 should equal matches count (each match produces 1 result)
        var winMatches = results.Count(r => r.Outcome == MatchOutcome.Player1Wins || r.Outcome == MatchOutcome.Player2Wins);
        var drawMatches = results.Count(r => r.Outcome == MatchOutcome.Draw);

        Assert.AreEqual(winMatches, totalWins, "Total wins should match");
        Assert.AreEqual(winMatches, totalLosses, "Total losses should match wins");
        Assert.AreEqual(drawMatches * 2, totalDraws, "Total draws should be 2x draw matches");
    }
}
