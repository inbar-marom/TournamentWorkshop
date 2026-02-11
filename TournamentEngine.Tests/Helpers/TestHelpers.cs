namespace TournamentEngine.Tests.Helpers;

using System.Linq;
using TournamentEngine.Core.Common;

/// <summary>
/// Test helper class for creating dummy bots and test data
/// </summary>
public static class TestHelpers
{
    public static List<BotInfo> CreateDummyBotInfos(int count, GameType gameType = GameType.RPSLS)
    {
        var bots = new List<BotInfo>();
        for (int i = 0; i < count; i++)
        {
            bots.Add(new BotInfo
            {
                TeamName = $"Team{i + 1}",
                FolderPath = $"bots/team{i + 1}",
                IsValid = true,
                ValidationErrors = new List<string>(),
                LoadTime = DateTime.UtcNow
            });
        }
        return bots;
    }

    public static List<IBot> CreateDummyBots(int count, GameType gameType = GameType.RPSLS)
    {
        var bots = new List<IBot>();
        for (int i = 0; i < count; i++)
        {
            bots.Add(new DummyBot($"Team{i + 1}", gameType));
        }
        return bots;
    }

    public static TournamentConfig CreateDefaultConfig()
    {
        return new TournamentConfig
        {
            Games = new List<GameType> { GameType.RPSLS },
            ImportTimeout = TimeSpan.FromSeconds(5),
            MoveTimeout = TimeSpan.FromSeconds(1),
            MemoryLimitMB = 512,
            MaxRoundsRPSLS = 50,
            LogLevel = "INFO",
            LogFilePath = "test_tournament.log",
            BotsDirectory = "test_bots",
            ResultsFilePath = "test_results.json"
        };
    }

    public static MatchResult CreateMatchResult(string bot1, string bot2, MatchOutcome outcome, GameType gameType = GameType.RPSLS)
    {
        return new MatchResult
        {
            Bot1Name = bot1,
            Bot2Name = bot2,
            GameType = gameType,
            Outcome = outcome,
            WinnerName = outcome switch
            {
                MatchOutcome.Player1Wins => bot1,
                MatchOutcome.Player2Wins => bot2,
                _ => string.Empty
            },
            Bot1Score = outcome == MatchOutcome.Player1Wins ? 30 : 20,
            Bot2Score = outcome == MatchOutcome.Player2Wins ? 30 : 20,
            MatchLog = new List<string> { "Test match" },
            Errors = new List<string>(),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(1),
            Duration = TimeSpan.FromSeconds(1)
        };
    }
}

/// <summary>
/// Dummy bot implementation for testing
/// </summary>
public class DummyBot : IBot
{
    public string TeamName { get; }
    public GameType GameType { get; }

    public DummyBot(string teamName, GameType gameType)
    {
        TeamName = teamName;
        GameType = gameType;
    }

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Rock");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Left");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Defend");
    }
}

/// <summary>
/// Mock IGameRunner for testing
/// </summary>
public class MockGameRunner : IGameRunner
{
    public List<(IBot bot1, IBot bot2, GameType gameType)> ExecutedMatches { get; } = new();

    public Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, GameType gameType, CancellationToken cancellationToken)
    {
        ExecutedMatches.Add((bot1, bot2, gameType));

        var result = new MatchResult
        {
            Bot1Name = bot1.TeamName,
            Bot2Name = bot2.TeamName,
            GameType = gameType,
            Outcome = MatchOutcome.Player1Wins,
            WinnerName = bot1.TeamName,
            Bot1Score = 30,
            Bot2Score = 20,
            MatchLog = new List<string> { "Mock match executed" },
            Errors = new List<string>(),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(1),
            Duration = TimeSpan.FromSeconds(1)
        };

        return Task.FromResult(result);
    }

    public Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, IGame game, CancellationToken cancellationToken)
    {
        return ExecuteMatch(bot1, bot2, game.GameType, cancellationToken);
    }

    public Task<bool> ValidateBot(IBot bot, GameType gameType)
    {
        return Task.FromResult(true);
    }
}

/// <summary>
/// Mock IScoringSystem for testing
/// </summary>
public class MockScoringSystem : IScoringSystem
{
    public (int player1Score, int player2Score) CalculateMatchScore(MatchResult matchResult)
    {
        return matchResult.Outcome switch
        {
            MatchOutcome.Player1Wins => (3, 0),
            MatchOutcome.Player2Wins => (0, 3),
            MatchOutcome.Draw => (1, 1),
            _ => (0, 0)
        };
    }

