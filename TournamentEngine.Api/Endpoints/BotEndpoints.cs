namespace TournamentEngine.Api.Endpoints;

using Models;
using Services;
using Microsoft.AspNetCore.Builder;
using System.Text.RegularExpressions;
using System.Linq;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.Common;
using TournamentEngine.Api.Utilities;

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
        BotLoader botLoader,
        DevelopmentSettingsService devSettings,
        ILogger<Program> logger)
    {
        var normalizationWarnings = new List<string>();
        var normalizedRequest = new BotSubmissionRequest
        {
            TeamName = request.TeamName,
            Overwrite = request.Overwrite,
            Files = BotFilePathNormalizer.NormalizeAndEnsureUnique(request.Files, request.TeamName, normalizationWarnings)
        };

        logger.LogInformation("Received bot submission for team {TeamName} with {FileCount} files",
            normalizedRequest.TeamName, normalizedRequest.Files?.Count ?? 0);

        // Check if verification is bypassed
        bool bypassEnabled = devSettings.IsVerificationBypassed();
        if (bypassEnabled)
        {
            logger.LogWarning("⚠️ VERIFICATION BYPASSED - Accepting bot without validation for team {TeamName}", normalizedRequest.TeamName);
            
            // Attempt to store bot without validation
            var bypassResult = await botStorage.StoreBotAsync(normalizedRequest);

            if (!bypassResult.Success)
            {
                if (bypassResult.Message.Contains("already exists") && !request.Overwrite)
                {
                    return Results.Conflict(bypassResult);
                }
                return Results.BadRequest(bypassResult);
            }

            logger.LogInformation("Bot submitted successfully (verification bypassed) for team {TeamName}: {SubmissionId}",
                normalizedRequest.TeamName, bypassResult.SubmissionId);
            return Results.Ok(bypassResult);
        }

        // Validate and compile bot
        var validationResult = await ValidateAndCompileBot(
            request.TeamName,
            normalizedRequest.Files,
            null, // No game type specified
            botLoader,
            logger);

        if (!validationResult.IsValid)
        {
            logger.LogWarning("Bot submission validation/compilation failed for team {TeamName}: {ErrorCount} errors",
                request.TeamName, validationResult.Errors.Count);
            return validationResult.PayloadTooLarge
                ? Results.Json(new BotSubmissionResult
                {
                    Success = false,
                    TeamName = normalizedRequest.TeamName,
                    Message = validationResult.Message,
                    Errors = validationResult.Errors
                }, statusCode: StatusCodes.Status413PayloadTooLarge)
                : Results.BadRequest(new BotSubmissionResult
                {
                    Success = false,
                    TeamName = normalizedRequest.TeamName,
                    Message = validationResult.Message,
                    Errors = validationResult.Errors
                });
        }

        if (normalizationWarnings.Count > 0)
        {
            validationResult.Warnings.AddRange(normalizationWarnings);
        }

        // Attempt to store bot
        var result = await botStorage.StoreBotAsync(normalizedRequest);

        if (!result.Success)
        {
            if (result.Message.Contains("already exists") && !request.Overwrite)
            {
                return Results.Conflict(result);
            }
            return Results.BadRequest(result);
        }

        logger.LogInformation("Bot submitted successfully for team {TeamName}: {SubmissionId}",
            normalizedRequest.TeamName, result.SubmissionId);
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
            var normalizedBotRequest = new BotSubmissionRequest
            {
                TeamName = botRequest.TeamName,
                Overwrite = botRequest.Overwrite,
                Files = BotFilePathNormalizer.NormalizeAndEnsureUnique(botRequest.Files, botRequest.TeamName)
            };

            // Basic validation
            if (string.IsNullOrWhiteSpace(normalizedBotRequest.TeamName) || normalizedBotRequest.Files == null || normalizedBotRequest.Files.Count == 0)
            {
                failureCount++;
                responses.Add(new BotSubmissionResult
                {
                    Success = false,
                    TeamName = normalizedBotRequest.TeamName,
                    Message = "Invalid submission",
                    Errors = new() { "Team name and files are required" }
                });
                return;
            }

            // Attempt to store
            var result = await botStorage.StoreBotAsync(normalizedBotRequest);
            if (result.Success)
                successCount++;
            else
                failureCount++;

            responses.Add(result);
            logger.LogInformation("Batch submission processed for team {TeamName}: Success={Success}",
                normalizedBotRequest.TeamName, result.Success);
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
    private static void ValidateAllFiles(List<BotFile> files, List<string> errors, List<string> warnings, ILogger<Program>? logger = null)
    {
        var hasCodeFile = false;
        var hasIBotImplementation = false;
        
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
                
                // Check if this file implements IBot
                var hasIBot = System.Text.RegularExpressions.Regex.IsMatch(file.Code, @":\s*IBot\b");
                
                if (hasIBot)
                {
                    hasIBotImplementation = true;
                }
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
        
        // Verify at least one file implements IBot interface
        if (hasCodeFile && !hasIBotImplementation)
        {
            errors.Add("No class implementing IBot found in bot code. At least one .cs file must contain a class declaration like 'class YourBot : IBot'");
        }
    }

    /// <summary>
    /// POST /api/bots/verify - Verify a bot before submission
    /// Enhanced validation including .NET library checks, syntax validation, and compilation test
    /// NOTE: Only C# (.cs) files are supported - Python is NOT supported
    /// </summary>
    private static async Task<IResult> VerifyBot(
        BotVerificationRequest request,
        BotStorageService botStorage,
        BotLoader botLoader,
        DevelopmentSettingsService devSettings,
        ILogger<Program> logger)
    {
        var normalizationWarnings = new List<string>();
        var normalizedFiles = BotFilePathNormalizer.NormalizeAndEnsureUnique(request.Files, request.TeamName, normalizationWarnings);

        logger.LogInformation("Verifying bot for team {TeamName}", request.TeamName);

        // Check if verification is bypassed
        bool bypassEnabled = devSettings.IsVerificationBypassed();
        if (bypassEnabled)
        {
            logger.LogWarning("⚠️ VERIFICATION BYPASSED - Accepting bot without validation for team {TeamName}", request.TeamName);
            return Results.Ok(new BotVerificationResult
            {
                Success = true,
                IsValid = true,
                Message = "Bot accepted (verification bypassed by development settings).",
                Errors = new(),
                Warnings = new() { "⚠️ Verification was bypassed - bot was not validated or compiled" }
            });
        }

        // Validate and compile bot
        var validationResult = await ValidateAndCompileBot(
            request.TeamName,
            normalizedFiles,
            request.GameType,
            botLoader,
            logger);

        if (normalizationWarnings.Count > 0)
        {
            validationResult.Warnings.AddRange(normalizationWarnings);
        }

        if (!validationResult.IsValid)
        {
            logger.LogWarning("Bot verification failed for team {TeamName}: {ErrorCount} errors",
                request.TeamName, validationResult.Errors.Count);
            return Results.BadRequest(new BotVerificationResult
            {
                Success = false,
                IsValid = false,
                Message = validationResult.Message,
                Errors = validationResult.Errors,
                Warnings = validationResult.Warnings
            });
        }

        logger.LogInformation("Bot verification successful for team {TeamName}", request.TeamName);
        return Results.Ok(new BotVerificationResult
        {
            Success = true,
            IsValid = true,
            Message = "Bot verification and compilation successful. Ready for submission.",
            Errors = new(),
            Warnings = validationResult.Warnings
        });
    }

    /// <summary>
    /// Shared validation and compilation logic for both submit and verify endpoints
    /// </summary>
    private static async Task<ValidationCompilationResult> ValidateAndCompileBot(
        string? teamName,
        List<BotFile>? files,
        GameType? gameType,
        BotLoader botLoader,
        ILogger<Program> logger)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Basic validation
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "Team name is required",
                Errors = new() { "TeamName cannot be empty" }
            };
        }

        if (files == null || files.Count == 0)
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "At least one file is required",
                Errors = new() { "Files collection is empty" }
            };
        }

        files = BotFilePathNormalizer.NormalizeAndEnsureUnique(files, teamName);

        // Validate team name format
        if (!IsValidTeamName(teamName))
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "Invalid team name format",
                Errors = new() { "Team name must contain only alphanumeric characters, hyphens, and underscores" }
            };
        }

        // Validate file sizes (same limits as submission)
        var maxFileSize = 50_000; // 50KB
        var maxTotalSize = 500_000; // 500KB

        foreach (var file in files)
        {
            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            if (fileSize > maxFileSize)
            {
                errors.Add($"File {file.FileName} exceeds maximum size of 50KB ({fileSize} bytes)");
            }
        }

        var totalSize = files.Sum(f => System.Text.Encoding.UTF8.GetByteCount(f.Code));
        if (totalSize > maxTotalSize)
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "Total submission size exceeds maximum of 500KB",
                Errors = new() { $"Total size ({totalSize} bytes) exceeds 500KB limit" },
                PayloadTooLarge = true
            };
        }

        // Check for duplicate filenames
        var fileNames = files.Select(f => f.FileName).ToList();
        if (fileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != fileNames.Count)
        {
            errors.Add("Duplicate file names detected");
        }

        // Run enhanced validation
        ValidateAllFiles(files, errors, warnings, logger);
        ValidateRequiredFiles(files, errors);

        // Game-type specific validation (if specified)
        if (gameType.HasValue)
        {
            ValidateForGameType(gameType.Value, files, warnings);
        }

        if (errors.Count > 0)
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "Bot validation failed",
                Errors = errors,
                Warnings = warnings
            };
        }

        // Test compilation
        var compilationResult = await CompileBot(teamName, files, botLoader, logger);
        
        if (!compilationResult.Success)
        {
            return new ValidationCompilationResult
            {
                IsValid = false,
                Message = "Bot compilation failed",
                Errors = compilationResult.Errors,
                Warnings = warnings
            };
        }

        // Add any compilation warnings
        if (compilationResult.Warnings.Count > 0)
        {
            warnings.AddRange(compilationResult.Warnings);
        }

        return new ValidationCompilationResult
        {
            IsValid = true,
            Message = "Bot validation and compilation successful",
            Errors = new(),
            Warnings = warnings
        };
    }

    /// <summary>
    /// Compile bot using BotLoader to verify it can be loaded
    /// </summary>
    private static async Task<CompilationResult> CompileBot(
        string teamName,
        List<BotFile> files,
        BotLoader botLoader,
        ILogger<Program> logger)
    {
        try
        {
            // Create temporary workspace structure that mimics real workspace
            // Structure: tempWorkspace/TournamentEngine.Api/bots/teamName/
            //            tempWorkspace/UserBot/UserBot.Core/
            var tempWorkspace = Path.Combine(Path.GetTempPath(), $"bot_workspace_{Guid.NewGuid()}");
            var tempBotDir = Path.Combine(tempWorkspace, "TournamentEngine.Api", "bots", teamName);
            Directory.CreateDirectory(tempBotDir);

            try
            {
                // Copy UserBot.Core files to temp workspace (required for bots using UserBot.Core namespace)
                var workspaceRoot = FindWorkspaceRoot();
                var userBotCorePath = Path.Combine(workspaceRoot, "UserBot", "UserBot.Core");
                
                if (Directory.Exists(userBotCorePath))
                {
                    var tempUserBotCore = Path.Combine(tempWorkspace, "UserBot", "UserBot.Core");
                    Directory.CreateDirectory(tempUserBotCore);
                    
                    var coreFiles = new[] { "IBot.cs", "GameState.cs", "GameType.cs" };
                    foreach (var fileName in coreFiles)
                    {
                        var sourceFile = Path.Combine(userBotCorePath, fileName);
                        if (File.Exists(sourceFile))
                        {
                            var destFile = Path.Combine(tempUserBotCore, fileName);
                            File.Copy(sourceFile, destFile);
                        }
                        else
                        {
                            logger.LogWarning("UserBot.Core file not found: {FilePath}", sourceFile);
                        }
                    }
                }
                else
                {
                    logger.LogWarning("UserBot.Core directory not found at: {Path}", userBotCorePath);
                }

                // Write all bot files to temp directory
                foreach (var file in files)
                {
                    var filePath = Path.Combine(tempBotDir, file.FileName);
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    await File.WriteAllTextAsync(filePath, file.Code);
                }

                // Try to load the bot
                var botInfo = await botLoader.LoadBotFromFolderAsync(tempBotDir, null, CancellationToken.None);

                if (!botInfo.IsValid)
                {
                    logger.LogWarning("Bot compilation failed for team {TeamName}: {Errors}",
                        teamName, string.Join("; ", botInfo.ValidationErrors));
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = botInfo.ValidationErrors.ToList()
                    };
                }

                logger.LogInformation("Bot compiled successfully for team {TeamName}", teamName);
                return new CompilationResult
                {
                    Success = true,
                    Errors = new(),
                    Warnings = new()
                };
            }
            finally
            {
                // Clean up temp workspace
                try
                {
                    if (Directory.Exists(tempWorkspace))
                    {
                        Directory.Delete(tempWorkspace, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to clean up temp workspace {TempWorkspace}", tempWorkspace);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during bot compilation for team {TeamName}", teamName);
            return new CompilationResult
            {
                Success = false,
                Errors = new() { $"Compilation exception: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Result of validation and compilation check
    /// </summary>
    private class ValidationCompilationResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = "";
        public List<string> Errors { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
        public bool PayloadTooLarge { get; init; }
    }

    /// <summary>
    /// Result of compilation check
    /// </summary>
    private class CompilationResult
    {
        public bool Success { get; init; }
        public List<string> Errors { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
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
            
            // If line ends with semicolon, it must be followed by //
            if (trimmed.EndsWith(";") && !trimmed.EndsWith("; //") && !trimmed.EndsWith(";//"))
            {
                invalidLines++;
            }
        }
        
        if (invalidLines > 0)
        {
            errors.Add($"File {file.FileName} violates double forward slash rule. All statement lines must end with ';//' or '; //' (except using directives, namespace declarations, and for loops)");
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
            
            // Allow user-defined namespaces (anything that doesn't start with "System" or "Microsoft")
            // Only validate .NET framework namespaces against approved list
            var isSystemNamespace = namespaceName.StartsWith("System") || namespaceName.StartsWith("Microsoft");
            
            if (isSystemNamespace && !ApprovedNamespaces.Contains(namespaceName))
            {
                errors.Add($"File {file.FileName} uses unapproved .NET namespace: {namespaceName}. Only approved .NET libraries are allowed: {string.Join(", ", ApprovedNamespaces)}");
            }
        }

        // Check for .NET 8.0 target framework hints (strict enforcement)
        if (code.Contains("<TargetFramework>") && !code.Contains("<TargetFramework>net8.0</TargetFramework>"))
        {
            errors.Add($"File {file.FileName} targets a framework other than net8.0. Must target .NET 8.0");
        }

        // Note: IBot implementation check moved to ValidateAllFiles
        // Individual files are no longer required to implement IBot - only one file in the submission needs to
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

    /// <summary>
    /// Validate that required documentation files are present.
    /// Penalty Kicks and Security Game files are optional (bonus games).
    /// Uses flexible matching - accepts files containing key terms.
    /// </summary>
    private static void ValidateRequiredFiles(List<BotFile> files, List<string> errors)
    {
        var fileNames = files.Select(f => f.FileName).ToList();

        // Required file patterns - checks if filename contains all the key terms (case-insensitive)
        var requiredPatterns = new[]
        {
            new { Name = "plan-rpsls.md", Terms = new[] { "plan", "rpsls", ".md" } },
            new { Name = "plan-colonelBlotto.md", Terms = new[] { "plan", "colonel", "blotto", ".md" } },
            new { Name = "RPSLS_Skill.md", Terms = new[] { "rpsls", "sk", ".md" } },  // "sk" matches "skill" or "Sk" 
            new { Name = "colonelBlotto_Skill.md", Terms = new[] { "colonel", "blotto", "sk", ".md" } },
            new { Name = "Research.agent.md", Terms = new[] { "research", "agent", ".md" } },
            new { Name = "plan-workshop.md", Terms = new[] { "plan", "workshop", ".md" } },
            new { Name = "copilot-instructions.md", Terms = new[] { "copilot", "instruction", ".md" } }
        };

        // Optional files (bonus games - not mandatory for submission)
        // penaltyKicks_Skill.md, securityGame_Skill.md
        // plan-penaltyKicks.md, plan-securityGame.md

        var missingFiles = new List<string>();
        
        foreach (var pattern in requiredPatterns)
        {
            // Check if any file contains all required terms (case-insensitive)
            var hasMatch = fileNames.Any(fileName => 
                pattern.Terms.All(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
            );
            
            if (!hasMatch)
            {
                missingFiles.Add(pattern.Name);
            }
        }

        if (missingFiles.Any())
        {
            errors.Add($"Missing required documentation files (or similar): {string.Join(", ", missingFiles)}");
        }
    }

    /// <summary>
    /// Find workspace root by walking up directory tree looking for .sln file
    /// or checking for UserBot.Core in published scenarios
    /// </summary>
    private static string FindWorkspaceRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        // First check if UserBot/UserBot.Core exists in current directory (published scenario)
        if (Directory.Exists(Path.Combine(currentDir, "UserBot", "UserBot.Core")))
        {
            return currentDir;
        }
        
        // Walk up directory tree looking for .sln file (development scenario)
        var searchDir = new DirectoryInfo(currentDir);
        while (searchDir != null)
        {
            if (File.Exists(Path.Combine(searchDir.FullName, "TournamentEngine.sln")))
            {
                return searchDir.FullName;
            }
            searchDir = searchDir.Parent;
        }

        // Fallback to current directory if not found
        return currentDir;
    }
}
