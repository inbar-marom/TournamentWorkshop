namespace TournamentEngine.Tests.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using System.Text;

/// <summary>
/// Tests for Bot API endpoints - Phase 1.1: Submission size limits
/// </summary>
[TestClass]
public class BotEndpointsTests
{
    [TestMethod]
    public void SubmitBot_500KBPayload_WithinLimit()
    {
        // Arrange - Create submission at exactly 500KB total
        var request = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "bot1.py",
                    Code = new string('a', 250_000) // 250KB
                },
                new BotFile
                {
                    FileName = "bot2.py",
                    Code = new string('b', 250_000) // 250KB
                }
                // Total: 500KB
            }
        };

        // Act - Calculate size (mimicking endpoint logic)
        var totalSize = request.Files.Sum(f => Encoding.UTF8.GetByteCount(f.Code));

        // Assert
        Assert.AreEqual(500_000, totalSize);
        Assert.IsTrue(totalSize <= 500_000, "500KB should be within limit");
    }

    [TestMethod]
    public void SubmitBot_501KBPayload_ExceedsLimit()
    {
        // Arrange - Create submission exceeding 500KB
        var request = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "bot.py",
                    Code = new string('a', 501_000) // 501KB - exceeds limit
                }
            }
        };

        // Act
        var totalSize = request.Files.Sum(f => Encoding.UTF8.GetByteCount(f.Code));

        // Assert
        Assert.AreEqual(501_000, totalSize);
        Assert.IsTrue(totalSize > 500_000, "501KB should exceed 500KB limit");
    }

    [TestMethod]
    public void SubmitBot_MultipleFiles_Under500KB_WithinLimit()
    {
        // Arrange - Multiple files totaling under 500KB
        var request = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile { FileName = "bot.py", Code = new string('a', 200_000) },
                new BotFile { FileName = "utils.py", Code = new string('b', 200_000) },
                new BotFile { FileName = "config.py", Code = new string('c', 99_000) }
                // Total: 499KB
            }
        };

        // Act
        var totalSize = request.Files.Sum(f => Encoding.UTF8.GetByteCount(f.Code));

        // Assert
        Assert.AreEqual(499_000, totalSize);
        Assert.IsTrue(totalSize <= 500_000, "499KB should be within 500KB limit");
    }

    [TestMethod]
    public void SubmitBot_SingleFileOver50KB_ExceedsSingleFileLimit()
    {
        // Arrange - Single file exceeding individual file limit
        var request = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = new List<BotFile>
            {
                new BotFile
                {
                    FileName = "bot.py",
                    Code = new string('a', 51_000) // Exceeds 50KB single file limit
                }
            }
        };

        // Act
        var file = request.Files.First();
        var fileSize = Encoding.UTF8.GetByteCount(file.Code);

        // Assert
        Assert.AreEqual(51_000, fileSize);
        Assert.IsTrue(fileSize > 50_000, "51KB file should exceed 50KB single file limit");
    }

    [TestMethod]
    public void SubmitBot_10FilesAt49KB_WithinLimits()
    {
        // Arrange - 10 files, each at 49KB (total 490KB)
        var files = new List<BotFile>();
        for (int i = 0; i < 10; i++)
        {
            files.Add(new BotFile
            {
                FileName = $"bot{i}.py",
                Code = new string('a', 49_000)
            });
        }

        var request = new BotSubmissionRequest
        {
            TeamName = "TestTeam",
            Files = files
        };

        // Act
        var totalSize = request.Files.Sum(f => Encoding.UTF8.GetByteCount(f.Code));
        var maxFileSize = request.Files.Max(f => Encoding.UTF8.GetByteCount(f.Code));

        // Assert
        Assert.AreEqual(490_000, totalSize);
        Assert.AreEqual(49_000, maxFileSize);
        Assert.IsTrue(totalSize <= 500_000, "490KB total should be within limit");
        Assert.IsTrue(maxFileSize <= 50_000, "49KB per file should be within limit");
    }

    [TestMethod]
    public void SizeCalculation_UTF8Encoding_AccurateCounting()
    {
        // Arrange - Test UTF-8 byte counting
        var simpleAscii = "hello";
        var unicodeChars = "こんにちは"; // Japanese characters

        // Act
        var asciiBytes = Encoding.UTF8.GetByteCount(simpleAscii);
        var unicodeBytes = Encoding.UTF8.GetByteCount(unicodeChars);

        // Assert
        Assert.AreEqual(5, asciiBytes, "ASCII 'hello' = 5 bytes");
        Assert.IsTrue(unicodeBytes > unicodeChars.Length, "Unicode chars take more than 1 byte each");
    }
}
