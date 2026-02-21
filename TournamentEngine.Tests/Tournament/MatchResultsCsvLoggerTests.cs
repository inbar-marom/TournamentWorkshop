namespace TournamentEngine.Tests.Tournament;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;

[TestClass]
public class MatchResultsCsvLoggerTests
{
    [TestMethod]
    public async Task AppendMatchResultAsync_FirstWrite_CreatesHeaderAndRowWithRequiredFields()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"tournament-csv-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "match-results.csv");

        try
        {
            var logger = new MatchResultsCsvLogger(filePath);
            logger.StartTournamentRun("tournament-1", GameType.RPSLS);
            var startTime = DateTime.UtcNow;
            var matchResult = new MatchResult
            {
                GameType = GameType.RPSLS,
                Bot1Name = "BotA",
                Bot2Name = "BotB",
                Outcome = MatchOutcome.Player1Wins,
                WinnerName = "BotA",
                Bot1Score = 3,
                Bot2Score = 1,
                StartTime = startTime,
                EndTime = startTime.AddSeconds(2),
                Duration = TimeSpan.FromSeconds(2),
                MatchLog = new List<string>
                {
                    "Round 1: BotA=Rock, BotB=Scissors",
                    "Round 2: BotA=Paper, BotB=Rock"
                },
                Errors = new List<string>()
            };

            // Act
            logger.AppendMatchResult(matchResult, "Group C");

            // Assert
            var csvFiles = Directory.GetFiles(tempDir, "match-results_*.csv");
            Assert.AreEqual(1, csvFiles.Length, "Exactly one run CSV file should be created");
            var lines = await File.ReadAllLinesAsync(csvFiles[0]);
            Assert.AreEqual(2, lines.Length, "CSV should include header and one data line");

            var header = lines[0];
            StringAssert.Contains(header, "GameType");
            StringAssert.Contains(header, "PlayerA");
            StringAssert.Contains(header, "PlayerB");
            StringAssert.Contains(header, "Group");
            StringAssert.Contains(header, "StartTimeUtc");
            StringAssert.Contains(header, "DurationMs");
            StringAssert.Contains(header, "MatchOutcome");
            StringAssert.Contains(header, "SubActsJson");

            var row = lines[1];
            StringAssert.Contains(row, "RPSLS");
            StringAssert.Contains(row, "BotA");
            StringAssert.Contains(row, "BotB");
            StringAssert.Contains(row, "Group C");
            StringAssert.Contains(row, $",{(int)MatchOutcome.Player1Wins},");
            StringAssert.Contains(row, "Round 1: BotA=Rock, BotB=Scissors");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public async Task AppendMatchResultAsync_ConcurrentWrites_WritesAllRowsOnce()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"tournament-csv-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "match-results.csv");

        try
        {
            var logger = new MatchResultsCsvLogger(filePath);
            logger.StartTournamentRun("tournament-2", GameType.RPSLS);
            const int matchCount = 40;

            var tasks = Enumerable.Range(1, matchCount).Select(i =>
            {
                var result = new MatchResult
                {
                    GameType = GameType.RPSLS,
                    Bot1Name = $"BotA{i}",
                    Bot2Name = $"BotB{i}",
                    Outcome = MatchOutcome.Draw,
                    WinnerName = null,
                    Bot1Score = 1,
                    Bot2Score = 1,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMilliseconds(200),
                    Duration = TimeSpan.FromMilliseconds(200),
                    MatchLog = new List<string> { $"Round 1: BotA{i}=Rock, BotB{i}=Rock" },
                    Errors = new List<string>()
                };

                return Task.Run(() => logger.AppendMatchResult(result, "Final Group-finalStandings"));
            }).ToList();

            // Act
            await Task.WhenAll(tasks);

            // Assert
            var csvFiles = Directory.GetFiles(tempDir, "match-results_*.csv");
            Assert.AreEqual(1, csvFiles.Length, "Concurrent writes in one run should stay in one file");
            var lines = await File.ReadAllLinesAsync(csvFiles[0]);
            Assert.AreEqual(matchCount + 1, lines.Length, "CSV should include one header and one row per match");
            Assert.AreEqual(lines.Skip(1).Count(l => l.Contains("Final Group-finalStandings")), matchCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public void StartTournamentRun_CalledTwice_CreatesDifferentFilesPerRun()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"tournament-csv-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "match-results.csv");

        try
        {
            var logger = new MatchResultsCsvLogger(filePath);
            var result = new MatchResult
            {
                GameType = GameType.RPSLS,
                Bot1Name = "BotA",
                Bot2Name = "BotB",
                Outcome = MatchOutcome.Draw,
                WinnerName = null,
                Bot1Score = 1,
                Bot2Score = 1,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(100),
                MatchLog = new List<string> { "Round 1" },
                Errors = new List<string>()
            };

            // Act
            logger.StartTournamentRun("run-1", GameType.RPSLS);
            logger.AppendMatchResult(result, "Group A");

            logger.StartTournamentRun("run-2", GameType.RPSLS);
            logger.AppendMatchResult(result, "Group A");

            // Assert
            var csvFiles = Directory.GetFiles(tempDir, "match-results_*.csv");
            Assert.AreEqual(2, csvFiles.Length, "Each tournament run should create a separate CSV file");
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
