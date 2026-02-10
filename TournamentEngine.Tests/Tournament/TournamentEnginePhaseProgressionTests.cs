namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Tests for TournamentEngine - Step 4: Phase progression
/// </summary>
[TestClass]
public class TournamentEnginePhaseProgressionTests
{
    private MockGameRunner _mockGameRunner = null!;
    private MockScoringSystem _mockScoringSystem = null!;
    private GroupStageTournamentEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGameRunner = new MockGameRunner();
        _mockScoringSystem = new MockScoringSystem();
        _engine = new GroupStageTournamentEngine(_mockGameRunner, _mockScoringSystem);
    }

    [TestMethod]
    public void AdvanceToNextRound_AfterInitialGroups_ShouldCreateFinalGroupMatches()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();

        // Act
        _engine.AdvanceToNextRound();
        var finalGroupMatches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(1, finalGroupMatches.Count, "Final group with 2 winners should have 1 match");
    }

    [TestMethod]
    public void AdvanceToNextRound_AfterFinalGroup_ShouldCompleteTournament()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in finalGroupMatches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }

        // Act
        var updatedInfo = _engine.AdvanceToNextRound();

        // Assert
        Assert.IsNotNull(updatedInfo.Champion, "Tournament should have a champion after final group");
        Assert.AreEqual(TournamentState.Completed, updatedInfo.State);
    }

    [TestMethod]
    public void AdvanceToNextRound_WithIncompleteMatches_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() => _engine.AdvanceToNextRound());
    }

    [TestMethod]
    public void AdvanceToNextRound_WhenAlreadyCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange — run full tournament to completion
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in finalGroupMatches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }
        _engine.AdvanceToNextRound();

        // Act & Assert — calling advance on completed tournament
        Assert.ThrowsException<InvalidOperationException>(() => _engine.AdvanceToNextRound());
    }

    [TestMethod]
    public void RecordMatchResult_AfterTournamentComplete_ShouldThrowInvalidOperationException()
    {
        // Arrange — run full tournament to completion
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in finalGroupMatches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }
        _engine.AdvanceToNextRound();

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            _engine.RecordMatchResult(TestHelpers.CreateMatchResult("Team1", "Team2", MatchOutcome.Player1Wins)));
    }

    [TestMethod]
    public void AdvanceToNextRound_AfterInitialGroups_ShouldSetCurrentRoundTo2()
    {
        // Arrange
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();

        // Act
        var info = _engine.AdvanceToNextRound();

        // Assert
        Assert.AreEqual(2, info.CurrentRound);
        Assert.AreEqual(2, _engine.GetCurrentRound());
    }

    private void RecordAllCurrentMatchesAsPlayer1Wins()
    {
        var matches = _engine.GetNextMatches();
        foreach (var (bot1, bot2) in matches)
        {
            var result = TestHelpers.CreateMatchResult(bot1.TeamName, bot2.TeamName, MatchOutcome.Player1Wins);
            _engine.RecordMatchResult(result);
        }
    }
}
