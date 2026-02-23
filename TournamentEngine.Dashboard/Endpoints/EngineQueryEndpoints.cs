using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// API endpoints for querying the running tournament engine's state.
/// These endpoints retrieve live data from tournaments being executed.
/// </summary>
public static class EngineQueryEndpoints
{
    public static void MapEngineQueryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/engine")
            .WithName("Tournament Engine Query")
            .WithOpenApi();

        group.MapGet("/status", GetTournamentStatus)
            .WithName("GetTournamentStatus")
            .WithOpenApi()
            .Produces<DashboardStateDto>();

        group.MapGet("/matches", GetEngineMatches)
            .WithName("GetEngineMatches")
            .WithOpenApi()
            .Produces<List<RecentMatchDto>>();

        group.MapGet("/matches/event/{eventName}", GetEngineMatchesByEvent)
            .WithName("GetEngineMatchesByEvent")
            .WithOpenApi()
            .Produces<List<RecentMatchDto>>();

        group.MapGet("/groups/{eventName}", GetEngineGroupsByEvent)
            .WithName("GetEngineGroupsByEvent")
            .WithOpenApi()
            .Produces<List<GroupDto>>();
    }

    /// <summary>
    /// GET /api/engine/status
    /// Returns the current tournament status summary from the running engine
    /// </summary>
    private static async Task<IResult> GetTournamentStatus(
        TournamentEngineQueryService engineQuery)
    {
        try
        {
            var state = await engineQuery.GetCurrentTournamentStateAsync();
            return Results.Ok(state);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving tournament status: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/engine/matches
    /// Returns all matches from the running tournament
    /// </summary>
    private static async Task<IResult> GetEngineMatches(
        TournamentEngineQueryService engineQuery)
    {
        try
        {
            var matches = await engineQuery.GetAllMatchesAsync();
            return Results.Ok(matches);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving matches: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/engine/matches/event/{eventName}
    /// Returns matches filtered by event name (game type)
    /// </summary>
    private static async Task<IResult> GetEngineMatchesByEvent(
        TournamentEngineQueryService engineQuery,
        string eventName)
    {
        try
        {
            var matches = await engineQuery.GetMatchesByEventAsync(eventName);
            return Results.Ok(matches);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving event matches: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/engine/groups/{eventName}
    /// Returns group standings for a specific event from the running tournament
    /// </summary>
    private static async Task<IResult> GetEngineGroupsByEvent(
        TournamentEngineQueryService engineQuery,
        string eventName)
    {
        try
        {
            var groups = await engineQuery.GetGroupStandingsByEventAsync(eventName);
            return Results.Ok(groups);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving group standings: {ex.Message}");
        }
    }
}
