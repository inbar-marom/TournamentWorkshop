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

// Create a SignalR event publisher that streams events in real-time
var eventPublisher = new SignalRSimulatorEventPublisher(connection);

// Create real tournament components
var config = new TournamentConfig
{
    MaxParallelMatches = 1,
    MaxRoundsRPSLS = 50
};

var gameRunner = new GameRunner(config);
var scoringSystem = new ScoringSystem();
var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
var tournamentManager = new TournamentManager(engine, gameRunner, eventPublisher);

// Create demo bots
Console.WriteLine("🤖 Creating 8 demo bots...");
var bots = await IntegrationTestHelpers.CreateDemoBots(8);
Console.WriteLine($"✅ Created: {string.Join(", ", bots.Select(b => b.TeamName))}\n");

Console.WriteLine("🏆 Starting REAL Tournament: RPSLS Championship");
Console.WriteLine("📊 Events will stream to Dashboard in REAL-TIME as matches execute!\n");
await Task.Delay(2000);

// Run tournament - events stream automatically as matches complete!
var tournamentInfo = await tournamentManager.RunTournamentAsync(bots, GameType.RPSLS, config);

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("🏆 TOURNAMENT COMPLETE!");
Console.WriteLine(new string('=', 60));
Console.WriteLine($"\n🥇 Champion: {tournamentInfo.Champion}");
Console.WriteLine($"📊 Total Matches: {tournamentInfo.MatchResults.Count}");
Console.WriteLine($"🎯 Tournament ID: {tournamentInfo.TournamentId}\n");

var rankings = scoringSystem.GetCurrentRankings(tournamentInfo);
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

    public SignalRSimulatorEventPublisher(HubConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        Console.WriteLine($"⚔️  {matchEvent.Bot1Name,-15} vs {matchEvent.Bot2Name,-15} → {matchEvent.Bot1Score,2}-{matchEvent.Bot2Score,-2} ({matchEvent.WinnerName ?? "Draw"})");
        
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
        
        await _connection.InvokeAsync("PublishMatchCompleted", recentMatch);
    }

    public async Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        await _connection.SendAsync("StandingsUpdated", standingsEvent);
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedDto startEvent)
    {
        Console.WriteLine($"\n🎯 Starting {startEvent.GameType} tournament with {startEvent.TotalBots} bots\n");
        await _connection.SendAsync("TournamentStarted", startEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        Console.WriteLine($"\n🏆 Tournament {completedEvent.TournamentNumber} completed! Champion: {completedEvent.Champion}");
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
}
