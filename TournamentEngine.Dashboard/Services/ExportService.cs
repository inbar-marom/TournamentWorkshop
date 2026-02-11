using System.Text.Json;
using System.Text;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Dashboard.Services;

/// <summary>
/// Service for exporting tournament data in various formats
/// Phase 6: Polish & Features - Export Functionality
/// </summary>
public class ExportService
{
    private readonly StateManagerService _stateManager;
    private readonly MatchFeedService _matchFeed;
    private readonly ILogger<ExportService> _logger;
    private readonly string[] _supportedFormats = new[] { "json", "csv" };

    public ExportService(
        StateManagerService stateManager,
        MatchFeedService matchFeed,
        ILogger<ExportService> logger)
    {
        _stateManager = stateManager;
        _matchFeed = matchFeed;
        _logger = logger;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(state, options);
    }

    public async Task<string> ExportToCsvAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var sb = new StringBuilder();

        // CSV header
        sb.AppendLine("Rank,TeamName,Points,Wins,Losses");

        // CSV rows
        if (state.OverallLeaderboard != null)
        {
            foreach (var team in state.OverallLeaderboard.OrderBy(t => t.Rank))
            {
                sb.AppendLine($"{team.Rank},{team.TeamName},{team.TotalPoints},{team.TotalWins},{team.TotalLosses}");
            }
        }

        return sb.ToString();
    }

    public async Task<string> ExportMatchHistoryAsync()
    {
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(matches, options);
    }

    public Task<string[]> GetAvailableFormatsAsync()
    {
        return Task.FromResult(_supportedFormats);
    }

    public async Task<ExportResultDto> ExportAsync(string format)
    {
        if (!_supportedFormats.Contains(format.ToLower()))
        {
            throw new ArgumentException($"Unsupported format: {format}. Valid formats are: {string.Join(", ", _supportedFormats)}");
        }

        string content;
        switch (format.ToLower())
        {
            case "json":
                content = await ExportToJsonAsync();
                break;
            case "csv":
                content = await ExportToCsvAsync();
                break;
            default:
                throw new ArgumentException($"Unsupported format: {format}");
        }

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
        var fileName = $"tournament-{timestamp}.{format.ToLower()}";

        return new ExportResultDto
        {
            Format = format.ToLower(),
            Content = content,
            FileName = fileName,
            ExportedAt = DateTime.UtcNow,
            Version = "1.0"
        };
    }

    public async Task<string> ExportFullSnapshotAsync()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        var matches = await _matchFeed.GetRecentMatchesAsync(1000);

        var snapshot = new
        {
            ExportedAt = DateTime.UtcNow,
            Version = "1.0",
            TournamentState = state,
            Matches = matches
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(snapshot, options);
    }
}

/// <summary>
/// Export result DTO
/// </summary>
public class ExportResultDto
{
    public string Format { get; set; } = "";
    public string Content { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public string Version { get; set; } = "";
}
