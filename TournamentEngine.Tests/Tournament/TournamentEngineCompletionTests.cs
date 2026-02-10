namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Tests for TournamentEngine - Step 5: Final group and completion
/// </summary>
[TestClass]
public class TournamentEngineCompletionTests
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
    public void AdvanceToNextRound_AfterInitialGroups_WithThreeWinners_ShouldCreateThreeFinalMatches()
    {
        // Arrange - 30 bots -> 3 groups -> 3 winners -> 3 matches in final group
        var bots = TestHelpers.CreateDummyBotInfos(30, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();

        // Act
        _engine.AdvanceToNextRound();
        var finalGroupMatches = _engine.GetNextMatches();

        // Assert
        Assert.AreEqual(3, finalGroupMatches.Count, "Final group with 3 winners should have 3 matches");
    }

    [TestMethod]
    public void IsTournamentComplete_AfterFinalGroup_ShouldReturnTrue()
    {
        // Arrange
        CompleteTournamentWithPlayer1Wins();

        // Act
        var isComplete = _engine.IsTournamentComplete();

        // Assert
        Assert.IsTrue(isComplete);
    }

    [TestMethod]
    public void AdvanceToNextRound_AfterFinalGroup_ShouldSetEndTime()
    {
        // Arrange
        CompleteTournamentWithPlayer1Wins();

        // Act
        var info = _engine.GetTournamentInfo();

        // Assert
        Assert.IsNotNull(info.EndTime, "EndTime should be set after completion");
    }

    private void CompleteTournamentWithPlayer1Wins()
    {
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
