namespace TournamentEngine.Api.Services;

using Models;
using System.Collections.Concurrent;

/// <summary>
/// Service for storing and managing bot submissions
/// Thread-safe concurrent submission handling with version control
/// </summary>
public class BotStorageService
{
    private readonly string _storageDirectory;
    private readonly ILogger<BotStorageService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly object _lock = new();
    private readonly Dictionary<string, BotSubmissionMetadata> _submissions = new(StringComparer.OrdinalIgnoreCase);
    private const long MaxFileSizeBytes = 50_000; // 50KB per file
    private const long MaxTotalSizeBytes = 200_000; // 200KB total

    public BotStorageService(string storageDirectory, ILogger<BotStorageService> logger)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _semaphore = new SemaphoreSlim(1, 1);

        // Create storage directory if it doesn't exist
        Directory.CreateDirectory(_storageDirectory);
        _logger.LogInformation("BotStorageService initialized with directory: {Directory}", _storageDirectory);
    }

    /// <summary>
    /// Store a bot submission with thread safety and version control
    /// </summary>
    public async Task<BotSubmissionResult> StoreBotAsync(BotSubmissionRequest request)
    {
        if (request == null)
            return new BotSubmissionResult { Success = false, Message = "Request is null", Errors = new() { "Invalid request" } };

        if (string.IsNullOrWhiteSpace(request.TeamName))
            return new BotSubmissionResult { Success = false, Message = "Team name is required", Errors = new() { "TeamName cannot be empty" } };

        if (request.Files == null || request.Files.Count == 0)
            return new BotSubmissionResult { Success = false, Message = "At least one file is required", Errors = new() { "Files collection is empty" } };

        var errors = ValidateSubmission(request);
        if (errors.Count > 0)
            return new BotSubmissionResult { Success = false, TeamName = request.TeamName, Message = "Validation failed", Errors = errors };

        await _semaphore.WaitAsync();
        try
        {
            return await StoreBotInternalAsync(request);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Get all stored bot submissions
    /// </summary>
    public List<BotSubmissionMetadata> GetAllSubmissions()
    {
        lock (_lock)
        {
            return _submissions.Values.ToList();
        }
    }

    /// <summary>
    /// Get a specific bot submission by team name
    /// </summary>
    public BotSubmissionMetadata? GetSubmission(string teamName)
    {
        lock (_lock)
        {
            return _submissions.TryGetValue(teamName, out var submission) ? submission : null;
        }
    }

    /// <summary>
    /// Delete a bot submission
    /// </summary>
    public async Task<bool> DeleteBotAsync(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return false;

        await _semaphore.WaitAsync();
        try
        {
            lock (_lock)
            {
                if (!_submissions.TryGetValue(teamName, out var metadata))
                    return false;

                try
                {
                    if (Directory.Exists(metadata.FolderPath))
                    {
                        Directory.Delete(metadata.FolderPath, recursive: true);
                        _logger.LogInformation("Deleted bot folder for team {TeamName}: {FolderPath}", teamName, metadata.FolderPath);
                    }

                    _submissions.Remove(teamName);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete bot for team {TeamName}", teamName);
                    return false;
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<BotSubmissionResult> StoreBotInternalAsync(BotSubmissionRequest request)
    {
        var submissionId = Guid.NewGuid().ToString();
        
        // Determine version
        lock (_lock)
        {
            _submissions.TryGetValue(request.TeamName, out var existing);
            var newVersion = (existing?.Version ?? 0) + 1;
            
            // Delete old version if exists
            if (existing != null && Directory.Exists(existing.FolderPath))
            {
                try
                {
                    Directory.Delete(existing.FolderPath, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old version folder: {FolderPath}", existing.FolderPath);
                }
            }

            var folderName = $"{request.TeamName}_v{newVersion}";
            var folderPath = Path.Combine(_storageDirectory, folderName);

            // Create folder
            Directory.CreateDirectory(folderPath);

            // Write files synchronously to avoid async in lock
            long totalSize = 0;
            var filePaths = new List<string>();

            foreach (var file in request.Files)
            {
                try
                {
                    var filePath = Path.Combine(folderPath, file.FileName);
                    var fileContent = System.Text.Encoding.UTF8.GetBytes(file.Code);
                    System.IO.File.WriteAllBytes(filePath, fileContent);
                    filePaths.Add(file.FileName);
                    totalSize += fileContent.Length;
                    _logger.LogDebug("Wrote file {FileName} for team {TeamName}", file.FileName, request.TeamName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write file {FileName} for team {TeamName}", file.FileName, request.TeamName);
                    throw;
                }
            }

            var metadata = new BotSubmissionMetadata
            {
                TeamName = request.TeamName,
                FolderPath = folderPath,
                FilePaths = filePaths,
                FileCount = request.Files.Count,
                TotalSizeBytes = totalSize,
                SubmissionTime = DateTime.UtcNow,
                Version = newVersion,
                SubmissionId = submissionId
            };

            _submissions[request.TeamName] = metadata;
            _logger.LogInformation("Bot stored for team {TeamName} (v{Version}): {FolderPath}", 
                request.TeamName, newVersion, folderPath);

            return new BotSubmissionResult
            {
                Success = true,
                TeamName = request.TeamName,
                SubmissionId = submissionId,
                Message = $"Bot submitted successfully (v{newVersion})"
            };
        }
    }

    private List<string> ValidateSubmission(BotSubmissionRequest request)
    {
        var errors = new List<string>();

        // Validate team name
        if (request.TeamName.Length > 100)
            errors.Add("Team name must be 100 characters or less");

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.TeamName, @"^[a-zA-Z0-9_\-]+$"))
            errors.Add("Team name must contain only alphanumeric characters, hyphens, and underscores");

        // Validate files
        long totalSize = 0;
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.FileName))
                errors.Add("File name cannot be empty");

            if (string.IsNullOrWhiteSpace(file.Code))
                errors.Add($"File {file.FileName} has no code");

            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            if (fileSize > MaxFileSizeBytes)
                errors.Add($"File {file.FileName} exceeds maximum size of {MaxFileSizeBytes} bytes");

            totalSize += fileSize;

            if (seenNames.Contains(file.FileName))
                errors.Add($"Duplicate file name: {file.FileName}");
            else
                seenNames.Add(file.FileName);
        }

        if (totalSize > MaxTotalSizeBytes)
            errors.Add($"Total submission size {totalSize} bytes exceeds maximum of {MaxTotalSizeBytes} bytes");

        return errors;
    }
}
