using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// API endpoints for querying tournament execution data.
/// Provides access to current tournament status, events, matches, groups, and leaderboards.
/// Single source of truth: StateManagerService
/// </summary>
public static class TournamentQueryEndpoints
{
    public static void MapTournamentQueryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tournament-engine")
            .WithName("Tournament Execution Data");

        // GET endpoints for querying tournament state
        group.MapGet("/status", GetTournamentStatus)
            .WithName("GetTournamentStatus")
            .WithDescription("Get current tournament status")
            .Produces<DashboardStateDto>();

        group.MapGet("/events", GetTournamentEvents)
            .WithName("GetTournamentEvents")
            .WithDescription("Get all tournament events with status and champion when completed")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/leaders", GetOverallLeaders)
            .WithName("GetOverallLeaders")
            .WithDescription("Get overall tournament leaderboard")
            .Produces<List<TeamStandingDto>>();

        group.MapGet("/groups/{eventName}", GetEngineGroupsByEvent)
            .WithName("GetEngineGroupsByEvent")
            .WithDescription("Get all groups for a specific event")
            .Produces<List<GroupDto>>();

        group.MapGet("/groups/{eventName}/{groupLabel}", GetEngineGroupDetails)
            .WithName("GetEngineGroupDetails")
            .WithDescription("Get group standings and recent matches for a specific group")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/connection", GetTournamentConnection)
            .WithName("GetTournamentConnection")
            .WithDescription("Check if tournament is connected and active")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/matches", GetEngineMatches)
            .WithName("GetEngineMatches")
            .WithDescription("Get all matches with optional filtering")
            .Produces<List<RecentMatchDto>>();

        group.MapGet("/matches/event/{eventName}", GetEngineMatchesByEvent)
            .WithName("GetEngineMatchesByEvent")
            .WithDescription("Get matches for a specific event")
            .Produces<List<RecentMatchDto>>();

        // POST endpoint for tournament to send match results
        group.MapPost("/match-result", ReceiveMatchResult)
            .WithName("ReceiveMatchResult")
            .WithDescription("Receive match result from tournament process")
            .Accepts<RecentMatchDto>("application/json")
            .Produces(StatusCodes.Status200OK);

