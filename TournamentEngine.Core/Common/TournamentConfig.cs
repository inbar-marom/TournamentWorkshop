namespace TournamentEngine.Core.Common;

/// <summary>
/// Tournament configuration settings
/// </summary>
public class TournamentConfig
{
    public List<GameType> Games { get; init; } = new();
    public TimeSpan ImportTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MoveTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public int MaxParallelMatches { get; init; } = 1;
    public int MemoryLimitMB { get; init; } = 512;
    public int MaxRoundsRPSLS { get; init; } = 50;
    public string LogLevel { get; init; } = "INFO";
    public string LogFilePath { get; init; } = "tournament_log.txt";
    public string BotsDirectory { get; init; } = "bots";
    public string ResultsFilePath { get; init; } = "results.json";
}
