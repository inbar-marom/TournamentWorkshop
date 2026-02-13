namespace TournamentEngine.Api.Models;

/// <summary>
/// Represents a file submission as part of a bot
/// </summary>
public class BotFile
{
    public required string FileName { get; init; }
    public required string Code { get; init; }
}

/// <summary>
/// Request to submit a bot via API
/// </summary>
public class BotSubmissionRequest
{
    public required string TeamName { get; init; }
    public required List<BotFile> Files { get; init; }
    public bool Overwrite { get; init; } = true;
}

/// <summary>
/// Result of a bot submission
/// </summary>
public class BotSubmissionResult
{
    public bool Success { get; init; }
    public string? TeamName { get; init; }
    public string? SubmissionId { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Metadata about a submitted bot
/// </summary>
public class BotSubmissionMetadata
{
    public required string TeamName { get; init; }
    public required string FolderPath { get; init; }
    public List<string> FilePaths { get; init; } = new();
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTime SubmissionTime { get; init; }
    public int Version { get; init; }
    public string SubmissionId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response for listing all bots
/// </summary>
public class ListBotsResponse
{
    public List<BotSubmissionMetadata> Bots { get; init; } = new();
}

/// <summary>
/// Response for batch submission
/// </summary>
public class BatchSubmissionResponse
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BotSubmissionResult> Results { get; init; } = new();
}
