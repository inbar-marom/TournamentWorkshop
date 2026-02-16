namespace TournamentEngine.Core.Common;

/// <summary>
/// Tournament configuration settings
/// </summary>
public class TournamentConfig
{
    public TimeSpan ImportTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MoveTimeout { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Maximum number of matches to run concurrently within a single stage.
    /// - 1 = Sequential execution (slowest, deterministic order)
    /// - N = At most N matches run concurrently (balanced)
    /// - int.MaxValue = Unlimited parallel execution within stage (fastest)
    /// 
    /// THREAD SAFETY GUARANTEES:
    /// - All matches within a stage complete before advancing to next stage
    /// - Shared data (standings, rankings) protected by locks
    /// - Stage boundaries always synchronized (group stage → finals → tiebreaker)
    /// </summary>
    public int MaxParallelMatches { get; init; } = 1;
    public int MemoryLimitMB { get; init; } = 512;
    public int MaxRoundsRPSLS { get; init; } = 50;
    public string LogLevel { get; init; } = "INFO";
    public string LogFilePath { get; init; } = "tournament_log.txt";
    public string BotsDirectory { get; init; } = "bots";
    public string ResultsFilePath { get; init; } = "results.json";
    
    // Multi-game tournament properties (Phase 3)
    public List<GameType> GameTypes { get; init; } = new List<GameType>
    {
        GameType.RPSLS,
        GameType.ColonelBlotto,
        GameType.PenaltyKicks,
        GameType.SecurityGame
    };
    
    public int GroupCount { get; init; } = 10; //
    public int FinalistsPerGroup { get; init; } = 1; // Top N from each group advance
    public bool UseTiebreakers { get; init; } = true; //
    public GameType TiebreakerGameType { get; init; } = GameType.ColonelBlotto; //
}
