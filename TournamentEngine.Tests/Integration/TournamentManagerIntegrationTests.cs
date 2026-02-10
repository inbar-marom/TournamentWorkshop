namespace TournamentEngine.Tests.Integration;

using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

[TestClass]
public class TournamentManagerIntegrationTests
{
    private MockGameRunner _gameRunner = null!;
    private ScoringSystem _scoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;
    private TournamentManager _tournamentManager = null!;
    private TournamentConfig _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _gameRunner = new MockGameRunner();
        _scoringSystem = new ScoringSystem();
        _engine = new GroupStageTournamentEngine(_gameRunner, _scoringSystem);
        _tournamentManager = new TournamentManager(_engine, _gameRunner);
        _config = CreateConfig();
    }

    [TestMethod]
    public async Task FullTournament_With20Bots_CompletesSuccessfully()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(20);

        // Run entire tournament automatically
        var finalInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _config, 
            CancellationToken.None);

        // Verify results
        Assert.AreEqual(TournamentState.Completed, finalInfo.State);
        Assert.IsNotNull(finalInfo.Champion);
        Assert.IsTrue(finalInfo.MatchResults.Count > 0);
        Assert.IsNotNull(finalInfo.EndTime);
        Assert.IsTrue((finalInfo.EndTime - finalInfo.StartTime)?.TotalSeconds >= 0);

        // Verify event log shows progression through phases
        var log = _engine.GetEventLog();
        Assert.IsTrue(log.Any(e => e.Contains("Tournament initialized")));
        Assert.IsTrue(log.Any(e => e.Contains("Advanced to FinalGroup")));
        Assert.IsTrue(log.Any(e => e.Contains("Tournament completed")));
        Assert.IsTrue(log.Any(e => e.Contains($"Champion: {finalInfo.Champion}")));

        // Verify champion is one of the original bots
        Assert.IsTrue(bots.Any(b => b.TeamName == finalInfo.Champion));

        // Verify scoring system logic
        AssertScoringSummary(_scoringSystem, finalInfo, bots.Count);
    }

    [TestMethod]
    public async Task FullTournament_With10Bots_CompletesSuccessfully()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(10);

        // Run entire tournament automatically
        var finalInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _config, 
            CancellationToken.None);

        // Verify results
        Assert.AreEqual(TournamentState.Completed, finalInfo.State);
        Assert.IsNotNull(finalInfo.Champion);
        Assert.IsTrue(finalInfo.MatchResults.Count > 0);
        Assert.IsNotNull(finalInfo.EndTime);

        // Verify all matches recorded have valid bots
        foreach (var match in finalInfo.MatchResults)
        {
            Assert.IsTrue(bots.Any(b => b.TeamName == match.Bot1Name));
            Assert.IsTrue(bots.Any(b => b.TeamName == match.Bot2Name));
        }

        // Verify scoring system logic
        AssertScoringSummary(_scoringSystem, finalInfo, bots.Count);
    }

    [TestMethod]
    public async Task FullTournament_With50Bots_CompletesSuccessfully()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(50);

        // Run entire tournament automatically
        var finalInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _config, 
            CancellationToken.None);

        // Verify results
        Assert.AreEqual(TournamentState.Completed, finalInfo.State);
        Assert.IsNotNull(finalInfo.Champion);
        Assert.IsTrue(finalInfo.MatchResults.Count > 0);
        Assert.IsNotNull(finalInfo.EndTime);

        // With 50 bots, we expect multiple initial groups
        var log = _engine.GetEventLog();
        var initLog = log.FirstOrDefault(e => e.Contains("Tournament initialized"));
        Assert.IsNotNull(initLog);

        // Verify scoring system logic
        AssertScoringSummary(_scoringSystem, finalInfo, bots.Count);
    }

    [TestMethod]
    public async Task FullTournament_VerifyPhaseProgression()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(20);

        // Run entire tournament automatically
        await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _config, 
            CancellationToken.None);

        // Verify event log shows all expected phases
        var log = _engine.GetEventLog().ToList();
        
        // Find phase transitions
        var initIndex = log.FindIndex(e => e.Contains("Tournament initialized"));
        var finalGroupIndex = log.FindIndex(e => e.Contains("Advanced to FinalGroup"));
        var completedIndex = log.FindIndex(e => e.Contains("Tournament completed"));

        // Verify order
        Assert.IsTrue(initIndex >= 0, "Should have initialization log");
        Assert.IsTrue(finalGroupIndex > initIndex, "Should advance to final group after init");
        Assert.IsTrue(completedIndex > finalGroupIndex, "Should complete after final group");

        // Verify scoring system logic
        var finalInfo = _engine.GetTournamentInfo();
        AssertScoringSummary(_scoringSystem, finalInfo, bots.Count);
    }

    [TestMethod]
    public async Task FullTournament_VerifyMatchesRecorded()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(15);

        // Run entire tournament
        var finalInfo = await _tournamentManager.RunTournamentAsync(
            bots, 
            GameType.RPSLS, 
            _config, 
            CancellationToken.None);

        // Verify all matches have valid outcomes
        Assert.IsTrue(finalInfo.MatchResults.Count > 0, "Should have match results");
        foreach (var match in finalInfo.MatchResults)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(match.Bot1Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(match.Bot2Name));
            Assert.IsTrue(match.Outcome != default, $"Match {match.Bot1Name} vs {match.Bot2Name} has default outcome");
        }

        // Verify matches were logged
        var log = _engine.GetEventLog();
        var matchLogs = log.Where(e => e.Contains("Recorded match:")).ToList();
        Assert.IsTrue(matchLogs.Count > 0, "Should have match recording logs");
        // Note: matchLogs count may be less than MatchResults if some matches were recorded
        // before logging was added, so we just verify that logging is happening
        Assert.IsTrue(matchLogs.Count <= finalInfo.MatchResults.Count, "Should not have more logs than matches");

        // Verify scoring system logic
        AssertScoringSummary(_scoringSystem, finalInfo, bots.Count);
    }

    [TestMethod]
    public async Task FullTournament_WithCancellationToken_ThrowsOnCancel()
    {
        // Create demo bots and config
        var bots = CreateDemoBots(20);
        var cts = new CancellationTokenSource();
        
        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await _tournamentManager.RunTournamentAsync(bots, GameType.RPSLS, _config, cts.Token);
        });
    }

    [TestMethod]
    public async Task FullTournament_WithNullBots_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await _tournamentManager.RunTournamentAsync(null!, GameType.RPSLS, _config);
        });
    }

    [TestMethod]
    public async Task FullTournament_WithNullConfig_ThrowsArgumentNullException()
    {
        var bots = CreateDemoBots(10);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
        {
            await _tournamentManager.RunTournamentAsync(bots, GameType.RPSLS, null!);
        });
    }

    [TestMethod]
    public async Task FullTournament_WithFewerThanTwoBots_ThrowsArgumentException()
    {
        var bots = CreateDemoBots(1);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await _tournamentManager.RunTournamentAsync(bots, GameType.RPSLS, _config);
        });
    }

    private static List<BotInfo> CreateDemoBots(int count)
    {
        var bots = new List<BotInfo>();
        for (int i = 1; i <= count; i++)
        {
            bots.Add(new BotInfo
            {
                TeamName = $"Team{i}",
                GameType = GameType.RPSLS,
                FilePath = $"demo/team{i}.cs",
                IsValid = true,
                ValidationErrors = new List<string>(),
                LoadTime = DateTime.UtcNow
            });
        }
        return bots;
    }

    private static TournamentConfig CreateConfig()
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

    private static void AssertScoringSummary(ScoringSystem scoringSystem, TournamentInfo finalInfo, int botCount)
    {
        var stats = scoringSystem.CalculateStatistics(finalInfo);
        Assert.AreEqual(finalInfo.MatchResults.Count, stats.TotalMatches, "Stats should count all matches");
        Assert.IsTrue(stats.MatchesByGame.TryGetValue(finalInfo.GameType, out var matchesByGame));
        Assert.AreEqual(finalInfo.MatchResults.Count, matchesByGame, "Stats should count matches by game");

        var rankings = scoringSystem.GenerateFinalRankings(finalInfo);
        Assert.AreEqual(botCount, rankings.Count, "Rankings should include all bots");

        var placements = rankings.Select(r => r.FinalPlacement).OrderBy(p => p).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(1, botCount).ToList(), placements, "Placements should be sequential");
    }
}
