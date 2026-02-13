using TournamentEngine.Api.Models;
using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// Bot Dashboard API endpoints for viewing and managing bot submissions.
/// </summary>
public static class BotDashboardEndpoints
{
    /// <summary>
    /// Registers all bot dashboard endpoints with the web application.
    /// </summary>
    public static void MapBotDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard/bots")
            .WithName("BotDashboard")
            .WithOpenApi();

        // Get all bots
        group.MapGet("/", GetAllBots)
            .WithName("GetAllBots")
            .WithSummary("Get all bot submissions")
            .WithDescription("Retrieves a list of all submitted bots with their metadata and validation status.");

        // Get bot details
        group.MapGet("/{teamName}", GetBotDetails)
            .WithName("GetBotDetails")
            .WithSummary("Get bot details")
            .WithDescription("Retrieves detailed information about a specific bot submission.");

        // Get bot version history
        group.MapGet("/{teamName}/history", GetBotHistory)
            .WithName("GetBotHistory")
            .WithSummary("Get bot version history")
            .WithDescription("Retrieves the version history for a specific bot submission.");

        // Get bot errors
        group.MapGet("/{teamName}/errors", GetBotErrors)
            .WithName("GetBotErrors")
            .WithSummary("Get bot compilation errors")
            .WithDescription("Retrieves compilation errors for a specific bot submission.");

        // Search bots
        group.MapGet("/search/{searchTerm}", SearchBots)
            .WithName("SearchBots")
            .WithSummary("Search bots by team name")
            .WithDescription("Searches for bots matching the given team name.");

        // Filter bots by status
        group.MapGet("/filter/{status}", FilterBotsByStatus)
            .WithName("FilterBotsByStatus")
            .WithSummary("Filter bots by validation status")
            .WithDescription("Filters bots by their validation status (Valid, Invalid, Pending).");

        // Validate a bot
        group.MapPost("/{teamName}/validate", ValidateBot)
            .WithName("ValidateBot")
            .WithSummary("Validate bot submission")
            .WithDescription("Triggers validation of a bot submission using the BotLoader.");
    }

    /// <summary>
    /// Gets all bot submissions.
    /// </summary>
    private static async Task<IResult> GetAllBots(BotDashboardService dashboardService)
    {
        try
        {
            var bots = await dashboardService.GetAllBotsAsync();
            return Results.Ok(new { success = true, data = bots });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Gets detailed information about a specific bot.
    /// </summary>
    private static async Task<IResult> GetBotDetails(string teamName, BotDashboardService dashboardService)
    {
        try
        {
            var bot = await dashboardService.GetBotDetailsAsync(teamName);
            return Results.Ok(new { success = true, data = bot });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the version history for a bot.
    /// </summary>
    private static async Task<IResult> GetBotHistory(string teamName, BotDashboardService dashboardService)
    {
        try
        {
            var history = await dashboardService.GetBotVersionHistoryAsync(teamName);
            return Results.Ok(new { success = true, data = history });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Gets compilation errors for a bot.
    /// </summary>
    private static async Task<IResult> GetBotErrors(string teamName, BotDashboardService dashboardService)
    {
        try
        {
            var bot = await dashboardService.GetBotDetailsAsync(teamName);
            var hasErrors = !string.IsNullOrEmpty(bot.CompilationError);
            
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    hasErrors = hasErrors,
                    errors = hasErrors ? new[] { bot.CompilationError } : Array.Empty<string>(),
                    details = bot.CompilationError ?? "No errors"
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Searches for bots by team name.
    /// </summary>
    private static async Task<IResult> SearchBots(string searchTerm, BotDashboardService dashboardService)
    {
        try
        {
            var bots = await dashboardService.SearchBotsAsync(searchTerm);
            return Results.Ok(new { success = true, data = bots });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Filters bots by validation status.
    /// </summary>
    private static async Task<IResult> FilterBotsByStatus(string status, BotDashboardService dashboardService)
    {
        try
        {
            if (!Enum.TryParse<ValidationStatus>(status, ignoreCase: true, out var validationStatus))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = $"Invalid status. Must be one of: Valid, Invalid, Pending, ValidationInProgress"
                });
            }

            var bots = await dashboardService.FilterByStatusAsync(validationStatus);
            return Results.Ok(new { success = true, data = bots });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Validates a bot submission (re-validates).
    /// </summary>
    private static async Task<IResult> ValidateBot(string teamName, BotDashboardService dashboardService)
    {
        try
        {
            var result = await dashboardService.ValidateBotAsync(teamName);
            
            return Results.Ok(new
            {
                success = true,
                data = result,
                message = $"Bot {teamName} validation completed with status: {result.Status}"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new
            {
                success = false,
                message = $"Validation failed for bot {teamName}",
                errors = new[] { ex.Message }
            });
        }
    }
}
