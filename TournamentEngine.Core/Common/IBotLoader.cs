namespace TournamentEngine.Core.Common;

/// <summary>
/// Interface for bot loading and validation services
/// </summary>
public interface IBotLoader
{
    /// <summary>
    /// Load a bot from a file path
    /// </summary>
    /// <param name="filePath">Path to the bot source file</param>
    /// <param name="gameType">Game type the bot should support</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Bot information with validation results</returns>
    Task<BotInfo> LoadBot(string filePath, GameType gameType, CancellationToken cancellationToken);

    /// <summary>
    /// Load all bots from a directory
    /// </summary>
    /// <param name="botsDirectory">Directory containing bot files</param>
    /// <param name="gameType">Game type the bots should support</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>List of bot information with validation results</returns>
    Task<List<BotInfo>> LoadBotsFromDirectory(string botsDirectory, GameType gameType, CancellationToken cancellationToken);

    /// <summary>
    /// Validate that a bot implements the required methods for a game
    /// </summary>
    /// <param name="bot">Bot to validate</param>
    /// <param name="gameType">Game type to validate against</param>
    /// <returns>Validation result with any errors</returns>
    Task<(bool isValid, List<string> errors)> ValidateBot(IBot bot, GameType gameType);

    /// <summary>
    /// Create a bot instance from loaded code
    /// </summary>
    /// <param name="botInfo">Bot information from loading process</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling</param>
    /// <returns>Instantiated bot ready for tournament</returns>
    Task<IBot> CreateBotInstance(BotInfo botInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Get allowed namespaces for bot code
    /// </summary>
    /// <returns>List of allowed namespace patterns</returns>
    List<string> GetAllowedNamespaces();

    /// <summary>
    /// Get blocked namespaces for bot code
    /// </summary>
    /// <returns>List of blocked namespace patterns</returns>
    List<string> GetBlockedNamespaces();
}
