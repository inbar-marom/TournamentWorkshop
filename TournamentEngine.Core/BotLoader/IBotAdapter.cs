using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Interface for bot adapters that translate between different bot type systems and the tournament engine
/// </summary>
public interface IBotAdapter : IBot
{
    /// <summary>
    /// The underlying bot instance being adapted
    /// </summary>
    object WrappedBot { get; }
    
    /// <summary>
    /// The namespace/type system this adapter supports
    /// </summary>
    string SupportedNamespace { get; }
}
