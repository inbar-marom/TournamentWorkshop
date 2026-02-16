namespace TournamentEngine.Core.BotLoader;

using Common;

/// <summary>
/// Wraps an IBot implementation to track cumulative memory usage across moves.
/// Memory tracking accumulates during an event and is reset when bot is reloaded.
/// </summary>
public class MemoryMonitoredBot : IBot
{
    private readonly IBot _innerBot;
    private readonly long _maxMemoryBytes;
    private long _cumulativeMemoryBytes = 0;
    private readonly object _memoryLock = new();

    public MemoryMonitoredBot(IBot innerBot, long maxMemoryBytes)
    {
        _innerBot = innerBot ?? throw new ArgumentNullException(nameof(innerBot));
        _maxMemoryBytes = maxMemoryBytes;
    }

    public string TeamName => _innerBot.TeamName;
    public GameType GameType => _innerBot.GameType;

    /// <summary>
    /// Gets the memory limit in bytes
    /// </summary>
    public long MemoryLimitBytes => _maxMemoryBytes;

    /// <summary>
    /// Gets the cumulative memory usage in bytes since last reset
    /// </summary>
    public long CumulativeMemoryUsage
    {
        get
        {
            lock (_memoryLock)
            {
                return _cumulativeMemoryBytes;
            }
        }
    }

    /// <summary>
    /// Resets cumulative memory tracking to zero.
    /// Called when bot is reloaded between events.
    /// </summary>
    public void ResetMemoryTracking()
    {
        lock (_memoryLock)
        {
            _cumulativeMemoryBytes = 0;
        }
    }

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return await ExecuteWithMemoryTracking(
            () => _innerBot.MakeMove(gameState, cancellationToken),
            nameof(MakeMove));
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return await ExecuteWithMemoryTracking(
            () => _innerBot.AllocateTroops(gameState, cancellationToken),
            nameof(AllocateTroops));
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return await ExecuteWithMemoryTracking(
            () => _innerBot.MakePenaltyDecision(gameState, cancellationToken),
            nameof(MakePenaltyDecision));
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return await ExecuteWithMemoryTracking(
            () => _innerBot.MakeSecurityMove(gameState, cancellationToken),
            nameof(MakeSecurityMove));
    }

    /// <summary>
    /// Executes bot method while tracking memory usage before and after execution.
    /// Accumulates memory delta and throws if limit exceeded.
    /// </summary>
    private async Task<T> ExecuteWithMemoryTracking<T>(Func<Task<T>> botMethod, string methodName)
    {
        // Force two GC cycles to establish a clean, consistent baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Capture memory before execution
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Execute bot method
        var result = await botMethod();

        // Capture memory after execution to include all allocations
        // Note: Not forcing GC here to capture the actual memory impact
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Calculate delta (only count positive changes - memory growth)
        var memoryDelta = Math.Max(0, memoryAfter - memoryBefore);

        // Update cumulative tracking and check limit
        lock (_memoryLock)
        {
            _cumulativeMemoryBytes += memoryDelta;

            if (_cumulativeMemoryBytes > _maxMemoryBytes)
            {
                var usedMB = _cumulativeMemoryBytes / 1024.0 / 1024.0;
                var limitMB = _maxMemoryBytes / 1024.0 / 1024.0;
                
                throw new OutOfMemoryException(
                    $"Bot '{TeamName}' exceeded memory limit in {methodName}: " +
                    $"{usedMB:F2}MB / {limitMB:F2}MB (cumulative)");
            }
        }

        return result;
    }
}
