using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using TournamentEngine.Console.Utilities;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Tests.Helpers;

Console.WriteLine("🎮 Tournament Simulator - Live Real-Time Streaming!\n");

// Setup logging
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Simulator");

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
var botCount = 10;
Console.WriteLine($"🤖 Creating {botCount} demo bots...");
var bots = IntegrationTestHelpers.CreateVariedBots(botCount);
Console.WriteLine($"✅ Created: {string.Join(", ", bots.Select(b => b.TeamName))}\n");

// Use the same ConsoleEventPublisher as the main console app
var publisherLogger = loggerFactory.CreateLogger<ConsoleEventPublisher>();
var eventPublisher = new ConsoleEventPublisher("http://localhost:5000/tournamentHub", publisherLogger);

// IMPORTANT: Wait for connection to be established before starting tournament
Console.WriteLine("⏳ Connecting to Dashboard...");
var connected = await eventPublisher.EnsureConnectedAsync();
if (connected)
{
    Console.WriteLine("✅ Connected to Dashboard successfully!\n");
}
else
{
    Console.WriteLine("⚠️  Could not connect to Dashboard - events will not be published\n");
}

var tournamentManager = new TournamentManager(engine, gameRunner, eventPublisher);
var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem, eventPublisher);

var seriesConfig = new TournamentSeriesConfig
{
    GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks },
    BaseConfig = config
};

Console.WriteLine("🏆 Starting REAL Tournament SERIES");
Console.WriteLine("📊 Events will stream to Dashboard in REAL-TIME as matches execute!\n");

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

 