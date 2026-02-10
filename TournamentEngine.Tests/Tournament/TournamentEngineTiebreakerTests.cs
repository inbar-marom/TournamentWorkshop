namespace TournamentEngine.Tests.Tournament;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

/// <summary>
/// Tests for TournamentEngine - Step 6: Tiebreaker handling
/// </summary>
[TestClass]
public class TournamentEngineTiebreakerTests
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
    public void AdvanceToNextRound_AfterFinalGroupTie_ShouldQueueTiebreakerMatch()
    {
        // Arrange - 20 bots -> 2 groups -> final group has 2 bots
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        var finalMatch = finalGroupMatches[0];
        var draw = TestHelpers.CreateMatchResult(finalMatch.bot1.TeamName, finalMatch.bot2.TeamName, MatchOutcome.Draw);
        _engine.RecordMatchResult(draw);

        // Act
        _engine.AdvanceToNextRound();
        var tiebreakerMatches = _engine.GetNextMatches();

        // Assert
        Assert.IsTrue(tiebreakerMatches.Count > 0, "Tie should schedule at least one tiebreaker match");
    }

    [TestMethod]
    public void AdvanceToNextRound_AfterFinalGroupTie_ShouldNotCompleteTournament()
    {
        // Arrange - 20 bots -> 2 groups -> final group has 2 bots
        var bots = TestHelpers.CreateDummyBotInfos(20, GameType.RPSLS);
        var config = TestHelpers.CreateDefaultConfig();
        _engine.InitializeTournament(bots, GameType.RPSLS, config);
        RecordAllCurrentMatchesAsPlayer1Wins();
        _engine.AdvanceToNextRound();

        var finalGroupMatches = _engine.GetNextMatches();
        var finalMatch = finalGroupMatches[0];
        var draw = TestHelpers.CreateMatchResult(finalMatch.bot1.TeamName, finalMatch.bot2.TeamName, MatchOutcome.Draw);
        _engine.RecordMatchResult(draw);

        // Act
        var info = _engine.AdvanceToNextRound();

        // Assert
        Assert.IsFalse(_engine.IsTournamentComplete(), "Tournament should not complete on a tie");
        Assert.IsNull(info.Champion, "Champion should remain unset during tiebreaker");
        Assert.IsNull(info.EndTime, "EndTime should not be set during tiebreaker");
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