    public Dictionary<string, TournamentStanding> UpdateStandings(MatchResult matchResult, Dictionary<string, TournamentStanding> currentStandings)
    {
        return currentStandings;
    }

    public List<BotRanking> GenerateFinalRankings(TournamentInfo tournamentInfo)
    {
        return new List<BotRanking>();
    }

    public TournamentStatistics CalculateStatistics(TournamentInfo tournamentInfo)
    {
        return new TournamentStatistics
        {
            TotalMatches = 0,
            TotalRounds = 0,
            AverageMatchDuration = TimeSpan.Zero
        };
    }

    public List<BotRanking> GetCurrentRankings(TournamentInfo tournamentInfo)
    {
        return new List<BotRanking>();
    }
}

/// <summary>
/// Mock ITournamentEngine for unit testing TournamentManager
/// </summary>
public class MockTournamentEngine : ITournamentEngine
{
    public List<string> MethodCalls { get; } = new();
    public Queue<List<(IBot, IBot)>> MatchBatches { get; } = new();
    public bool IsComplete { get; set; } = false;
    public TournamentInfo FinalInfo { get; set; } = new()
    {
        TournamentId = Guid.NewGuid().ToString(),
        GameType = GameType.RPSLS,
        State = TournamentState.NotStarted
    };
    public int InitializeCallCount { get; private set; }
    public int GetNextMatchesCallCount { get; private set; }
    public int RecordMatchResultCallCount { get; private set; }
    public int AdvanceToNextRoundCallCount { get; private set; }
    public int GetTournamentInfoCallCount { get; private set; }

    public TournamentInfo InitializeTournament(List<BotInfo> bots, GameType gameType, TournamentConfig config)
    {
        MethodCalls.Add($"InitializeTournament(bots:{bots.Count}, {gameType})");
        InitializeCallCount++;
        FinalInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = gameType,
            State = TournamentState.InProgress,
            StartTime = DateTime.UtcNow,
            Bots = bots,
            MatchResults = new List<MatchResult>()
        };
        return FinalInfo;
    }

    public List<(IBot, IBot)> GetNextMatches()
    {
        MethodCalls.Add("GetNextMatches()");
        GetNextMatchesCallCount++;
        
        if (MatchBatches.Count > 0)
        {
            return MatchBatches.Dequeue();
        }
        
        return new List<(IBot, IBot)>();
    }

    public TournamentInfo RecordMatchResult(MatchResult result)
    {
        MethodCalls.Add($"RecordMatchResult({result.Bot1Name} vs {result.Bot2Name})");
        RecordMatchResultCallCount++;
        FinalInfo.MatchResults.Add(result);
        return FinalInfo;
    }

    public TournamentInfo AdvanceToNextRound()
    {
        MethodCalls.Add("AdvanceToNextRound()");
        AdvanceToNextRoundCallCount++;
        
        if (MatchBatches.Count == 0)
        {
            IsComplete = true;
            FinalInfo.State = TournamentState.Completed;
            FinalInfo.EndTime = DateTime.UtcNow;
            FinalInfo.Champion = FinalInfo.Bots?.FirstOrDefault()?.TeamName ?? "Team1";
        }
        
        return FinalInfo;
    }

    public bool IsTournamentComplete()
    {
        MethodCalls.Add("IsTournamentComplete()");
        return IsComplete;
    }

    public TournamentInfo GetTournamentInfo()
    {
        MethodCalls.Add("GetTournamentInfo()");
        GetTournamentInfoCallCount++;
        return FinalInfo;
    }

    public int GetCurrentRound()
    {
        MethodCalls.Add("GetCurrentRound()");
        return 1;
    }

    public List<IBot> GetRemainingBots()
    {
        MethodCalls.Add("GetRemainingBots()");
        return new List<IBot>();
    }

    public List<(IBot bot, int placement)> GetFinalRankings()
    {
        MethodCalls.Add("GetFinalRankings()");
        return new List<(IBot bot, int placement)>();
    }

    public IReadOnlyList<string> GetEventLog()
    {
        MethodCalls.Add("GetEventLog()");
        return new List<string> { "Tournament initialized", "Advanced to FinalGroup", "Tournament completed" };
    }
}
