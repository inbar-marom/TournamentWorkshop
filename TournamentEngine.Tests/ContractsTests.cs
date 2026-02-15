using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests;

[TestClass]
public class ContractsTests
{
    [TestMethod]
    public void GameType_EnumValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var gameTypes = Enum.GetValues<GameType>();
        
        // Assert
        Assert.AreEqual(4, gameTypes.Length);
        Assert.IsTrue(gameTypes.Contains(GameType.RPSLS));
        Assert.IsTrue(gameTypes.Contains(GameType.ColonelBlotto));
        Assert.IsTrue(gameTypes.Contains(GameType.PenaltyKicks));
        Assert.IsTrue(gameTypes.Contains(GameType.SecurityGame));
    }

    [TestMethod]
    public void MatchOutcome_EnumValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var outcomes = Enum.GetValues<MatchOutcome>();
        
        // Assert
        Assert.AreEqual(7, outcomes.Length);
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Unknown));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Player1Wins));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Player2Wins));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Draw));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.BothError));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Player1Error));
        Assert.IsTrue(outcomes.Contains(MatchOutcome.Player2Error));
    }

    [TestMethod]
    public void TournamentState_EnumValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var states = Enum.GetValues<TournamentState>();
        
        // Assert
        Assert.AreEqual(4, states.Length);
        Assert.IsTrue(states.Contains(TournamentState.NotStarted));
        Assert.IsTrue(states.Contains(TournamentState.InProgress));
        Assert.IsTrue(states.Contains(TournamentState.Completed));
        Assert.IsTrue(states.Contains(TournamentState.Cancelled));
    }

    [TestMethod]
    public void MatchResult_Creation_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var startTime = DateTime.Now;
        var endTime = startTime.AddMinutes(1);
        
        // Act
        var result = new MatchResult
        {
            Bot1Name = "TestBot1",
            Bot2Name = "TestBot2",
            GameType = GameType.RPSLS,
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = "TestBot1",
            Bot1Score = 10,
            Bot2Score = 5,
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime
        };
        
        // Assert
        Assert.AreEqual("TestBot1", result.Bot1Name);
        Assert.AreEqual("TestBot2", result.Bot2Name);
        Assert.AreEqual(GameType.RPSLS, result.GameType);
        Assert.AreEqual(MatchOutcome.Player1Wins, result.Outcome);
        Assert.AreEqual("TestBot1", result.WinnerName);
        Assert.AreEqual(10, result.Bot1Score);
        Assert.AreEqual(5, result.Bot2Score);
        Assert.AreEqual(TimeSpan.FromMinutes(1), result.Duration);
        Assert.IsNotNull(result.MatchLog);
        Assert.IsNotNull(result.Errors);
    }

    [TestMethod]
    public void GameState_Creation_ShouldInitializeCollections()
    {
        // Act
        var gameState = new GameState
        {
            CurrentRound = 1,
            MaxRounds = 50,
            IsGameOver = false,
            Winner = null
        };
        
        // Assert
        Assert.AreEqual(1, gameState.CurrentRound);
        Assert.AreEqual(50, gameState.MaxRounds);
        Assert.IsFalse(gameState.IsGameOver);
        Assert.IsNull(gameState.Winner);
        Assert.IsNotNull(gameState.State);
        Assert.IsNotNull(gameState.MoveHistory);
        Assert.AreEqual(0, gameState.State.Count);
        Assert.AreEqual(0, gameState.MoveHistory.Count);
    }

    [TestMethod]
    public void BotInfo_Creation_ShouldSetPropertiesCorrectly()
    {
        // Act
        var botInfo = new BotInfo
        {
            TeamName = "TestTeam",
            FolderPath = "test/path/bot",
            IsValid = true,
            LoadTime = DateTime.Now
        };
        
        // Assert
        Assert.AreEqual("TestTeam", botInfo.TeamName);
        Assert.AreEqual("test/path/bot", botInfo.FolderPath);
        Assert.IsTrue(botInfo.IsValid);
        Assert.IsNotNull(botInfo.ValidationErrors);
        Assert.AreEqual(0, botInfo.ValidationErrors.Count);
    }

    [TestMethod]
    public void TournamentConfig_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(5), config.ImportTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), config.MoveTimeout);
        Assert.AreEqual(512, config.MemoryLimitMB);
        Assert.AreEqual(50, config.MaxRoundsRPSLS);
        Assert.AreEqual("INFO", config.LogLevel);
        Assert.AreEqual("tournament_log.txt", config.LogFilePath);
        Assert.AreEqual("bots", config.BotsDirectory);
        Assert.AreEqual("results.json", config.ResultsFilePath);
    }

    [TestMethod]
    public void TournamentEngineException_Creation_ShouldPreserveMessage()
    {
        // Arrange
        var message = "Test exception message";
        
        // Act
        var exception = new TournamentEngineException(message);
        
        // Assert
        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void BotLoadException_Creation_ShouldPreserveDetails()
    {
        // Arrange
        var teamName = "TestTeam";
        var message = "Bot loading failed";
        
        // Act
        var exception = new BotLoadException(teamName, message);
        
        // Assert
        Assert.AreEqual(teamName, exception.TeamName);
        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void BotExecutionException_Creation_ShouldPreserveDetails()
    {
        // Arrange
        var teamName = "TestTeam";
        var gameType = GameType.RPSLS;
        var message = "Bot execution failed";
        
        // Act
        var exception = new BotExecutionException(teamName, gameType, message);
        
        // Assert
        Assert.AreEqual(teamName, exception.TeamName);
        Assert.AreEqual(gameType, exception.GameType);
        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void InvalidMoveException_Creation_ShouldPreserveDetails()
    {
        // Arrange
        var teamName = "TestTeam";
        var move = "InvalidMove";
        var message = "Invalid move attempted";
        
        // Act
        var exception = new InvalidMoveException(teamName, move, message);
        
        // Assert
        Assert.AreEqual(teamName, exception.TeamName);
        Assert.AreEqual(move, exception.Move);
        Assert.AreEqual(message, exception.Message);
    }
}
