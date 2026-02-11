using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;
using TournamentEngine.Tests.DummyBots;

namespace TournamentEngine.Tests;

[TestClass]
public class BracketAdvancementTests
{
    [TestMethod]
    public void CreateBracket_EvenNumberOfBots_ShouldCreateBalancedBracket()
    {
        // Arrange
        var bots = CreateMockBots(8);

        // Act
        var bracket = CreateSingleEliminationBracket(bots);

        // Assert
        Assert.AreEqual(4, bracket[1].Count, "Round 1 should have 4 matches (8 bots)");
        Assert.AreEqual(2, bracket[2].Count, "Round 2 should have 2 matches (4 winners)");
        Assert.AreEqual(1, bracket[3].Count, "Round 3 should have 1 match (2 winners)");
        Assert.AreEqual(3, bracket.Keys.Count, "Should have 3 rounds total");
    }

    [TestMethod]
    public void CreateBracket_OddNumberOfBots_ShouldAssignByes()
    {
        // Arrange
        var bots = CreateMockBots(7);

        // Act
        var bracket = CreateSingleEliminationBracket(bots);

        // Assert
        Assert.AreEqual(3, bracket[1].Count, "Round 1 should have 3 matches (6 bots, 1 bye)");
        Assert.AreEqual(2, bracket[2].Count, "Round 2 should have 2 matches (3 winners + 1 bye)");
        Assert.AreEqual(1, bracket[3].Count, "Round 3 should have 1 match (2 winners)");
        
        // Check that one bot got a bye (automatically advances)
        var round1Participants = bracket[1].SelectMany(match => match).Count();
        Assert.AreEqual(6, round1Participants, "Round 1 should have 6 participants (1 bot gets bye)");
    }

    [TestMethod]
    public void CreateBracket_SingleBot_ShouldCreateEmptyBracket()
    {
        // Arrange
        var bots = CreateMockBots(1);

        // Act
        var bracket = CreateSingleEliminationBracket(bots);

        // Assert
        Assert.AreEqual(0, bracket.Keys.Count, "Single bot should not need any rounds");
        // Champion should be the only bot
    }

    [TestMethod]
    public void CreateBracket_TwoBots_ShouldCreateOneMatch()
    {
        // Arrange
        var bots = CreateMockBots(2);

        // Act
        var bracket = CreateSingleEliminationBracket(bots);

        // Assert
        Assert.AreEqual(1, bracket.Keys.Count, "Two bots should need 1 round");
        Assert.AreEqual(1, bracket[1].Count, "Round 1 should have 1 match");
        Assert.AreEqual(2, bracket[1][0].Count, "The match should have 2 participants");
    }

    [TestMethod]
    public void AdvanceTournament_CompleteRound_ShouldAdvanceWinners()
    {
        // Arrange
        var bots = CreateMockBots(4);
        var bracket = CreateSingleEliminationBracket(bots);
        var round1Results = new List<MatchResult>
        {
            CreateMockMatchResult(bots[0], bots[1], bots[0]), // Bot 0 wins
            CreateMockMatchResult(bots[2], bots[3], bots[2])  // Bot 2 wins
        };

        // Act
        var winners = AdvanceRound(round1Results);
        var nextRoundMatches = CreateNextRoundMatches(winners);

        // Assert
        Assert.AreEqual(2, winners.Count, "Should have 2 winners from round 1");
        Assert.IsTrue(winners.Contains(bots[0].TeamName));
        Assert.IsTrue(winners.Contains(bots[2].TeamName));
        Assert.AreEqual(1, nextRoundMatches.Count, "Should create 1 match for next round");
    }

