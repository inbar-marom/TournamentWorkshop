using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// Tournament configuration endpoints for getting available games and default configuration.
/// </summary>
public static class TournamentConfigEndpoints
{
    public static void MapTournamentConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/config")
            .WithName("Tournament Configuration")
            .WithOpenApi();

        group.MapGet("/default-tournament", GetDefaultTournamentConfig)
            .WithName("GetDefaultTournamentConfig")
            .WithOpenApi();

        group.MapGet("/available-games", GetAvailableGames)
            .WithName("GetAvailableGames")
            .WithOpenApi();
    }

    /// <summary>
    /// GET /api/config/default-tournament
    /// Returns the default tournament configuration (list of games that will be run)
    /// </summary>
    private static IResult GetDefaultTournamentConfig()
    {
        try
        {
            // Return the default tournament series configuration
            var defaultGames = new List<EventStepDto>
            {
                new() { StepIndex = 1, GameType = GameType.RPSLS, Status = EventStepStatus.NotStarted },
                new() { StepIndex = 2, GameType = GameType.ColonelBlotto, Status = EventStepStatus.NotStarted },
                new() { StepIndex = 3, GameType = GameType.PenaltyKicks, Status = EventStepStatus.NotStarted },
                new() { StepIndex = 4, GameType = GameType.SecurityGame, Status = EventStepStatus.NotStarted }
            };

            var config = new
            {
                SeriesName = "Dashboard Tournament Series",
                TotalSteps = defaultGames.Count,
                Steps = defaultGames,
                Description = "Default tournament configuration - RPSLS, Colonel Blotto, Penalty Kicks, Security Game"
            };

            return Results.Ok(config);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving tournament configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/config/available-games
    /// Returns list of available game types that can be used in tournaments
    /// </summary>
    private static IResult GetAvailableGames()
    {
        try
        {
            var games = new[]
            {
                new { name = "RPSLS", displayName = "Rock Paper Scissors Lizard Spock" },
                new { name = "ColonelBlotto", displayName = "Colonel Blotto" },
                new { name = "PenaltyKicks", displayName = "Penalty Kicks" },
                new { name = "SecurityGame", displayName = "Security Game" }
            };

            return Results.Ok(games);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving available games: {ex.Message}");
        }
    }
}
