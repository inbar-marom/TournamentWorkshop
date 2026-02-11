namespace TournamentEngine.Core.Common;

/// <summary>
/// Exception thrown when a bot makes an invalid move
/// </summary>
public class InvalidMoveException : TournamentEngineException
{
    public string TeamName { get; }
    public string Move { get; }
    
    public InvalidMoveException(string teamName, string move, string message) : base(message)
    {
        TeamName = teamName;
        Move = move;
    }
}
