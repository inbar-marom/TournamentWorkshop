namespace TournamentEngine.Core.Common;

/// <summary>
/// Result of bot code validation, including any errors or warnings
/// </summary>
public class BotValidationResult
{
    /// <summary>
    /// Indicates whether the bot code passed all validation checks
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// List of validation errors (prevents bot from loading)
    /// </summary>
    public List<string> Errors { get; init; } = new();
    
    /// <summary>
    /// List of validation warnings (bot can still load but may have issues)
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// List of blocked namespaces detected in the bot code
    /// </summary>
    public List<string> BlockedNamespaces { get; init; } = new();
}