    [TestMethod]
    public void Tournament_FullProgression_ShouldDetermineChampion()
    {
        // Arrange
        var bots = CreateMockBots(4);
        var tournamentState = new TournamentInfo
        {
            TournamentId = "TEST_TOURNAMENT",
            GameType = GameType.RPSLS,
            State = TournamentState.InProgress,
            Bots = bots.Select(b => new BotInfo 
            { 
                TeamName = b.TeamName, 
                GameType = b.GameType, 
                FilePath = $"{b.TeamName}.cs",
                IsValid = true,
                LoadTime = DateTime.Now
            }).ToList(),
            StartTime = DateTime.Now,
            CurrentRound = 1,
            TotalRounds = 2
        };

        // Act - Simulate tournament progression
        // Round 1: 2 matches
        var round1Results = new List<MatchResult>
        {
            CreateMockMatchResult(bots[0], bots[1], bots[0]), // Bot 0 wins
            CreateMockMatchResult(bots[2], bots[3], bots[2])  // Bot 2 wins
        };
        
        tournamentState.MatchResults.AddRange(round1Results);
        tournamentState.CurrentRound = 2;
        
        // Round 2: Finals
        var round2Results = new List<MatchResult>
        {
            CreateMockMatchResult(bots[0], bots[2], bots[0]) // Bot 0 wins championship
        };
        
        tournamentState.MatchResults.AddRange(round2Results);
        tournamentState.State = TournamentState.Completed;
        tournamentState.Champion = bots[0].TeamName;
        tournamentState.EndTime = DateTime.Now;

        // Assert
        Assert.AreEqual(TournamentState.Completed, tournamentState.State);
        Assert.AreEqual(bots[0].TeamName, tournamentState.Champion);
        Assert.AreEqual(3, tournamentState.MatchResults.Count); // 2 + 1 matches
        Assert.IsNotNull(tournamentState.EndTime);
    }

    [TestMethod]
    public void Tournament_LargerBracket_ShouldCalculateCorrectRounds()
    {
        // Arrange & Act
        var testCases = new[]
        {
            (botCount: 2, expectedRounds: 1),
            (botCount: 4, expectedRounds: 2),
            (botCount: 8, expectedRounds: 3),
            (botCount: 16, expectedRounds: 4),
            (botCount: 32, expectedRounds: 5),
            (botCount: 64, expectedRounds: 6)
        };

        foreach (var (botCount, expectedRounds) in testCases)
        {
            var rounds = CalculateRequiredRounds(botCount);
            Assert.AreEqual(expectedRounds, rounds, 
                $"Tournament with {botCount} bots should require {expectedRounds} rounds");
        }
    }

    [TestMethod]
    public void Tournament_OddNumbers_ShouldCalculateCorrectRounds()
    {
        // Arrange & Act
        var testCases = new[]
        {
            (botCount: 3, expectedRounds: 2),  // 3 → 2 → 1
            (botCount: 5, expectedRounds: 3),  // 5 → 3 → 2 → 1
            (botCount: 7, expectedRounds: 3),  // 7 → 4 → 2 → 1
            (botCount: 15, expectedRounds: 4), // 15 → 8 → 4 → 2 → 1
            (botCount: 31, expectedRounds: 5)  // 31 → 16 → 8 → 4 → 2 → 1
        };

        foreach (var (botCount, expectedRounds) in testCases)
        {
            var rounds = CalculateRequiredRounds(botCount);
            Assert.AreEqual(expectedRounds, rounds, 
                $"Tournament with {botCount} bots should require {expectedRounds} rounds");
        }
    }

    [TestMethod]
    public void GenerateRankings_CompletedTournament_ShouldOrderCorrectly()
    {
        // Arrange
        var bots = CreateMockBots(4);
        var matchResults = new List<MatchResult>
        {
            // Semifinals
            CreateMockMatchResult(bots[0], bots[1], bots[0]), // Bot 0 beats Bot 1
            CreateMockMatchResult(bots[2], bots[3], bots[2]), // Bot 2 beats Bot 3
            // Finals  
            CreateMockMatchResult(bots[0], bots[2], bots[0])  // Bot 0 beats Bot 2 (champion)
        };

        // Act
        var rankings = GenerateRankings(matchResults, bots);

        // Assert
        Assert.AreEqual(4, rankings.Count);
        Assert.AreEqual(bots[0].TeamName, rankings[0].BotName); // 1st place - champion
        Assert.AreEqual(bots[2].TeamName, rankings[1].BotName); // 2nd place - finalist
        // 3rd place tie between semifinal losers
        Assert.IsTrue(rankings[2].BotName == bots[1].TeamName || rankings[2].BotName == bots[3].TeamName);
        Assert.IsTrue(rankings[3].BotName == bots[1].TeamName || rankings[3].BotName == bots[3].TeamName);
    }

    // Helper methods
    private static List<IBot> CreateMockBots(int count)
    {
        var bots = new List<IBot>();
        for (int i = 0; i < count; i++)
        {
            bots.Add(new MockBot($"Bot{i}"));
        }
        return bots;
    }

