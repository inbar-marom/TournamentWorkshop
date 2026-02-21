namespace TournamentEngine.Tests.Tournament;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

[TestClass]
public class GroupStageTournamentEngineCsvLoggingTests
{
    [TestMethod]
    public void RecordMatchResult_WithCsvLogger_AppendsCsvRowWithGroupLabel()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"tournament-engine-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "match-results.csv");

        try
        {
            var bots = TestHelpers.CreateDummyBotInfos(4);
            var gameRunner = new MockGameRunner();
            var scoringSystem = new ScoringSystem();
            var csvLogger = new MatchResultsCsvLogger(csvPath);
            var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem, csvLogger);
            var config = new TournamentConfig
            {
                ImportTimeout = TimeSpan.FromSeconds(5),
                MoveTimeout = TimeSpan.FromSeconds(1),
                MemoryLimitMB = 512,
                MaxRoundsRPSLS = 50,
                LogLevel = "INFO",
                LogFilePath = "test_tournament.log",
                BotsDirectory = "test_bots",
                ResultsFilePath = "test_results.json",
                GroupCount = 1,
                FinalistsPerGroup = 1,
                UseTiebreakers = true,
                TiebreakerGameType = GameType.ColonelBlotto
            };

            engine.InitializeTournament(bots, GameType.RPSLS, config);
            var match = engine.GetNextMatches()[0];
            var result = TestHelpers.CreateMatchResult(
                match.bot1.TeamName,
                match.bot2.TeamName,
                MatchOutcome.Player1Wins,
                GameType.RPSLS);

            // Act
            engine.RecordMatchResult(result);

            // Assert
            var csvFiles = Directory.GetFiles(tempDir, "match-results_*.csv");
            Assert.AreEqual(1, csvFiles.Length, "One run-specific CSV file should be created");
            var lines = File.ReadAllLines(csvFiles[0]);
            Assert.AreEqual(2, lines.Length, "CSV should contain header + one row");
            StringAssert.Contains(lines[1], "Group A");
            StringAssert.Contains(lines[1], match.bot1.TeamName);
            StringAssert.Contains(lines[1], match.bot2.TeamName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
