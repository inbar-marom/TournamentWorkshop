using System.Text.Json;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for generating shareable links and snapshots
/// Phase 6: Polish & Features - Share Functionality
/// </summary>
public class ShareService
{
    private readonly StateManagerService _stateManager;
    private readonly ILogger<ShareService> _logger;
    private readonly Dictionary<string, TournamentSnapshotDto> _snapshots = new();
    private readonly Dictionary<string, ShareStatsDto> _shareStats = new();

    public ShareService(
        StateManagerService stateManager,
        ILogger<ShareService> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public async Task<string> GenerateShareLinkAsync(string? message = null)
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var baseUrl = "http://localhost:5000/share";
        
        if (string.IsNullOrEmpty(message))
        {
            return $"{baseUrl}?status={state.Status}";
        }

        var encodedMessage = Uri.EscapeDataString(message);
        return $"{baseUrl}?message={encodedMessage}";
    }

    public async Task<TournamentSnapshotDto> CreateSnapshotAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var id = GenerateShortId();

        // Create a deep copy of the state to ensure snapshot independence
        var stateCopy = DeepCopy(state);

        var snapshot = new TournamentSnapshotDto
        {
            Id = id,
            State = stateCopy,
            CreatedAt = DateTime.UtcNow
        };

        _snapshots[id] = snapshot;
        _shareStats[id] = new ShareStatsDto { ViewCount = 0 };

        _logger.LogInformation("Created snapshot {SnapshotId}", id);
        return snapshot;
    }

    public Task<TournamentSnapshotDto?> GetSnapshotAsync(string id)
    {
        if (_snapshots.TryGetValue(id, out var snapshot))
        {
            return Task.FromResult<TournamentSnapshotDto?>(snapshot);
        }

        return Task.FromResult<TournamentSnapshotDto?>(null);
    }

    public Task<List<TournamentSnapshotDto>> ListSnapshotsAsync()
    {
        return Task.FromResult(_snapshots.Values
            .OrderByDescending(s => s.CreatedAt)
            .ToList());
    }

    public Task<bool> DeleteSnapshotAsync(string id)
    {
        var removed = _snapshots.Remove(id);
        if (removed)
        {
            _shareStats.Remove(id);
            _logger.LogInformation("Deleted snapshot {SnapshotId}", id);
        }
        return Task.FromResult(removed);
    }

    public Task<string> GenerateEmbedCodeAsync(string snapshotId, int width = 800, int height = 600)
    {
        var embedCode = $@"<iframe src=""http://localhost:5000/embed/{snapshotId}"" width=""{width}"" height=""{height}"" frameborder=""0""></iframe>";
        return Task.FromResult(embedCode);
    }

    public Task<ShareStatsDto> GetShareStatsAsync(string snapshotId)
    {
        if (_shareStats.TryGetValue(snapshotId, out var stats))
        {
            return Task.FromResult(stats);
        }

        return Task.FromResult(new ShareStatsDto { ViewCount = 0 });
    }

    public Task TrackViewAsync(string snapshotId)
    {
        if (_shareStats.TryGetValue(snapshotId, out var stats))
        {
            stats.ViewCount++;
        }
        return Task.CompletedTask;
    }

    private string GenerateShortId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Creates a deep copy of an object using JSON serialization
    /// </summary>
    private T DeepCopy<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}

/// <summary>
/// Tournament snapshot DTO
/// </summary>
public class TournamentSnapshotDto
{
    public string Id { get; set; } = "";
    public TournamentStateDto State { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Share statistics DTO
/// </summary>
public class ShareStatsDto
{
    public int ViewCount { get; set; }
}
