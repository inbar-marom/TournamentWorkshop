namespace TournamentEngine.Core.Common;

/// <summary>
/// Base exception for tournament engine errors
/// </summary>
public class TournamentEngineException : Exception
{
    public TournamentEngineException(string message) : base(message) { }
    public TournamentEngineException(string message, Exception innerException) : base(message, innerException) { }
}
