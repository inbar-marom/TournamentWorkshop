namespace TournamentEngine.Console.Configuration;

using System.Collections.Generic;
using TournamentEngine.Core.Common;

/// <summary>
/// Root configuration object that matches appsettings.json structure
/// </summary>
public class TournamentConfiguration
{
    public TournamentEngineSettings? TournamentEngine { get; set; }
    public TournamentSeriesSettings? TournamentSeries { get; set; }
    public LoggingSettings? Logging { get; set; }
}

/// <summary>
/// Tournament Engine runtime configuration settings
/// </summary>
public class TournamentEngineSettings
{
    /// <summary>
    /// Directory path where bot submissions are stored
    /// </summary>
    public string? BotsDirectory { get; set; }

    /// <summary>
    /// Directory path where tournament results are exported
    /// </summary>
    public string? ResultsDirectory { get; set; }

    /// <summary>
    /// List of game types to run in the tournament series
    /// </summary>
    public List<GameType>? DefaultGameTypes { get; set; }

    /// <summary>
    /// Timeout (seconds) for loading bots from directory
    /// </summary>
    public int BotLoadingTimeout { get; set; }

    /// <summary>
    /// Timeout (seconds) for each bot move/decision
    /// </summary>
    public int MoveTimeout { get; set; }

    /// <summary>
    /// Memory limit (MB) for bot execution
    /// </summary>
    public int MemoryLimitMB { get; set; }

    /// <summary>
    /// Maximum number of matches to run in parallel per round
    /// </summary>
    public int MaxParallelMatches { get; set; }

    /// <summary>
    /// Maximum number of rounds for RPSLS game
    /// </summary>
    public int MaxRoundsRPSLS { get; set; }

    /// <summary>
    /// Maximum number of rounds for Colonel Blotto game
    /// </summary>
    public int MaxRoundsBlotto { get; set; }

    /// <summary>
    /// Maximum number of rounds for Penalty Kicks game
    /// </summary>
    public int MaxRoundsPenaltyKicks { get; set; }

    /// <summary>
    /// Maximum number of rounds for Security Game
    /// </summary>
    public int MaxRoundsSecurityGame { get; set; }

    /// <summary>
    /// Logging level (Debug, Information, Warning, Error)
    /// </summary>
    public string? LogLevel { get; set; }

    /// <summary>
    /// Enable dashboard service for real-time viewing
    /// </summary>
    public bool EnableDashboard { get; set; }

    /// <summary>
    /// URL of the dashboard SignalR hub
    /// </summary>
    public string? DashboardUrl { get; set; }

    /// <summary>
    /// Port on which dashboard service listens
    /// </summary>
    public int DashboardPort { get; set; }
}

/// <summary>
/// Tournament series execution settings
/// </summary>
public class TournamentSeriesSettings
{
    /// <summary>
    /// Name/title for this tournament series
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Number of bots to include in the tournament
    /// </summary>
    public int BotCount { get; set; }

    /// <summary>
    /// Whether to randomly seed bots for group assignments
    /// </summary>
    public bool SeedRandomly { get; set; }

    /// <summary>
    /// Whether to export results to JSON after tournament completion
    /// </summary>
    public bool ExportResults { get; set; }
}

/// <summary>
/// Logging configuration for Microsoft.Extensions.Logging
/// </summary>
public class LoggingSettings
{
    /// <summary>
    /// Log level settings per category
    /// </summary>
    public Dictionary<string, string>? LogLevel { get; set; }
}
