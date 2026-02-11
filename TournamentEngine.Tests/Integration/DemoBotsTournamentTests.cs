using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;

namespace TournamentEngine.Tests.Integration;

[TestClass]
public class DemoBotsTournamentTests
{
    private static TournamentConfig CreateConfig() => new TournamentConfig
    {
        Games = new List<GameType> { GameType.RPSLS },
        ImportTimeout = TimeSpan.FromSeconds(5),
        MoveTimeout = TimeSpan.FromSeconds(1),
        MemoryLimitMB = 512,
        MaxRoundsRPSLS = 10,
        LogLevel = "INFO",
        LogFilePath = "tournament.log",
        BotsDirectory = "workshop/demo_bots",
        ResultsFilePath = "results.json"
    };

    [TestMethod]
    public async Task RpslsTournament_TeamPaperBeatsTeamRock()
    {
        // Arrange: two demo bots
        var bots = new List<BotInfo>
        {
            new BotInfo
            {
                TeamName = "TeamRock",
                GameType = GameType.RPSLS,
                FilePath = "workshop/demo_bots/TeamRock.cs",
                IsValid = true,
                ValidationErrors = new List<string>(),
                LoadTime = DateTime.UtcNow
            },
            new BotInfo
            {
                TeamName = "TeamPaper",
                GameType = GameType.RPSLS,
                FilePath = "workshop/demo_bots/TeamPaper.cs",
                IsValid = true,
                ValidationErrors = new List<string>(),
                LoadTime = DateTime.UtcNow
            }
        };

        var config = CreateConfig();
        var runner = new GameRunner(config);
        var scoring = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(runner, scoring);
        var manager = new TournamentManager(engine, runner);

        // Act: run tournament
        var info = await manager.RunTournamentAsync(bots, GameType.RPSLS, config, CancellationToken.None);

        // Assert: completed and champion is TeamPaper
        Assert.AreEqual(TournamentState.Completed, info.State);
        Assert.AreEqual("TeamPaper", info.Champion);
        Assert.IsTrue(info.MatchResults.Count > 0);
        Assert.IsNotNull(info.EndTime);
        Assert.IsTrue((info.EndTime - info.StartTime)?.TotalMilliseconds >= 0);
        
        // Verify results contain match TeamPaper vs TeamRock
        Assert.IsTrue(info.MatchResults.Any(m => 
            (m.Bot1Name == "TeamPaper" && m.Bot2Name == "TeamRock") ||
            (m.Bot1Name == "TeamRock" && m.Bot2Name == "TeamPaper")));
    }
}
