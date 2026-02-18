using System.Reflection;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Factory for creating bot adapters based on the bot's type system
/// Automatically detects if a bot uses a different namespace and wraps it appropriately
/// </summary>
public static class BotAdapterFactory
{
    private static readonly Type EngineIBotType = typeof(IBot);
    private static readonly string EngineNamespace = "TournamentEngine.Core.Common";
    
    /// <summary>
    /// Detects if a bot instance needs adaptation and creates appropriate adapter
    /// Returns the original bot if it's already compatible, or an adapter if conversion is needed
    /// </summary>
    public static IBot CreateAdapterIfNeeded(object botInstance)
    {
        if (botInstance == null)
            throw new ArgumentNullException(nameof(botInstance));
        
        var botType = botInstance.GetType();
        
        // If bot already implements TournamentEngine.Core.Common.IBot, return as-is
        if (EngineIBotType.IsAssignableFrom(botType))
        {
            return (IBot)botInstance;
        }
        
        // Check if bot implements an IBot from a different namespace
        var botInterfaces = botType.GetInterfaces();
        var iBotInterface = botInterfaces.FirstOrDefault(i => i.Name == "IBot");
        
        if (iBotInterface == null)
        {
            throw new InvalidOperationException(
                $"Bot type {botType.Name} does not implement any IBot interface");
        }
        
        // Determine the namespace/type system
        var botNamespace = iBotInterface.Namespace ?? "Unknown";
        
        // Create dynamic adapter for cross-namespace bot
        return new DynamicBotAdapter(botInstance, botNamespace);
    }
    
    /// <summary>
    /// Checks if a type needs adaptation (uses different namespace)
    /// </summary>
    public static bool NeedsAdapter(Type botType)
    {
        if (EngineIBotType.IsAssignableFrom(botType))
            return false;
        
        var botInterfaces = botType.GetInterfaces();
        return botInterfaces.Any(i => i.Name == "IBot" && i.Namespace != EngineNamespace);
    }
    
    /// <summary>
    /// Gets the namespace/type system a bot uses
    /// </summary>
    public static string GetBotTypeSystem(Type botType)
    {
        if (EngineIBotType.IsAssignableFrom(botType))
            return EngineNamespace;
        
        var botInterfaces = botType.GetInterfaces();
        var iBotInterface = botInterfaces.FirstOrDefault(i => i.Name == "IBot");
        
        return iBotInterface?.Namespace ?? "Unknown";
    }
}
