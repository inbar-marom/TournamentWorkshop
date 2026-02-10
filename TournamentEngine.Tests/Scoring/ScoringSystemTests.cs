namespace TournamentEngine.Tests.Scoring;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Tests.Helpers;

[TestClass]
public class ScoringSystemTests
{
    private ScoringSystem _scoringSystem = null!;

    [TestInitialize]
    public void Setup()
    {
        _scoringSystem = new ScoringSystem();
    }

    [TestMethod]
    public void CalculateMatchScore_Player1Wins_ReturnsThreeZero()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Player1Wins);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(3, player1Score);
        Assert.AreEqual(0, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_Player2Wins_ReturnsZeroThree()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Player2Wins);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(0, player1Score);
        Assert.AreEqual(3, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_Draw_ReturnsOneOne()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Draw);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(1, player1Score);
        Assert.AreEqual(1, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_Player1Error_ReturnsZeroThree()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Player1Error);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(0, player1Score);
        Assert.AreEqual(3, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_Player2Error_ReturnsThreeZero()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Player2Error);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(3, player1Score);
        Assert.AreEqual(0, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_BothError_ReturnsZeroZero()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.BothError);

        // Act
        var (player1Score, player2Score) = scoringSystem.CalculateMatchScore(matchResult);

        // Assert
        Assert.AreEqual(0, player1Score);
        Assert.AreEqual(0, player2Score);
    }

    [TestMethod]
    public void CalculateMatchScore_UnknownOutcome_Throws()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Unknown);

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
        {
            scoringSystem.CalculateMatchScore(matchResult);
        });
    }

    [TestMethod]
    public void UpdateStandings_Player1Wins_UpdatesStandingsAndOpponents()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Player1Wins);
        var standings = new Dictionary<string, TournamentStanding>();

        // Act
        var updated = scoringSystem.UpdateStandings(matchResult, standings);

        // Assert
        Assert.AreEqual(2, updated.Count);

        var bot1 = updated["Bot1"];
        var bot2 = updated["Bot2"];

        Assert.AreEqual(1, bot1.Wins);
        Assert.AreEqual(0, bot1.Losses);
        Assert.AreEqual(3, bot1.TotalScore);
        Assert.AreEqual(0, bot1.TotalOpponentScore);
        CollectionAssert.Contains(bot1.OpponentsPlayed, "Bot2");

        Assert.AreEqual(0, bot2.Wins);
        Assert.AreEqual(1, bot2.Losses);
        Assert.AreEqual(0, bot2.TotalScore);
        Assert.AreEqual(3, bot2.TotalOpponentScore);
        CollectionAssert.Contains(bot2.OpponentsPlayed, "Bot1");
    }

    [TestMethod]
    public void UpdateStandings_Draw_UpdatesScoresForBoth()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var matchResult = TestHelpers.CreateMatchResult("Bot1", "Bot2", MatchOutcome.Draw);
        var standings = new Dictionary<string, TournamentStanding>();

        // Act
        var updated = scoringSystem.UpdateStandings(matchResult, standings);

        // Assert
        var bot1 = updated["Bot1"];
        var bot2 = updated["Bot2"];

        Assert.AreEqual(0, bot1.Wins);
        Assert.AreEqual(0, bot1.Losses);
        Assert.AreEqual(1, bot1.TotalScore);
        Assert.AreEqual(1, bot1.TotalOpponentScore);

        Assert.AreEqual(0, bot2.Wins);
        Assert.AreEqual(0, bot2.Losses);
        Assert.AreEqual(1, bot2.TotalScore);
        Assert.AreEqual(1, bot2.TotalOpponentScore);
    }

    [TestMethod]
    public void GetCurrentRankings_OrdersByScoreThenWins()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var bots = TestHelpers.CreateDummyBotInfos(3, GameType.RPSLS);
        var matchResults = new List<MatchResult>
        {
            TestHelpers.CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins),
            TestHelpers.CreateMatchResult("Team2", "Team3", MatchOutcome.Player1Wins),
            TestHelpers.CreateMatchResult("Team1", "Team3", MatchOutcome.Draw)
        };

        var tournamentInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = GameType.RPSLS,
            State = TournamentState.InProgress,
            Bots = bots,
            MatchResults = matchResults,
            StartTime = DateTime.UtcNow
        };

        // Act
        var rankings = scoringSystem.GetCurrentRankings(tournamentInfo);

        // Assert
        Assert.AreEqual(3, rankings.Count);
        Assert.AreEqual("Team1", rankings[0].BotName);
        Assert.AreEqual("Team2", rankings[1].BotName);
        Assert.AreEqual("Team3", rankings[2].BotName);

        Assert.AreEqual(1, rankings[0].Wins);
        Assert.AreEqual(0, rankings[0].Losses);
        Assert.AreEqual(4, rankings[0].TotalScore);
    }

    [TestMethod]
    public void GenerateFinalRankings_AssignsPlacementsInOrder()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var bots = TestHelpers.CreateDummyBotInfos(3, GameType.RPSLS);
        var matchResults = new List<MatchResult>
        {
            TestHelpers.CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins),
            TestHelpers.CreateMatchResult("Team2", "Team3", MatchOutcome.Player1Wins),
            TestHelpers.CreateMatchResult("Team1", "Team3", MatchOutcome.Draw)
        };

        var tournamentInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = GameType.RPSLS,
            State = TournamentState.Completed,
            Bots = bots,
            MatchResults = matchResults,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };

        // Act
        var rankings = scoringSystem.GenerateFinalRankings(tournamentInfo);

        // Assert
        Assert.AreEqual(3, rankings.Count);
        Assert.AreEqual("Team1", rankings[0].BotName);
        Assert.AreEqual(1, rankings[0].FinalPlacement);
        Assert.AreEqual("Team2", rankings[1].BotName);
        Assert.AreEqual(2, rankings[1].FinalPlacement);
        Assert.AreEqual("Team3", rankings[2].BotName);
        Assert.AreEqual(3, rankings[2].FinalPlacement);
    }

    [TestMethod]
    public void CalculateStatistics_ReturnsTotalsAndAverages()
    {
        // Arrange
        var scoringSystem = _scoringSystem;
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(10);

        MatchResult CreateMatch(string bot1, string bot2, MatchOutcome outcome, int durationSeconds, List<string>? errors = null)
        {
            return new MatchResult
            {
                Bot1Name = bot1,
                Bot2Name = bot2,
                GameType = GameType.RPSLS,
                Outcome = outcome,
                WinnerName = outcome switch
                {
                    MatchOutcome.Player1Wins => bot1,
                    MatchOutcome.Player2Wins => bot2,
                    _ => string.Empty
                },
                Bot1Score = 0,
                Bot2Score = 0,
                MatchLog = new List<string>(),
                Errors = errors ?? new List<string>(),
                StartTime = startTime,
                EndTime = startTime.AddSeconds(durationSeconds),
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };
        }

        var matchResults = new List<MatchResult>
        {
            CreateMatch("Bot1", "Bot2", MatchOutcome.Player1Wins, 2),
            CreateMatch("Bot2", "Bot3", MatchOutcome.Player1Wins, 4, new List<string> { "timeout" }),
            CreateMatch("Bot2", "Bot1", MatchOutcome.Player1Wins, 1)
        };

        var tournamentInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = GameType.RPSLS,
            State = TournamentState.Completed,
            Bots = TestHelpers.CreateDummyBotInfos(3, GameType.RPSLS),
            MatchResults = matchResults,
            StartTime = startTime,
            EndTime = endTime,
            TotalRounds = 3
        };

        // Act
        var stats = scoringSystem.CalculateStatistics(tournamentInfo);

        // Assert
        Assert.AreEqual(3, stats.TotalMatches);
        Assert.AreEqual(3, stats.TotalRounds);
        Assert.AreEqual(TimeSpan.FromSeconds(10), stats.TournamentDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(7d / 3d), stats.AverageMatchDuration);
        Assert.AreEqual(1, stats.TotalErrors);
        Assert.AreEqual(1, stats.TotalTimeouts);
        Assert.AreEqual("Bot2", stats.MostActiveBot);
        Assert.AreEqual("Bot2", stats.HighestScoringBot);
        Assert.AreEqual(3, stats.MatchesByGame[GameType.RPSLS]);
    }
}
