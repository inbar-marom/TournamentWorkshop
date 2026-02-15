namespace TournamentEngine.Api.Endpoints;

using Models;
using Services;
using Microsoft.AspNetCore.Builder;
using System.Text.RegularExpressions;
using System.Linq;

/// <summary>
/// API endpoints for bot submission, listing, and management
/// Maps endpoints to BotStorageService methods
/// </summary>
public static class BotEndpoints
{
    public static void MapBotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bots")
            .WithName("Bots");

        group.MapPost("/submit", SubmitBot)
            .WithName("SubmitBot");

        group.MapPost("/submit-batch", SubmitBatch)
            .WithName("SubmitBatch");

        group.MapGet("/list", ListBots)
            .WithName("ListBots");

        group.MapDelete("/{teamName}", DeleteBot)
            .WithName("DeleteBot");

        group.MapPost("/pause", PauseSubmissions)
            .WithName("PauseSubmissions");

        group.MapPost("/resume", ResumeSubmissions)
            .WithName("ResumeSubmissions");

        group.MapGet("/pause-status", GetPauseStatus)
            .WithName("GetPauseStatus");
    }

    /// <summary>
    /// POST /api/bots/submit - Submit a single bot
    /// </summary>
    private static async Task<IResult> SubmitBot(
        BotSubmissionRequest request,
        BotStorageService botStorage,
        ILogger<Program> logger)
    {
        logger.LogInformation("Received bot submission for team {TeamName} with {FileCount} files",
            request.TeamName, request.Files?.Count ?? 0);

        // Validate request
        if (request == null || string.IsNullOrWhiteSpace(request.TeamName))
            return Results.BadRequest(new BotSubmissionResult
            {
                Success = false,
                Message = "Team name is required",
                Errors = new() { "TeamName cannot be empty" }
            });

        if (request.Files == null || request.Files.Count == 0)
            return Results.BadRequest(new BotSubmissionResult
            {
                Success = false,
                Message = "At least one file is required",
                Errors = new() { "Files collection is empty" }
            });

        // Validate team name
        if (!IsValidTeamName(request.TeamName))
            return Results.BadRequest(new BotSubmissionResult
            {
                Success = false,
                TeamName = request.TeamName,
                Message = "Invalid team name",
                Errors = new() { "Team name must contain only alphanumeric characters, hyphens, and underscores" }
            });

        // Validate file sizes
        var maxFileSize = 50_000; // 50KB
        var maxTotalSize = 200_000; // 200KB

        foreach (var file in request.Files)
        {
            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            if (fileSize > maxFileSize)
            {
                logger.LogWarning("File {FileName} for team {TeamName} exceeds size limit: {Size} > {Max}",
                    file.FileName, request.TeamName, fileSize, maxFileSize);
                return Results.Json(new BotSubmissionResult
                {
                    Success = false,
                    TeamName = request.TeamName,
                    Message = $"File {file.FileName} exceeds maximum size of 50KB",
                    Errors = new() { $"File {file.FileName} is too large" }
                }, statusCode: StatusCodes.Status413PayloadTooLarge);
            }
        }

        var totalSize = request.Files.Sum(f => System.Text.Encoding.UTF8.GetByteCount(f.Code));
        if (totalSize > maxTotalSize)
        {
            logger.LogWarning("Total submission size for team {TeamName} exceeds limit: {Size} > {Max}",
                request.TeamName, totalSize, maxTotalSize);
            return Results.Json(new BotSubmissionResult
            {
                Success = false,
                TeamName = request.TeamName,
                Message = "Total submission size exceeds maximum of 200KB",
                Errors = new() { "Submitted files are too large in total" }
            }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        // Check for duplicate filenames
        var fileNames = request.Files.Select(f => f.FileName).ToList();
        if (fileNames.Distinct().Count() != fileNames.Count)
        {
            logger.LogWarning("Duplicate file names in submission for team {TeamName}", request.TeamName);
            return Results.BadRequest(new BotSubmissionResult
            {
                Success = false,
                TeamName = request.TeamName,
                Message = "Duplicate file names detected",
                Errors = new() { "File names must be unique" }
            });
        }

        // Attempt to store bot
        var result = await botStorage.StoreBotAsync(request);

        if (!result.Success)
        {
            if (result.Message.Contains("already exists") && !request.Overwrite)
            {
                return Results.Conflict(result);
            }
            return Results.BadRequest(result);
        }

        logger.LogInformation("Bot submitted successfully for team {TeamName}: {SubmissionId}",
            request.TeamName, result.SubmissionId);
        return Results.Ok(result);
    }

    /// <summary>
    /// POST /api/bots/submit-batch - Submit multiple bots at once
    /// </summary>
    private static async Task<IResult> SubmitBatch(
        BatchSubmissionRequest batchRequest,
        BotStorageService botStorage,
        ILogger<Program> logger)
    {
        logger.LogInformation("Received batch submission with {BotCount} bots", batchRequest.Bots?.Count ?? 0);

        if (batchRequest == null || batchRequest.Bots == null || batchRequest.Bots.Count == 0)
            return Results.BadRequest(new BatchSubmissionResponse
            {
                SuccessCount = 0,
                FailureCount = 1,
                Results = new() { new BotSubmissionResult
                {
                    Success = false,
                    Message = "At least one bot is required",
                    Errors = new() { "Bots list cannot be empty" }
                }}
            });

        var responses = new List<BotSubmissionResult>();
        var successCount = 0;
        var failureCount = 0;

        var tasks = batchRequest.Bots.Select(async botRequest =>
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(botRequest.TeamName) || botRequest.Files == null || botRequest.Files.Count == 0)
            {
                failureCount++;
                responses.Add(new BotSubmissionResult
                {
                    Success = false,
                    TeamName = botRequest.TeamName,
                    Message = "Invalid submission",
                    Errors = new() { "Team name and files are required" }
                });
                return;
            }

            // Attempt to store
            var result = await botStorage.StoreBotAsync(botRequest);
            if (result.Success)
                successCount++;
            else
                failureCount++;

            responses.Add(result);
            logger.LogInformation("Batch submission processed for team {TeamName}: Success={Success}",
                botRequest.TeamName, result.Success);
        });

        await Task.WhenAll(tasks);

        return Results.Ok(new BatchSubmissionResponse
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            Results = responses
        });
    }

    /// <summary>
    /// GET /api/bots/list - List all submitted bots with metadata
    /// </summary>
    private static IResult ListBots(BotStorageService botStorage, ILogger<Program> logger)
    {
        logger.LogInformation("Listing all submitted bots");
        var submissions = botStorage.GetAllSubmissions();
        logger.LogInformation("Found {BotCount} submitted bots", submissions.Count);

        return Results.Ok(new ListBotsResponse
        {
            Bots = submissions
        });
    }

    /// <summary>
    /// DELETE /api/bots/{teamName} - Remove a submitted bot
    /// </summary>
    private static async Task<IResult> DeleteBot(
        string teamName,
        BotStorageService botStorage,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return Results.BadRequest(new { success = false, message = "Team name is required" });

        logger.LogInformation("Delete request for bot team {TeamName}", teamName);

        var success = await botStorage.DeleteBotAsync(teamName);

        if (!success)
        {
            logger.LogWarning("Failed to delete bot for team {TeamName} (not found or error)", teamName);
            return Results.NotFound(new { success = false, message = $"Bot for team {teamName} not found" });
        }

        logger.LogInformation("Bot deleted successfully for team {TeamName}", teamName);
        return Results.Ok(new { success = true, message = $"Bot {teamName} deleted successfully" });
    }

    /// <summary>
    /// Validate team name format (alphanumeric, hyphens, underscores only)
    /// </summary>
    private static bool IsValidTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return false;

        return Regex.IsMatch(teamName, @"^[a-zA-Z0-9_-]+$");
    }

    private static IResult PauseSubmissions(BotStorageService botStorage, ILogger<Program> logger)
    {
        botStorage.SetPauseState(true);
        logger.LogInformation("Bot submissions have been paused.");
        return Results.Ok(new { success = true, message = "Bot submissions paused." });
    }

    private static IResult ResumeSubmissions(BotStorageService botStorage, ILogger<Program> logger)
    {
        botStorage.SetPauseState(false);
        logger.LogInformation("Bot submissions have been resumed.");
        return Results.Ok(new { success = true, message = "Bot submissions resumed." });
    }

    private static IResult GetPauseStatus(BotStorageService botStorage)
    {
        var isPaused = botStorage.IsPaused();
        return Results.Ok(new { success = true, isPaused });
    }
}
