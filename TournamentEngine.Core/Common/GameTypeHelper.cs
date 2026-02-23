namespace TournamentEngine.Core.Common;

/// <summary>
/// Helper utility for GameType enum conversions and display names
/// </summary>
public static class GameTypeHelper
{
    /// <summary>
    /// Gets the display name for a GameType enum value
    /// </summary>
    public static string GetDisplayName(GameType gameType) => gameType switch
    {
        GameType.RPSLS => "RPSLS",
        GameType.ColonelBlotto => "Colonel Blotto",
        GameType.PenaltyKicks => "Penalty Kicks",
        GameType.SecurityGame => "Security Game",
        _ => gameType.ToString()
    };

    /// <summary>
    /// Gets the enum name for a GameType (same as ToString but more explicit)
    /// </summary>
    public static string GetEnumName(GameType gameType) => gameType.ToString();
}
