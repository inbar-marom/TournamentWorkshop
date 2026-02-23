using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for managing bot dashboard operations.
/// Provides methods to retrieve, search, filter, sort, and validate bots.
/// Implements caching for performance optimization.
/// </summary>
public class BotDashboardService : IDisposable
{
    private readonly BotStorageService _storageService;
    private readonly IBotLoader _botLoader;
    private readonly ILogger<BotDashboardService> _logger;
    private readonly TournamentConfig _config;
    
    // Cache for bot list with TTL
    private List<BotDashboardDto>? _botsCache;
    private DateTime _cacheExpirationTime = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(20); // Refresh bots every 20 seconds
    private string _cacheFingerprint = string.Empty;
    private readonly object _cacheLock = new();

    public BotDashboardService(
        BotStorageService storageService,
        IBotLoader botLoader,
        ILogger<BotDashboardService> logger,
        TournamentConfig config)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _botLoader = botLoader ?? throw new ArgumentNullException(nameof(botLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets all submitted bots with their validation status.
    /// Results are cached for performance.
    /// Sorted by submission time (newest first) by default.
    /// </summary>
    public async Task<List<BotDashboardDto>> GetAllBotsAsync()
    {
        try
        {
            var submissions = _storageService.GetAllSubmissions();
            var submissionsFingerprint = BuildSubmissionsFingerprint(submissions);

            // Check cache first
            lock (_cacheLock)
            {
                if (_botsCache != null
                    && DateTime.UtcNow < _cacheExpirationTime
                    && string.Equals(_cacheFingerprint, submissionsFingerprint, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Returning cached bot list");
                    return new List<BotDashboardDto>(_botsCache);
                }
            }

            _logger.LogInformation("Loading {Count} bots in parallel...", submissions.Count);

            // Load bots in parallel for better performance (max 10 at a time to avoid overwhelming the system)
            var semaphore = new SemaphoreSlim(10, 10);
            var tasks = submissions.Select(async submission =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await ConvertToBotsDto(submission);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading bot details for {TeamName}", submission.TeamName);
                    return null; // Will be filtered out
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var botResults = await Task.WhenAll(tasks);
            var bots = botResults.Where(b => b != null).Cast<BotDashboardDto>().ToList();

            // Sort by submission time (newest first)
            bots = bots.OrderByDescending(b => b.SubmissionTime).ToList();

            // Update cache
            lock (_cacheLock)
            {
                _botsCache = new List<BotDashboardDto>(bots);
                _cacheExpirationTime = DateTime.UtcNow.Add(_cacheDuration);
                _cacheFingerprint = submissionsFingerprint;
            }

            return bots;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all bots");
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific bot.
    /// </summary>
    public async Task<BotDashboardDto> GetBotDetailsAsync(string teamName)
    {
        try
        {
            var submission = _storageService.GetSubmission(teamName);
            
            if (submission == null)
            {
                throw new KeyNotFoundException($"Bot submission not found for team: {teamName}");
            }

            return await ConvertToBotsDto(submission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bot details for {TeamName}", teamName);
            throw;
        }
    }

    /// <summary>
    /// Searches bots by team name (case-insensitive).
    /// </summary>
    public async Task<List<BotDashboardDto>> SearchBotsAsync(string searchTerm)
    {
        try
        {
            var allBots = await GetAllBotsAsync();
            
            var searchLower = searchTerm.ToLowerInvariant();
            var filtered = allBots
                .Where(b => b.TeamName.ToLowerInvariant().Contains(searchLower))
                .ToList();

            _logger.LogInformation("Search for '{SearchTerm}' returned {Count} results", searchTerm, filtered.Count);
            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching bots for term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    /// <summary>
    /// Filters bots by validation status.
    /// </summary>
    public async Task<List<BotDashboardDto>> FilterByStatusAsync(ValidationStatus status)
    {
        try
        {
            var allBots = await GetAllBotsAsync();
            var filtered = allBots.Where(b => b.Status == status).ToList();

            _logger.LogInformation("Filtered bots by status {Status}: {Count} results", status, filtered.Count);
            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering bots by status: {Status}", status);
            throw;
        }
    }

    /// <summary>
    /// Validates a bot by running it through the BotLoader.
    /// Returns updated bot details with validation results.
    /// </summary>
    public async Task<BotDashboardDto> ValidateBotAsync(string teamName)
    {
        try
        {
            _logger.LogInformation("Starting validation for bot: {TeamName}", teamName);

            var submission = _storageService.GetSubmission(teamName);
            if (submission == null)
            {
                throw new KeyNotFoundException($"Bot submission not found for team: {teamName}");
            }

            // Run validation using BotLoader
            var botInfo = await _botLoader.LoadBotFromFolderAsync(submission.FolderPath, _config);

            // Clear cache to force refresh
            lock (_cacheLock)
            {
                _botsCache = null;
            }

            // Build and return updated BotDashboardDto
            var dto = new BotDashboardDto
            {
                TeamName = submission.TeamName,
                SubmissionTime = submission.SubmissionTime,
                LastUpdatedTime = DateTime.UtcNow,
                Status = botInfo.IsValid ? ValidationStatus.Valid : ValidationStatus.Invalid,
                FileCount = submission.FileCount,
                TotalSizeBytes = submission.TotalSizeBytes,
                Version = submission.Version,
                CompilationError = botInfo.IsValid ? null : string.Join("; ", botInfo.ValidationErrors),
                FileNames = submission.FilePaths ?? new List<string>(),
                VersionHistory = new List<BotVersionInfo>()
            };

            _logger.LogInformation("Validation completed for {TeamName}. Status: {Status}", teamName, dto.Status);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating bot: {TeamName}", teamName);
            throw;
        }
    }

    /// <summary>
    /// Sorts bots by specified property.
    /// </summary>
    public async Task<List<BotDashboardDto>> SortBotsAsync(List<BotDashboardDto> bots, string sortBy, bool ascending = true)
    {
        try
        {
            var sorted = sortBy.ToLowerInvariant() switch
            {
                "teamname" => ascending 
                    ? bots.OrderBy(b => b.TeamName).ToList()
                    : bots.OrderByDescending(b => b.TeamName).ToList(),
                
                "submissiontime" => ascending
                    ? bots.OrderBy(b => b.SubmissionTime).ToList()
                    : bots.OrderByDescending(b => b.SubmissionTime).ToList(),
                
                "status" => ascending
                    ? bots.OrderBy(b => b.Status).ToList()
                    : bots.OrderByDescending(b => b.Status).ToList(),
                
                "filecount" => ascending
                    ? bots.OrderBy(b => b.FileCount).ToList()
                    : bots.OrderByDescending(b => b.FileCount).ToList(),
                
                "size" => ascending
                    ? bots.OrderBy(b => b.TotalSizeBytes).ToList()
                    : bots.OrderByDescending(b => b.TotalSizeBytes).ToList(),
                
                _ => bots
            };

            return await Task.FromResult(sorted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sorting bots by {SortBy}", sortBy);
            throw;
        }
    }

    /// <summary>
    /// Gets version history for a specific bot.
    /// </summary>
    public async Task<List<BotVersionInfo>> GetBotVersionHistoryAsync(string teamName)
    {
        try
        {
            var botDetails = await GetBotDetailsAsync(teamName);
            return botDetails.VersionHistory ?? new List<BotVersionInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version history for {TeamName}", teamName);
            throw;
        }
    }

    /// <summary>
    /// Loads valid bots with compiled instances for tournament execution.
    /// </summary>
    public async Task<List<BotInfo>> LoadValidBotInfosAsync(CancellationToken cancellationToken = default)
    {
        var submissions = _storageService.GetAllSubmissions();
        var bots = new List<BotInfo>();

        foreach (var submission in submissions)
        {
            try
            {
                var botInfo = await _botLoader.LoadBotFromFolderAsync(submission.FolderPath, _config, cancellationToken);
                if (botInfo.IsValid)
                {
                    bots.Add(botInfo);
                }
                else
                {
                    _logger.LogWarning("Bot '{TeamName}' is invalid: {Errors}",
                        botInfo.TeamName,
                        string.Join("; ", botInfo.ValidationErrors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load bot for tournament: {TeamName}", submission.TeamName);
            }
        }

        return bots;
    }

    /// <summary>
    /// Clears the cache to force a refresh on next GetAllBotsAsync call.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _botsCache = null;
            _cacheExpirationTime = DateTime.MinValue;
            _cacheFingerprint = string.Empty;
        }
        _logger.LogInformation("Bot cache cleared");
    }

    private static string BuildSubmissionsFingerprint(List<BotSubmissionMetadata> submissions)
    {
        if (submissions.Count == 0)
        {
            return "empty";
        }

        return string.Join('|', submissions
            .OrderBy(s => s.TeamName, StringComparer.OrdinalIgnoreCase)
            .Select(s => $"{s.TeamName}:{s.Version}:{s.SubmissionTime.Ticks}:{s.FileCount}:{s.TotalSizeBytes}"));
    }

    #region Private Helpers

    /// <summary>
    /// Converts BotSubmissionMetadata to BotDashboardDto with validation info.
    /// </summary>
    private async Task<BotDashboardDto> ConvertToBotsDto(BotSubmissionMetadata submission)
    {
        try
        {
            // Load bot info for validation status
            var botInfo = await _botLoader.LoadBotFromFolderAsync(submission.FolderPath, _config);

            var dto = new BotDashboardDto
            {
                TeamName = submission.TeamName,
                SubmissionTime = submission.SubmissionTime,
                LastUpdatedTime = DateTime.UtcNow,
                Status = botInfo.IsValid ? ValidationStatus.Valid : ValidationStatus.Invalid,
                FileCount = submission.FileCount,
                TotalSizeBytes = submission.TotalSizeBytes,
                Version = submission.Version,
                CompilationError = botInfo.IsValid ? null : string.Join("; ", botInfo.ValidationErrors),
                FileNames = submission.FilePaths ?? new List<string>(),
                VersionHistory = new List<BotVersionInfo>()
            };

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading bot info for {TeamName}, marking as invalid", submission.TeamName);
            
            return new BotDashboardDto
            {
                TeamName = submission.TeamName,
                SubmissionTime = submission.SubmissionTime,
                LastUpdatedTime = DateTime.UtcNow,
                Status = ValidationStatus.Invalid,
                FileCount = submission.FileCount,
                TotalSizeBytes = submission.TotalSizeBytes,
                Version = submission.Version,
                CompilationError = $"Failed to load bot: {ex.Message}",
                FileNames = submission.FilePaths ?? new List<string>(),
                VersionHistory = new List<BotVersionInfo>()
            };
        }
    }

    #endregion

    public void Dispose()
    {
        ClearCache();
        GC.SuppressFinalize(this);
    }
}
