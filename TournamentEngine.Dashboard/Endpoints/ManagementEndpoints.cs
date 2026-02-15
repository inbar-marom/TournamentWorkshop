using TournamentEngine.Dashboard.Services;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// Tournament management endpoints for operator controls.
/// Handles start, pause, resume, stop, clear, and rerun operations.
/// </summary>
public static class ManagementEndpoints
{
    public static void MapManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/manage")
            .WithName("Tournament Management")
            .WithOpenApi();

        group.MapGet("/status", GetManagementStatus)
            .WithName("GetManagementStatus")
            .WithOpenApi();

        group.MapPost("/start", StartTournament)
            .WithName("StartTournament")
            .WithOpenApi();

        group.MapPost("/pause", PauseTournament)
            .WithName("PauseTournament")
            .WithOpenApi();

        group.MapPost("/resume", ResumeTournament)
            .WithName("ResumeTournament")
            .WithOpenApi();

        group.MapPost("/stop", StopTournament)
            .WithName("StopTournament")
            .WithOpenApi();

        group.MapPost("/clear", ClearSubmissions)
            .WithName("ClearSubmissions")
            .WithOpenApi();

        group.MapPost("/rerun", RerunTournament)
            .WithName("RerunTournament")
            .WithOpenApi();

        group.MapGet("/readiness", CheckBotsReadiness)
            .WithName("CheckBotsReadiness")
            .WithOpenApi();

        group.MapGet("/state", GetDashboardState)
            .WithName("GetDashboardState")
            .WithOpenApi();
    }

    /// <summary>
    /// Get current management state.
    /// </summary>
    private static async Task<IResult> GetManagementStatus(TournamentManagementService service)
    {
        var state = await service.GetStateAsync();
        return Results.Ok(state);
    }

    /// <summary>
    /// Check if bots are ready to start a tournament.
    /// </summary>
    private static async Task<IResult> CheckBotsReadiness(TournamentManagementService service)
    {
        var (ready, message, botCount) = await service.CheckBotsReadyAsync();
        return Results.Ok(new { ready, message, botCount });
    }

    /// <summary>
    /// Start a new tournament.
    /// </summary>
    private static async Task<IResult> StartTournament(TournamentManagementService service)
    {
        var result = await service.StartAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Tournament started", state });
    }

    /// <summary>
    /// Pause the running tournament.
    /// </summary>
    private static async Task<IResult> PauseTournament(TournamentManagementService service)
    {
        var result = await service.PauseAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Tournament paused", state });
    }

    /// <summary>
    /// Resume a paused tournament.
    /// </summary>
    private static async Task<IResult> ResumeTournament(TournamentManagementService service)
    {
        var result = await service.ResumeAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Tournament resumed", state });
    }

    /// <summary>
    /// Stop the running tournament.
    /// </summary>
    private static async Task<IResult> StopTournament(TournamentManagementService service)
    {
        var result = await service.StopAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Tournament stopped", state });
    }

    /// <summary>
    /// Clear all bot submissions and reset to NotStarted state.
    /// </summary>
    private static async Task<IResult> ClearSubmissions(TournamentManagementService service)
    {
        var result = await service.ClearSubmissionsAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Submissions cleared", state });
    }

    /// <summary>
    /// Rerun the last tournament configuration.
    /// </summary>
    private static async Task<IResult> RerunTournament(TournamentManagementService service)
    {
        var result = await service.RerunAsync();
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.Message });
        }

        var state = await service.GetStateAsync();
        return Results.Ok(new { message = "Tournament re-run started", state });
    }

    /// <summary>
    /// Get current dashboard state for the main UI.
    /// </summary>
    private static async Task<IResult> GetDashboardState(StateManagerService stateManager)
    {
        var state = await stateManager.GetCurrentStateAsync();
        return Results.Ok(state);
    }}