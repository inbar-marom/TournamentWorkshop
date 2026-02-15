using UserBot.Core;

namespace UserBot.BasicBot;

public class NaiveBot : IBot
{
    public string TeamName => "NaiveBot";
    public GameType GameType => Core.GameType.RPSLS;

    // RPSLS: Always returns Rock
    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Rock");
    }

    // Colonel Blotto: Naive equal distribution
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    }

    // Penalty Kicks: Naive decision
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Center");
    }

    // Security: Naive decision
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Defend");
    }
}

