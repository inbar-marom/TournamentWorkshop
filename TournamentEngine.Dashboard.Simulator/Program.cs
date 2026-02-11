using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

Console.WriteLine("🎮 Tournament Simulator - Live Real-Time Streaming!\n");

// Connect to Dashboard SignalR Hub
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/tournamentHub")
    .WithAutomaticReconnect()
    .Build();

connection.Closed += async (error) =>
{
    Console.WriteLine("❌ Connection closed. Reconnecting...");
    await Task.Delay(2000);
    await connection.StartAsync();
};

try
{
    await connection.StartAsync();
    Console.WriteLine("✅ Connected to Dashboard at http://localhost:5000/tournamentHub\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to connect: {ex.Message}");
    Console.WriteLine("Make sure the Dashboard is running (dotnet run in TournamentEngine.Dashboard)");
    return;
}

// Create real tournament components
var config = new TournamentConfig
{
    MaxParallelMatches = 5,
    MaxRoundsRPSLS = 50
};

var gameRunner = new GameRunner(config);
var scoringSystem = new ScoringSystem();
var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);

// Create demo bots
var botCount = 6;
Console.WriteLine($"🤖 Creating {botCount} demo bots...");
var bots = IntegrationTestHelpers.CreateVariedBots(botCount);
Console.WriteLine($"✅ Created: {string.Join(", ", bots.Select(b => b.TeamName))}\n");

// Create a SignalR event publisher that streams events in real-time
var eventPublisher = new SignalRSimulatorEventPublisher(connection, scoringSystem, bots, GameType.RPSLS);
var tournamentManager = new TournamentManager(engine, gameRunner, eventPublisher);
var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);

var seriesConfig = new TournamentSeriesConfig
{
    GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
    BaseConfig = config
};

await eventPublisher.ResetStateAsync();

Console.WriteLine("🏆 Starting REAL Tournament SERIES");
Console.WriteLine("📊 Events will stream to Dashboard in REAL-TIME as matches execute!\n");
await Task.Delay(2000);

// Run series - events stream automatically as matches complete!
var seriesInfo = await seriesManager.RunSeriesAsync(bots, seriesConfig);

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("🏆 TOURNAMENT COMPLETE!");
Console.WriteLine(new string('=', 60));
var totalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count);
var champion = seriesInfo.Tournaments.LastOrDefault()?.Champion ?? "Unknown";
Console.WriteLine($"\n🥇 Champion: {champion}");
Console.WriteLine($"📊 Total Matches: {totalMatches}");
Console.WriteLine($"🎯 Series ID: {seriesInfo.SeriesId}\n");

var rankings = scoringSystem.GetCurrentRankings(seriesInfo.Tournaments.Last());
Console.WriteLine("Final Standings:\n");

foreach (var ranking in rankings.Take(5))
{
    var medal = ranking.FinalPlacement switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => "  "
    };
    Console.WriteLine($"{medal} {ranking.FinalPlacement}. {ranking.BotName,-15} - {ranking.TotalScore,3} pts  ({ranking.Wins}W-{ranking.Losses}L)");
}

Console.WriteLine("\n✨ All events streamed to Dashboard in real-time!");
Console.WriteLine("Check http://localhost:5000 for the live dashboard view.");
Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

await connection.StopAsync();

/// <summary>
/// SignalR event publisher for the simulator
/// Publishes tournament events directly to the Dashboard Hub
/// </summary>
class SignalRSimulatorEventPublisher : ITournamentEventPublisher
{
    private readonly HubConnection _connection;
    private readonly IScoringSystem _scoringSystem;
    private readonly TournamentInfo _tournamentInfo;
    private TournamentStateDto _currentState;

    public SignalRSimulatorEventPublisher(
        HubConnection connection,
        IScoringSystem scoringSystem,
        List<BotInfo> bots,
        GameType gameType)
    {
        _connection = connection;
        _scoringSystem = scoringSystem;
        _tournamentInfo = new TournamentInfo
        {
            TournamentId = Guid.NewGuid().ToString(),
            GameType = gameType,
            State = TournamentState.NotStarted,
            Bots = bots,
            MatchResults = new List<MatchResult>(),
            Bracket = new Dictionary<int, List<string>>(),
            Champion = null,
            StartTime = DateTime.UtcNow,
            EndTime = null,
            CurrentRound = 1,
            TotalRounds = 1
        };
        _currentState = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted,
            Message = "Waiting for tournament to start...",
            TournamentId = _tournamentInfo.TournamentId,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        Console.WriteLine($"⚔️  {matchEvent.Bot1Name,-15} vs {matchEvent.Bot2Name,-15} → {matchEvent.Bot1Score,2}-{matchEvent.Bot2Score,-2} ({matchEvent.WinnerName ?? "Draw"})");

        _tournamentInfo.MatchResults.Add(new MatchResult
        {
            Bot1Name = matchEvent.Bot1Name,
            Bot2Name = matchEvent.Bot2Name,
            GameType = matchEvent.GameType,
            Outcome = matchEvent.Outcome,
            WinnerName = matchEvent.WinnerName,
            Bot1Score = matchEvent.Bot1Score,
            Bot2Score = matchEvent.Bot2Score,
            StartTime = matchEvent.CompletedAt,
            EndTime = matchEvent.CompletedAt,
            Duration = TimeSpan.Zero
        });
        
        var recentMatch = new RecentMatchDto
        {
            MatchId = matchEvent.MatchId,
            Bot1Name = matchEvent.Bot1Name,
            Bot2Name = matchEvent.Bot2Name,
            Outcome = matchEvent.Outcome,
            WinnerName = matchEvent.WinnerName,
            Bot1Score = matchEvent.Bot1Score,
            Bot2Score = matchEvent.Bot2Score,
            CompletedAt = matchEvent.CompletedAt,
            GameType = matchEvent.GameType
        };

        _currentState.RecentMatches.Add(recentMatch);
        if (_currentState.RecentMatches.Count > 50)
        {
            _currentState.RecentMatches = _currentState.RecentMatches
                .OrderByDescending(m => m.CompletedAt)
                .Take(50)
                .ToList();
        }
        
        await _connection.InvokeAsync("PublishMatchCompleted", recentMatch);
        await Task.Delay(500);
    }

