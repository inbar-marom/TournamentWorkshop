using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.DummyBots;

/// <summary>
/// Random RPSLS bot using System.Random
/// </summary>
public class RandomBot : IBot
{
    private readonly Random _random = new();
    private readonly string[] _moves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };

    public string TeamName => "RandomBot";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        var move = _moves[_random.Next(_moves.Length)];
        return Task.FromResult(move);
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        // Random allocation ensuring sum = 100
        var allocation = new int[5];
        var remaining = 100;
        
        for (int i = 0; i < 4; i++)
        {
            allocation[i] = _random.Next(0, remaining + 1);
            remaining -= allocation[i];
        }
        allocation[4] = remaining;
        
        return Task.FromResult(allocation);
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_random.Next(2) == 0 ? "Left" : "Right");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_random.Next(2) == 0 ? "Attack" : "Defend");
    }
}
