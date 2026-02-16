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

        group.MapPost("/verify", VerifyBot)
            .WithName("VerifyBot");
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
        var maxTotalSize = 500_000; // 500KB

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
                Message = "Total submission size exceeds maximum of 500KB",
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

        // Run enhanced validation (same as verify endpoint)
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();
        ValidateAllFiles(request.Files, validationErrors, validationWarnings);

        if (validationErrors.Count > 0)
        {
            logger.LogWarning("Bot submission validation failed for team {TeamName}: {ErrorCount} errors", request.TeamName, validationErrors.Count);
            return Results.BadRequest(new BotSubmissionResult
            {
                Success = false,
                TeamName = request.TeamName,
                Message = "Bot validation failed",
                Errors = validationErrors
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

    /// <summary>
    /// Approved .NET namespaces for bot safety
    /// </summary>
    private static readonly HashSet<string> ApprovedNamespaces = new()
    {
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Numerics",
        "System.Threading",
        "System.Threading.Tasks",
        "System.IO",
        "System.Text.RegularExpressions",
        "System.Diagnostics",
        "TournamentEngine.Core.Common"
    };

    /// <summary>
    /// Shared validation logic for all bot file submissions
    /// Validates file types, content, approved libraries, and coding rules
    /// </summary>
    private static void ValidateAllFiles(List<BotFile> files, List<string> errors, List<string> warnings)
    {
        var hasCodeFile = false;
        
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Code))
            {
                errors.Add($"File {file.FileName} is empty");
                continue;
            }

            // C# file validation
            if (file.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                hasCodeFile = true;
                ValidateCSharpFile(file, errors, warnings);
            }
            // Allow documentation files - just skip validation
            else if (file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                     file.FileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
                     file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                // Documentation/verification files allowed - no validation needed
                continue;
            }
            else
            {
                warnings.Add($"File {file.FileName} has unknown extension. Only .cs files will be compiled. Documentation files (.md, .py, .txt) are allowed but ignored.");
            }
        }
        
        if (!hasCodeFile)
        {
            errors.Add("At least one .cs file is required");
        }
    }

    /// <summary>
    /// POST /api/bots/verify - Verify a bot before submission
    /// Enhanced validation including .NET library checks and syntax validation
    /// NOTE: Only C# (.cs) files are supported - Python is NOT supported
    /// </summary>
    private static async Task<IResult> VerifyBot(
        BotVerificationRequest request,
        BotStorageService botStorage,
        ILogger<Program> logger)
    {
        logger.LogInformation("Verifying bot for team {TeamName}", request.TeamName);

        if (string.IsNullOrWhiteSpace(request.TeamName))
            return Results.BadRequest(new BotVerificationResult
            {
                IsValid = false,
                Message = "Team name is required",
                Errors = new() { "TeamName cannot be empty" }
            });

        if (request.Files == null || request.Files.Count == 0)
            return Results.BadRequest(new BotVerificationResult
            {
                IsValid = false,
                Message = "At least one file is required",
                Errors = new() { "Files collection is empty" }
            });

        // Validate team name format
        if (!IsValidTeamName(request.TeamName))
            return Results.BadRequest(new BotVerificationResult
            {
                IsValid = false,
                Message = "Invalid team name format",
                Errors = new() { "Team name must contain only alphanumeric characters, hyphens, and underscores" }
            });

        // Validate file sizes (same limits as submission)
        var maxFileSize = 50_000;
        var maxTotalSize = 500_000;
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var file in request.Files)
        {
            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            if (fileSize > maxFileSize)
            {
                errors.Add($"File {file.FileName} exceeds maximum size of 50KB ({fileSize} bytes)");
            }
        }

        var totalSize = request.Files.Sum(f => System.Text.Encoding.UTF8.GetByteCount(f.Code));
        if (totalSize > maxTotalSize)
        {
            errors.Add($"Total submission size exceeds maximum of 500KB ({totalSize} bytes)");
        }

        // Check for duplicate filenames
        var fileNames = request.Files.Select(f => f.FileName).ToList();
        if (fileNames.Distinct().Count() != fileNames.Count)
        {
            errors.Add("Duplicate file names detected");
        }

        // Run enhanced validation
        ValidateAllFiles(request.Files, errors, warnings);

        // Game-type specific validation (if specified)
        if (request.GameType.HasValue)
        {
            ValidateForGameType(request.GameType.Value, request.Files, warnings);
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Bot verification failed for team {TeamName}: {ErrorCount} errors", request.TeamName, errors.Count);
            return Results.BadRequest(new BotVerificationResult
            {
                IsValid = false,
                Message = "Bot verification failed",
                Errors = errors,
                Warnings = warnings
            });
        }

        logger.LogInformation("Bot verification successful for team {TeamName}", request.TeamName);
        return Results.Ok(new BotVerificationResult
        {
            IsValid = true,
            Message = "Bot verification successful. Ready for submission.",
            Errors = new(),
            Warnings = warnings
        });
    }

    /// <summary>
    /// Validate C# file for approved libraries and .NET 8.0 compatibility
    /// Enforces: approved namespaces, no dangerous APIs, no unsafe code, double forward slashes at end of lines, proper structure
    /// </summary>
    private static void ValidateCSharpFile(BotFile file, List<string> errors, List<string> warnings)
    {
        var code = file.Code;

        // Check for basic C# structure
        if (!code.Contains("class "))
        {
            warnings.Add($"File {file.FileName} appears to be C# but has no class definitions");
        }

        // CODING RULE 1: Check for double semicolons (;;) - not allowed
        if (code.Contains(";;"))
        {
            errors.Add($"File {file.FileName} contains double semicolons (;;) which are not allowed");
        }
        
        // CODING RULE 2: Check for double forward slashes at end of lines (required pattern)
        // Note: This validates that statements end with ; // (semicolon space double-slash)
        // Look for lines ending with semicolon but NOT followed by space and //
        var lines = code.Split('\n');
        var invalidLines = 0;
        
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r', '\n', ' ', '\t');
            
            // Skip empty lines, using directives, namespace declarations, braces, and for loop headers
            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.TrimStart().StartsWith("using ") ||
                trimmed.TrimStart().StartsWith("namespace ") ||
                trimmed.Contains("for (") ||
                trimmed.Trim() == "{" ||
                trimmed.Trim() == "}")
            {
                continue;
            }
            
            // If line ends with semicolon, it must be followed by space and //
            if (trimmed.EndsWith(";") && !trimmed.EndsWith("; //"))
            {
                invalidLines++;
            }
        }
        
        if (invalidLines > 0)
        {
            errors.Add($"File {file.FileName} violates double forward slash rule. All statement lines must end with '; //' (except using directives, namespace declarations, and for loops)");
        }

        // Check for unsafe code blocks (not allowed)
        if (code.Contains("unsafe "))
        {
            errors.Add($"File {file.FileName} contains 'unsafe' code blocks which are not allowed");
        }

        // Check for dangerous patterns
        var dangerousPatterns = new[]
        {
            "System.Reflection.Assembly.Load",
            "System.Runtime.InteropServices",
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Diagnostics.Process.Start",
            "File.Delete",
            "Directory.Delete",
            "Environment.Exit"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (code.Contains(pattern))
            {
                errors.Add($"File {file.FileName} contains disallowed pattern: {pattern}");
            }
        }

        // Validate using directives (namespaces)
        var usingMatches = Regex.Matches(code, @"using\s+([a-zA-Z0-9_.]+)\s*;");
        foreach (Match match in usingMatches)
        {
            var namespaceName = match.Groups[1].Value;
            
            // Only exact matches are allowed - no hierarchical namespace approval
            // This prevents System.Net.Http from being approved just because "System" is approved
            if (!ApprovedNamespaces.Contains(namespaceName))
            {
                errors.Add($"File {file.FileName} uses unapproved namespace: {namespaceName}. Only approved .NET libraries are allowed: {string.Join(", ", ApprovedNamespaces)}");
            }
        }

        // Check for TournamentEngine.Core.Common namespace (required for IBot)
        if (!code.Contains("using TournamentEngine.Core.Common"))
        {
            warnings.Add($"File {file.FileName} should include 'using TournamentEngine.Core.Common;' to implement IBot interface");
        }

        // Check for .NET 8.0 target framework hints (strict enforcement)
        if (code.Contains("<TargetFramework>") && !code.Contains("<TargetFramework>net8.0</TargetFramework>"))
        {
            errors.Add($"File {file.FileName} targets a framework other than net8.0. Must target .NET 8.0");
        }

        // Check for required IBot implementation
        if (!code.Contains(": IBot"))
        {
            warnings.Add($"File {file.FileName} doesn't appear to implement IBot interface");
        }

        // Check for required method signatures (all 4 game methods)
        var requiredMethods = new[] { "MakeMove", "AllocateTroops", "MakePenaltyDecision", "MakeSecurityMove" };
        foreach (var method in requiredMethods)
        {
            if (!code.Contains(method))
            {
                warnings.Add($"File {file.FileName} doesn't appear to implement required method: {method}");
            }
        }
    }

    /// <summary>
    /// Validate bot files for specific game type requirements
    /// </summary>
    private static void ValidateForGameType(Core.Common.GameType gameType, List<BotFile> files, List<string> warnings)
    {
        var allCode = string.Join("\n", files.Select(f => f.Code));

        switch (gameType)
        {
            case Core.Common.GameType.RPSLS:
                if (!allCode.Contains("Rock") && !allCode.Contains("Paper"))
                {
                    warnings.Add("RPSLS bot should handle Rock, Paper, Scissors, Lizard, Spock moves");
                }
                break;

            case Core.Common.GameType.ColonelBlotto:
                if (!allCode.Contains("troops") && !allCode.Contains("Troops") && !allCode.Contains("allocate"))
                {
                    warnings.Add("Colonel Blotto bot should handle troop allocation logic");
                }
                break;

            case Core.Common.GameType.PenaltyKicks:
                if (!allCode.Contains("direction") && !allCode.Contains("Direction"))
                {
                    warnings.Add("Penalty Kicks bot should handle direction selection");
                }
                break;

            case Core.Common.GameType.SecurityGame:
                if (!allCode.Contains("target") && !allCode.Contains("Target"))
                {
                    warnings.Add("Security Game bot should handle target selection logic");
                }
                break;
        }
    }
}