    public async Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        var standings = standingsEvent.OverallStandings;
        if (standings == null || standings.Count == 0)
        {
            standings = BuildOverallStandings();
        }

        _currentState.Status = TournamentStatus.InProgress;
        _currentState.Message = "Standings updated";
        _currentState.OverallLeaderboard = standings;
        _currentState.LastUpdated = DateTime.UtcNow;

        await _connection.InvokeAsync("PublishStateUpdate", _currentState);
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedDto startEvent)
    {
        Console.WriteLine($"\n🎯 Starting {startEvent.GameType} tournament with {startEvent.TotalBots} bots\n");
        _tournamentInfo.State = TournamentState.InProgress;
        _currentState.Status = TournamentStatus.InProgress;
        _currentState.Message = "Tournament started";
        _currentState.LastUpdated = DateTime.UtcNow;
        await _connection.SendAsync("TournamentStarted", startEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        Console.WriteLine($"\n🏆 Tournament {completedEvent.TournamentNumber} completed! Champion: {completedEvent.Champion}");
        _tournamentInfo.State = TournamentState.Completed;
        _tournamentInfo.EndTime = DateTime.UtcNow;
        _tournamentInfo.Champion = completedEvent.Champion;
        _currentState.Status = TournamentStatus.Completed;
        _currentState.Message = "Tournament completed";
        _currentState.Champion = completedEvent.Champion;
        _currentState.OverallLeaderboard = BuildOverallStandings();
        _currentState.LastUpdated = DateTime.UtcNow;
        await _connection.InvokeAsync("PublishStateUpdate", _currentState);
        await _connection.SendAsync("TournamentCompleted", completedEvent);
    }

    public async Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine($"🎯 ROUND {roundEvent.RoundNumber}");
        Console.WriteLine($"{new string('=', 60)}\n");
        await _connection.SendAsync("RoundStarted", roundEvent);
    }

    public async Task UpdateCurrentStateAsync(TournamentStateDto state)
    {
        await _connection.InvokeAsync("PublishStateUpdate", state);
    }

    public async Task ResetStateAsync()
    {
        _currentState = new TournamentStateDto
        {
            Status = TournamentStatus.NotStarted,
            Message = "Starting new tournament series",
            TournamentId = _tournamentInfo.TournamentId,
            OverallLeaderboard = new List<TeamStandingDto>(),
            GroupStandings = new List<GroupDto>(),
            RecentMatches = new List<RecentMatchDto>(),
            LastUpdated = DateTime.UtcNow
        };

        await _connection.InvokeAsync("PublishStateUpdate", _currentState);
    }

    private List<TeamStandingDto> BuildOverallStandings()
    {
        var rankings = _scoringSystem.GetCurrentRankings(_tournamentInfo);
        var standings = new List<TeamStandingDto>(rankings.Count);

        foreach (var ranking in rankings)
        {
            standings.Add(new TeamStandingDto
            {
                Rank = ranking.FinalPlacement,
                TeamName = ranking.BotName,
                TotalPoints = ranking.TotalScore,
                TournamentWins = 0,
                TotalWins = ranking.Wins,
                TotalLosses = ranking.Losses,
                RankChange = 0
            });
        }

        return standings;
    }
}

class VariedBot : IBot
{
    private static readonly string[] Moves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
    private static readonly int[][] BlottoPatterns =
    {
        new[] { 20, 20, 20, 20, 20 },
        new[] { 30, 30, 20, 10, 10 },
        new[] { 40, 20, 20, 10, 10 },
        new[] { 10, 10, 20, 30, 30 },
        new[] { 50, 10, 10, 10, 20 },
        new[] { 25, 25, 25, 15, 10 },
        new[] { 34, 33, 33, 0, 0 },
        new[] { 60, 10, 10, 10, 10 }
    };

 