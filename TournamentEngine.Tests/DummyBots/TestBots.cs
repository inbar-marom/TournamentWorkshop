using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.DummyBots;

/// <summary>
/// Paper bot that always plays Paper (counters Rock)
/// </summary>
public class PaperBot : IBot
{
    public string TeamName => "PaperBot";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Paper");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        // Conservative balanced allocation
        return Task.FromResult(new int[] { 25, 25, 20, 15, 15 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Right");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Defend");
    }
}

/// <summary>
/// Faulty bot that demonstrates error handling
/// </summary>
public class FaultyBot : IBot
{
    public string TeamName => "FaultyBot";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Intentionally return invalid move for testing error handling
        return Task.FromResult("InvalidMove");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        // Invalid allocation - doesn't sum to 100
        return Task.FromResult(new int[] { 30, 30, 30, 30, 30 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("InvalidDirection");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("InvalidAction");
    }
}

/// <summary>
/// Timeout bot for testing timeout handling
/// </summary>
public class TimeoutBot : IBot
{
    public string TeamName => "TimeoutBot";
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Simulate long processing that should timeout
        await Task.Delay(5000, cancellationToken);
        return "Rock";
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);
        return new int[] { 20, 20, 20, 20, 20 };
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);
        return "Left";
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);
        return "Defend";
    }
}
