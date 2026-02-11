namespace TournamentEngine.Core.Common;

/// <summary>
/// Exception thrown when a bot fails during execution
/// </summary>
public class BotExecutionException : TournamentEngineException
{
    public string TeamName { get; }
    public GameType GameType { get; }
    
    public BotExecutionException(string teamName, GameType gameType, string message) : base(message)
    {
        TeamName = teamName;
        GameType = gameType;
    }
    
    public BotExecutionException(string teamName, GameType gameType, string message, Exception innerException) : base(message, innerException)
    {
        TeamName = teamName;
        GameType = gameType;
    }
}
