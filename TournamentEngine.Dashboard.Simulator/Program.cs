using Microsoft.AspNetCore.SignalR.Client;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

Console.WriteLine("🎮 Tournament Simulator - Starting...\n");

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

// Simulate a tournament
var teams = new[] { "TeamAlpha", "TeamBeta", "TeamGamma", "TeamDelta", 
                   "TeamEpsilon", "TeamZeta", "TeamEta", "TeamTheta" };
var random = new Random();
var standings = teams.Select((team, index) => new TeamStandingDto
{
    Rank = index + 1,
    TeamName = team,
    TotalPoints = 0,
    TotalWins = 0,
    TotalLosses = 0,
    TournamentWins = 0,
    RankChange = 0
}).ToList();

Console.WriteLine("🏆 Starting Simulated Tournament: RPSLS Championship");
Console.WriteLine("📊 8 teams competing in round-robin format\n");
await Task.Delay(2000);

// Publish initial state
await PublishStateUpdate(connection, TournamentStatus.InProgress, 
    "Tournament started - Round 1 of 7", standings, new List<RecentMatchDto>());

var allMatches = new List<RecentMatchDto>();
int matchCounter = 1;
int round = 1;
int totalRounds = 7;

// Simulate rounds
for (round = 1; round <= totalRounds; round++)
{
    Console.WriteLine($"\n{new string('=', 60)}");
    Console.WriteLine($"🎯 ROUND {round}/{totalRounds}");
    Console.WriteLine($"{new string('=', 60)}\n");
    
    // Generate matches for this round
    var roundMatches = GenerateRoundMatches(teams, round);
    
    foreach (var (team1, team2) in roundMatches)
    {
        await Task.Delay(1500); // Match duration
        
        // Simulate match result
        var bot1Score = random.Next(0, 16);
        var bot2Score = random.Next(0, 16);
        var match = new RecentMatchDto
        {
            MatchId = $"match-{matchCounter++}",
            Bot1Name = team1,
            Bot2Name = team2,
            Bot1Score = bot1Score,
            Bot2Score = bot2Score,
            WinnerName = bot1Score > bot2Score ? team1 : (bot2Score > bot1Score ? team2 : null),
            Outcome = bot1Score > bot2Score ? MatchOutcome.Player1Wins : 
                     (bot2Score > bot1Score ? MatchOutcome.Player2Wins : MatchOutcome.Draw),
            CompletedAt = DateTime.UtcNow,
            GameType = GameType.RPSLS
        };
        
        allMatches.Add(match);
        
        // Update standings
        UpdateStandings(standings, match);
        
        // Console output
        var result = match.WinnerName != null 
            ? $"{match.WinnerName} wins!" 
            : "Draw";
        Console.WriteLine($"⚔️  {team1,-15} vs {team2,-15} → {bot1Score,2}-{bot2Score,-2} ({result})");
        
        // Publish match completion
        await connection.InvokeAsync("PublishMatchCompleted", match);
        
        // Publish updated standings every few matches
        if (matchCounter % 3 == 0)
        {
            await PublishStateUpdate(connection, TournamentStatus.InProgress,
                $"Round {round}/{totalRounds} - {matchCounter-1} matches completed", 
                standings, allMatches.TakeLast(10).ToList());
        }
    }
    
    Console.WriteLine($"\n📊 Round {round} Complete!");
    await PublishStateUpdate(connection, TournamentStatus.InProgress,
        $"Round {round}/{totalRounds} completed", standings, allMatches.TakeLast(10).ToList());
}

// Tournament complete
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("🏆 TOURNAMENT COMPLETE!");
Console.WriteLine(new string('=', 60));
Console.WriteLine("\n📊 Final Standings:\n");

foreach (var team in standings.OrderBy(t => t.Rank).Take(5))
{
    var medal = team.Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => "  "
    };
    Console.WriteLine($"{medal} {team.Rank}. {team.TeamName,-15} - {team.TotalPoints,3} pts  ({team.TotalWins}W-{team.TotalLosses}L)");
}

var champion = standings.OrderBy(s => s.Rank).First();
await PublishStateUpdate(connection, TournamentStatus.Completed,
    $"Tournament finished! Champion: {champion.TeamName}", 
    standings, allMatches.TakeLast(10).ToList());

Console.WriteLine("\n✨ Simulation complete! Check the dashboard for final results.");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await connection.StopAsync();

// Helper methods
static List<(string, string)> GenerateRoundMatches(string[] teams, int round)
{
    var matches = new List<(string, string)>();
    var n = teams.Length;
    
    // Simple round-robin pairing
    for (int i = 0; i < n / 2; i++)
    {
        var idx1 = (round - 1 + i) % n;
        var idx2 = (n - 1 - i + round - 1) % n;
        if (idx1 != idx2)
        {
            matches.Add((teams[idx1], teams[idx2]));
        }
    }
    
    return matches;
}

static void UpdateStandings(List<TeamStandingDto> standings, RecentMatchDto match)
{
    var team1 = standings.First(s => s.TeamName == match.Bot1Name);
    var team2 = standings.First(s => s.TeamName == match.Bot2Name);
    
    if (match.Outcome == MatchOutcome.Player1Wins)
    {
        team1.TotalWins++;
        team1.TotalPoints += 3;
        team2.TotalLosses++;
    }
    else if (match.Outcome == MatchOutcome.Player2Wins)
    {
        team2.TotalWins++;
        team2.TotalPoints += 3;
        team1.TotalLosses++;
    }
    else
    {
        team1.TotalPoints += 1;
        team2.TotalPoints += 1;
    }
    
    // Recalculate ranks
    var ranked = standings.OrderByDescending(s => s.TotalPoints)
                         .ThenByDescending(s => s.TotalWins)
                         .ToList();
    
    for (int i = 0; i < ranked.Count; i++)
    {
        var oldRank = ranked[i].Rank;
        ranked[i].Rank = i + 1;
        ranked[i].RankChange = oldRank - ranked[i].Rank;
    }
}

static async Task PublishStateUpdate(HubConnection connection, 
    TournamentStatus status, string message, 
    List<TeamStandingDto> standings, List<RecentMatchDto> matches)
{
    var state = new TournamentStateDto
    {
        TournamentId = "simulated-tournament-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
        Status = status,
        Message = message,
        OverallLeaderboard = new List<TeamStandingDto>(standings.OrderBy(s => s.Rank)),
        RecentMatches = matches,
        LastUpdated = DateTime.UtcNow,
        CurrentTournament = new CurrentTournamentDto
        {
            TournamentNumber = 1,
            GameType = GameType.RPSLS,
            Stage = TournamentStage.GroupStage,
            CurrentRound = 1,
            TotalRounds = 7,
            MatchesCompleted = matches.Count,
            TotalMatches = 28,
            ProgressPercentage = (matches.Count / 28.0) * 100
        }
    };
    
    try
    {
        await connection.InvokeAsync("PublishStateUpdate", state);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Warning: Failed to publish state update: {ex.Message}");
    }
}
