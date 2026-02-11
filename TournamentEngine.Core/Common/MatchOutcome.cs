namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents the outcome of a single match
/// </summary>
public enum MatchOutcome
{
    Unknown = 0,
    Player1Wins,
    Player2Wins,
    Draw,
    BothError,
    Player1Error,
    Player2Error
}
