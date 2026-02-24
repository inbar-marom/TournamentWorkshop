using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Endpoints;

/// <summary>
/// Tournament data query endpoints for fetching filtered matches, groups, etc.
/// </summary>
public static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tournament")
            .WithName("Tournament Data")
            .WithOpenApi();

        group.MapGet("/matches", GetMatches)
            .WithName("GetMatches")
            .WithOpenApi();

        group.MapGet("/groups/{eventName}", GetGroupsByEvent)
            .WithName("GetGroupsByEvent")
            .WithOpenApi();
    }

    /// <summary>
    /// GET /api/tournament/matches?eventName={eventName}&groupLabel={groupLabel}
    /// Returns matches filtered by event and/or group
    /// </summary>
    private static async Task<IResult> GetMatches(
        StateManagerService stateManager,
        string? eventName = null,
        string? groupLabel = null)
    {
        try
        {
            List<RecentMatchDto> matches;
            
            // If both event and group specified, get matches for that specific group
            if (!string.IsNullOrEmpty(eventName) && !string.IsNullOrEmpty(groupLabel))
            {
                matches = stateManager.GetMatchesByEventAndGroup(eventName, groupLabel);
            }
            // If only event specified, get all matches for that event
            else if (!string.IsNullOrEmpty(eventName))
            {
                matches = stateManager.GetMatchesByEvent(eventName);
            }
            // Otherwise get all matches
            else
            {
                matches = stateManager.GetAllMatches();
            }

            return Results.Ok(matches);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving matches: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/tournament/groups/{eventName}
    /// Returns group standings for a specific event with full metadata
    /// Falls back to dashboard state groups if no matches tracked yet
    /// </summary>
    private static async Task<IResult> GetGroupsByEvent(
        StateManagerService stateManager,
        string eventName)
    {
        try
        {
            // Get matches for this event
            var eventMatches = stateManager.GetMatchesByEvent(eventName);

            // If we have matches, extract groups from them
            if (eventMatches.Count > 0)
            {
                var groups = eventMatches
                    .GroupBy(m => m.GroupLabel)
                    .Select(g => new GroupInfoDto
                    {
                        GroupId = g.First().GroupId ?? g.Key,
                        GroupLabel = g.Key ?? "",
                        EventId = g.First().EventId ?? "",
                        EventName = eventName,
                        MatchCount = g.Count()
                    })
                    .OrderBy(g => g.GroupLabel)
                    .ToList();

                return Results.Ok(groups);
            }

            // If no matches yet, return groups from the dashboard state
            // This handles the case where tournament just started but no matches completed yet
            var state = await stateManager.GetCurrentStateAsync();
            if (state?.GroupStandings != null && state.GroupStandings.Count > 0)
            {
                var groupsFromState = state.GroupStandings
                    .Where(g => (g.EventName ?? string.Empty).Equals(eventName, StringComparison.OrdinalIgnoreCase))
                    .Select(g => new GroupInfoDto
                    {
                        GroupId = g.GroupId,
                        GroupLabel = g.GroupName,
                        EventId = g.EventId,
                        EventName = g.EventName ?? eventName,
                        MatchCount = g.Rankings?.Count ?? 0
                    })
                    .OrderBy(g => g.GroupLabel)
                    .ToList();

                if (groupsFromState.Count > 0)
                {
                    return Results.Ok(groupsFromState);
                }
            }

            // No groups found
            return Results.Ok(new List<GroupInfoDto>());
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving groups for event: {ex.Message}");
        }
    }
}
