namespace TournamentEngine.Core.Common;

/// <summary>
/// Exception thrown when a bot fails to load
/// </summary>
public class BotLoadException : TournamentEngineException
{
    public string TeamName { get; }
    
    public BotLoadException(string teamName, string message) : base(message)
    {
        TeamName = teamName;
    }
    
    public BotLoadException(string teamName, string message, Exception innerException) : base(message, innerException)
    {
        TeamName = teamName;
    }
}
