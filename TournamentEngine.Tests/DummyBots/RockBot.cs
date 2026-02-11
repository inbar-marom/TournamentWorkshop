using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.DummyBots;

/// <summary>
/// Simple RPSLS bot that always plays Rock
/// </summary>
public class RockBot : IBot
{
    public string TeamName => "RockBot";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Rock");
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        // Even distribution for Colonel Blotto
        return Task.FromResult(new int[] { 20, 20, 20, 20, 20 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Left");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Defend");
    }
}
