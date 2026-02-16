namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for loading and compiling bot code from files or directories.
/// Uses Roslyn to compile C# source files into executable bots.
/// </summary>
public interface IBotLoader
{
    /// <summary>
    /// Loads all bots from the specified directory.
    /// Scans for team folders, each containing one or more .cs files.
    /// Each bot handles all game types through a single IBot implementation.
    /// </summary>
    /// <param name="directory">Directory containing team folders with bot source files</param>
    /// <param name="config">Tournament configuration (optional, required for memory monitoring)</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>List of bot information (both valid and invalid bots)</returns>
    Task<List<BotInfo>> LoadBotsFromDirectoryAsync(
        string directory,
        TournamentConfig? config = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads a single bot from a team folder.
    /// Compiles all .cs files in the folder together into one assembly.
    /// Bot must implement IBot interface and handle all game types.
    /// </summary>
    /// <param name="teamFolder">Path to team folder containing bot source files</param>
    /// <param name="config">Tournament configuration (optional, required for memory monitoring)</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Bot information with populated BotInstance if valid</returns>
    Task<BotInfo> LoadBotFromFolderAsync(
        string teamFolder,
        TournamentConfig? config = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reloads all bots from their original folder paths.
    /// This resets memory tracking counters and creates fresh bot instances.
    /// </summary>
    /// <param name="existingBots">List of BotInfo instances to reload</param>
    /// <param name="config">Tournament configuration (optional, required for memory monitoring)</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>List of reloaded BotInfo instances</returns>
    Task<List<BotInfo>> ReloadAllBotsAsync(
        List<BotInfo> existingBots,
        TournamentConfig? config = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates bot code files without compiling.
    /// Checks syntax, IBot implementation, namespace restrictions, and size limits.
    /// Accepts multiple file contents for multi-file bots.
    /// </summary>
    /// <param name="files">Dictionary of filename to file content</param>
    /// <returns>Validation result with any errors</returns>
    BotValidationResult ValidateBotCode(Dictionary<string, string> files);
}