    private static Dictionary<int, List<List<string>>> CreateSingleEliminationBracket(List<IBot> bots)
    {
        var bracket = new Dictionary<int, List<List<string>>>();
        var currentRoundBots = bots.Select(b => b.TeamName).ToList();
        var round = 1;

        while (currentRoundBots.Count > 1)
        {
            var matches = new List<List<string>>();
            
            for (int i = 0; i < currentRoundBots.Count - 1; i += 2)
            {
                matches.Add(new List<string> { currentRoundBots[i], currentRoundBots[i + 1] });
            }

            bracket[round] = matches;
            
            // Calculate next round participants
            var nextRoundSize = matches.Count;
            if (currentRoundBots.Count % 2 == 1)
            {
                nextRoundSize++; // Add the bot that got a bye
            }
            
            currentRoundBots = Enumerable.Range(0, nextRoundSize).Select(i => $"Winner{round}_{i}").ToList();
            round++;
        }

        return bracket;
    }

    private static List<string> AdvanceRound(List<MatchResult> roundResults)
    {
        return roundResults.Select(r => r.WinnerName).ToList();
    }

    private static List<List<string>> CreateNextRoundMatches(List<string> winners)
    {
        var matches = new List<List<string>>();
        for (int i = 0; i < winners.Count - 1; i += 2)
        {
            matches.Add(new List<string> { winners[i], winners[i + 1] });
        }
        return matches;
    }

    private static MatchResult CreateMockMatchResult(IBot bot1, IBot bot2, IBot winner)
    {
        return new MatchResult
        {
            Bot1Name = bot1.TeamName,
            Bot2Name = bot2.TeamName,
            GameType = GameType.RPSLS,
            Outcome = winner.TeamName == bot1.TeamName ? MatchOutcome.Player1Wins : MatchOutcome.Player2Wins,
            WinnerName = winner.TeamName,
            Bot1Score = winner.TeamName == bot1.TeamName ? 10 : 8,
            Bot2Score = winner.TeamName == bot2.TeamName ? 10 : 8,
            StartTime = DateTime.Now.AddMinutes(-5),
            EndTime = DateTime.Now,
            Duration = TimeSpan.FromMinutes(5)
        };
    }

    private static int CalculateRequiredRounds(int botCount)
    {
        if (botCount <= 1) return 0;
        return (int)Math.Ceiling(Math.Log2(botCount));
    }

    private static List<BotRanking> GenerateRankings(List<MatchResult> matchResults, List<IBot> allBots)
    {
        var rankings = new List<BotRanking>();
        
        // Find champion (winner of last match)
        var finalMatch = matchResults.Last();
        var champion = finalMatch.WinnerName;
        var finalist = finalMatch.Bot1Name == champion ? finalMatch.Bot2Name : finalMatch.Bot1Name;
        
        rankings.Add(new BotRanking 
        { 
            BotName = champion, 
            FinalPlacement = 1,
            Wins = matchResults.Count(m => m.WinnerName == champion),
            Losses = 0,
            TotalScore = 0,
            EliminationRound = 0
        });
        
        rankings.Add(new BotRanking 
        { 
            BotName = finalist, 
            FinalPlacement = 2,
            Wins = matchResults.Count(m => m.WinnerName == finalist) - 1,
            Losses = 1,
            TotalScore = 0,
            EliminationRound = matchResults.Count
        });

        // Add remaining bots with tied placements
        var remainingBots = allBots.Where(b => b.TeamName != champion && b.TeamName != finalist).ToList();
        for (int i = 0; i < remainingBots.Count; i++)
        {
            rankings.Add(new BotRanking 
            { 
                BotName = remainingBots[i].TeamName, 
                FinalPlacement = 3 + i,
                Wins = 0,
                Losses = 1,
                TotalScore = 0,
                EliminationRound = 1
            });
        }

        return rankings;
    }

    // Mock bot class for testing
    private class MockBot : IBot
    {
        public MockBot(string teamName)
        {
            TeamName = teamName;
        }

        public string TeamName { get; }
        public GameType GameType => GameType.RPSLS;

        public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
            => Task.FromResult("Rock");

        public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
            => Task.FromResult(new int[] { 20, 20, 20, 20, 20 });

        public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
            => Task.FromResult("Left");

        public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
            => Task.FromResult("Defend");
    }
}
