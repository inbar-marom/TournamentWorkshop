using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Loads and compiles bot code from files using Roslyn compilation.
/// Supports single-file and multi-file bots.
/// </summary>
public class BotLoader : IBotLoader
{
    public Task<List<BotInfo>> LoadBotsFromDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("LoadBotsFromDirectoryAsync not yet implemented");
    }

    public Task<BotInfo> LoadBotFromFolderAsync(string teamFolder, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("LoadBotFromFolderAsync not yet implemented");
    }

    public BotValidationResult ValidateBotCode(Dictionary<string, string> files)
    {
        throw new NotImplementedException("ValidateBotCode not yet implemented");
    }
}