        // POST endpoint to clear matches (for testing)
        group.MapPost("/clear", ClearMatches)
            .WithName("ClearMatches")
            .WithDescription("Clear all stored matches (testing only)")
            .Produces(StatusCodes.Status200OK);
    }

    /// <summary>
    /// GET /api/tournament-engine/status
    /// Returns the current tournament status including start time, current status, current event.
    /// </summary>
    private static async Task<IResult> GetTournamentStatus(StateManagerService stateManager, ILogger<StateManagerService> logger)
    {
        try
        {
            var state = await stateManager.GetCurrentStateAsync();
            var currentEventName = state.CurrentEvent?.GameType.ToString() 
                ?? state.TournamentState?.Steps?.FirstOrDefault(s => s.StepIndex == state.TournamentState.CurrentStepIndex)?.GameType.ToString() 
                ?? "Unknown";
            
            return Results.Ok(new
            {
                status = state.Status.ToString(),
                message = state.Message,
                tournamentName = state.TournamentName,
                scheduledStartTime = state.ScheduledStartTime,
                currentEventIndex = state.TournamentProgress?.CurrentEventIndex ?? state.TournamentState?.CurrentStepIndex ?? -1,
                currentEventName = currentEventName,
                totalEvents = state.TournamentState?.Steps?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tournament status");
            return Results.Ok(new 
            { 
                status = nameof(TournamentStatus.NotStarted),
                message = "Tournament not initialized",
                currentEventIndex = -1,
                currentEventName = "Unknown",
                totalEvents = 0
            });
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/events
    /// Returns all tournament events with status (Pending, InProgress, Completed) and champion when completed.
    /// </summary>
    private static async Task<IResult> GetTournamentEvents(StateManagerService stateManager, ILogger<StateManagerService> logger)
    {
        try
        {
            var state = await stateManager.GetCurrentStateAsync();
            if (state.TournamentState?.Steps == null || state.TournamentState.Steps.Count == 0)
            {
                logger.LogWarning("GetTournamentEvents: No steps available. TournamentState null={TsNull}, Steps null={StepsNull}",
                    state.TournamentState == null, state.TournamentState?.Steps == null);
                return Results.Json(Array.Empty<object>());
            }

            var events = state.TournamentState.Steps
                .OrderBy(s => s.StepIndex)
                .Select(step => new
                {
                    stepIndex = step.StepIndex,
                    eventName = step.GameType.ToString(),
                    status = step.Status.ToString(),
                    champion = step.Status == EventStepStatus.Completed ? step.WinnerName : null,
                    eventId = step.EventId
                })
                .ToList();

            logger.LogInformation("GetTournamentEvents: Returning {Count} events", events.Count);
            return Results.Json(events);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tournament events");
            return Results.Json(Array.Empty<object>());
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/leaders
    /// Returns overall tournament leaderboard across all events.
    /// </summary>
    private static async Task<IResult> GetOverallLeaders(StateManagerService stateManager, ILogger<StateManagerService> logger)
    {
        try
        {
            var state = await stateManager.GetCurrentStateAsync();
            return Results.Ok(state.OverallLeaderboard ?? new List<TeamStandingDto>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving overall leaders");
            return Results.Ok(new List<TeamStandingDto>());
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/groups/{eventName}
    /// Returns all groups for a specific event with their current standings.
    /// </summary>
    private static async Task<IResult> GetEngineGroupsByEvent(StateManagerService stateManager, string eventName, ILogger<StateManagerService> logger)
    {
        try
        {
            var groups = stateManager.GetGroupsByEvent(eventName);
            return Results.Ok(groups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving groups for event {EventName}", eventName);
            return Results.Ok(new List<GroupDto>());
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/groups/{eventName}/{groupLabel}
    /// Returns a specific group's standing and recent matches.
    /// </summary>
    private static async Task<IResult> GetEngineGroupDetails(
        StateManagerService stateManager, 
        string eventName, 
        string groupLabel,
        ILogger<StateManagerService> logger)
    {
        try
        {
            var groups = stateManager.GetGroupsByEvent(eventName);
            var group = groups.FirstOrDefault(g => (g.GroupName ?? string.Empty).Equals(groupLabel, StringComparison.OrdinalIgnoreCase));
            var recentMatches = stateManager.GetMatchesByEventAndGroup(eventName, groupLabel);

            return Results.Json(new
            {
                eventName = eventName,
                groupLabel = groupLabel,
                groupStanding = group?.Rankings ?? new List<BotRankingDto>(),
                recentMatches = recentMatches
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving group details for {EventName}/{GroupLabel}", eventName, groupLabel);
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/connection
    /// Returns whether tournament is connected (has active state/recent activity).
    /// </summary>
    private static async Task<IResult> GetTournamentConnection(StateManagerService stateManager, ILogger<StateManagerService> logger)
    {
        try
        {
            var state = await stateManager.GetCurrentStateAsync();
            var lastActivity = stateManager.GetLatestActivityUtc();
            var connected = state.Status != TournamentStatus.NotStarted || (DateTime.UtcNow - lastActivity) <= TimeSpan.FromMinutes(5);

            return Results.Json(new
            {
                connected = connected,
                connectionStatus = connected ? "Connected" : "Disconnected",
                lastActivityUtc = lastActivity,
                tournamentStatus = state.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking tournament connection");
            return Results.Json(new
            {
                connected = false,
                connectionStatus = "Disconnected",
                lastActivityUtc = DateTime.UtcNow,
                tournamentStatus = "Error"
            });
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/matches
    /// Returns all matches with optional event and group filtering via query parameters.
    /// </summary>
    private static async Task<IResult> GetEngineMatches(
        StateManagerService stateManager,
        [FromQuery] string? eventName,
        [FromQuery] string? groupLabel,
        ILogger<StateManagerService> logger)
    {
        try
        {
            List<RecentMatchDto> matches;

            if (!string.IsNullOrWhiteSpace(eventName) && !string.IsNullOrWhiteSpace(groupLabel))
            {
                matches = stateManager.GetMatchesByEventAndGroup(eventName, groupLabel);
            }
            else if (!string.IsNullOrWhiteSpace(eventName))
            {
                matches = stateManager.GetMatchesByEvent(eventName);
            }
            else
            {
                matches = stateManager.GetAllMatches();
            }

            return Results.Ok(matches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving matches");
            return Results.Ok(new List<RecentMatchDto>());
        }
    }

    /// <summary>
    /// GET /api/tournament-engine/matches/event/{eventName}
    /// Returns matches filtered by event name.
    /// </summary>
    private static async Task<IResult> GetEngineMatchesByEvent(
        StateManagerService stateManager,
        string eventName,
        ILogger<StateManagerService> logger)
    {
        try
        {
            var matches = stateManager.GetMatchesByEvent(eventName);
            return Results.Ok(matches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving event matches for {EventName}", eventName);
            return Results.Ok(new List<RecentMatchDto>());
        }
    }

    /// <summary>
    /// POST /api/tournament-engine/match-result
    /// Tournament process POSTs match results here as they complete.
    /// </summary>
    private static async Task<IResult> ReceiveMatchResult(
        RecentMatchDto match,
        StateManagerService stateManager,
        ILogger<StateManagerService> logger)
    {
        try
        {
            stateManager.AddRecentMatch(new MatchCompletedDto
            {
                MatchId = match.MatchId,
                TournamentId = match.TournamentId,
                TournamentName = match.TournamentName,
                EventId = match.EventId,
                EventName = match.EventName,
                Bot1Name = match.Bot1Name,
                Bot2Name = match.Bot2Name,
                Outcome = match.Outcome,
                WinnerName = match.WinnerName,
                Bot1Score = match.Bot1Score,
                Bot2Score = match.Bot2Score,
                CompletedAt = match.CompletedAt,
                GameType = match.GameType,
                GroupId = match.GroupId,
                GroupLabel = match.GroupLabel
            });

            logger.LogInformation("Received match result: {Bot1} vs {Bot2}", match.Bot1Name, match.Bot2Name);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving match result");
            return Results.Problem($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// POST /api/tournament-engine/clear
    /// Clears all stored matches and state (useful for testing/resetting).
    /// </summary>
    private static async Task<IResult> ClearMatches(StateManagerService stateManager, ILogger<StateManagerService> logger)
    {
        try
        {
            await stateManager.ClearStateAsync();
            logger.LogInformation("Cleared all tournament state");
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing state");
            return Results.Problem($"Error: {ex.Message}");
        }
    }
}
