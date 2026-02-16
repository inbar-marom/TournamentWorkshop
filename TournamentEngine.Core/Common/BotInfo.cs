namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a bot loaded from source code.
/// Each bot handles all game types through its IBot implementation.
/// </summary>
public class BotInfo
{
    /// <summary>
    /// The team name extracted from the bot code or folder name
    /// </summary>
    public required string TeamName { get; init; }
    
    /// <summary>
    /// Path to the team folder containing bot source files.
    /// May contain multiple .cs files for multi-file bots.
    /// </summary>
    public string? FolderPath { get; init; }
    
    /// <summary>
    /// Legacy property for single-file bots. Use FolderPath for new implementations.
    /// </summary>
    [Obsolete("Use FolderPath instead. FilePath maintained for backward compatibility.")]
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Indicates whether the bot passed all validation and compilation checks
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// List of validation or compilation errors
    /// </summary>
    public List<string> ValidationErrors { get; init; } = new();
    
    /// <summary>
    /// When the bot was loaded
    /// </summary>
    public DateTime LoadTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// The loaded bot instance, ready to execute for ALL game types.
    /// Null if the bot failed to load (check IsValid and ValidationErrors).
    /// Populated by IBotLoader during bot loading phase.
    /// </summary>
    public IBot? BotInstance { get; init; }
    
    /// <summary>
    /// Memory-monitored wrapper around BotInstance that tracks cumulative memory usage.
    /// Use this for actual bot execution to enforce memory limits.
    /// Null if bot is not wrapped (for backward compatibility or if validation failed).
    /// </summary>
    public BotLoader.MemoryMonitoredBot? MonitoredInstance { get; init; }
    
    /// <summary>
    /// Gets the cumulative memory usage in bytes since last bot reload.
    /// Returns 0 if bot is not memory-monitored.
    /// </summary>
    public long CumulativeMemoryBytes => MonitoredInstance?.CumulativeMemoryUsage ?? 0;
    
    /// <summary>
    /// Resets the cumulative memory tracking to zero.
    /// Called automatically when bots are reloaded between events.
    /// </summary>
    public void ResetMemoryTracking()
    {
        MonitoredInstance?.ResetMemoryTracking();
    }
    
    /// <summary>
    /// Gets the bot instance to use for execution (monitored if available, otherwise raw instance).
    /// </summary>
    public IBot? GetExecutableBot() => MonitoredInstance ?? BotInstance;
}
