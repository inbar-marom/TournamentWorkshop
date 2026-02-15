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

public class PaperBot : IBot
{
    public string TeamName => "PaperBot";
    public GameType GameType => Core.GameType.RPSLS;

    // RPSLS: Always returns Paper
    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Paper");
    }

    // Colonel Blotto: Focus on first three battlefields
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 30, 30, 30, 5, 5 });
    }

    // Penalty Kicks: Choose left
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Left");
    }

    // Security: Attack
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Attack");
    }
}

public class ScissorsBot : IBot
{
    public string TeamName => "ScissorsBot";
    public GameType GameType => Core.GameType.RPSLS;

    // RPSLS: Always returns Scissors
    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Scissors");
    }

    // Colonel Blotto: Focus on last three battlefields
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { 5, 5, 30, 30, 30 });
    }

    // Penalty Kicks: Choose right
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Right");
    }

    // Security: Patrol
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Patrol");
    }
}

public class RandomBot : IBot
{
    private readonly Random _random = new();
    private readonly string[] _rpslsMoves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
    private readonly string[] _penaltyMoves = { "Left", "Center", "Right" };
    private readonly string[] _securityMoves = { "Defend", "Attack", "Patrol" };

    public string TeamName => "RandomBot";
    public GameType GameType => Core.GameType.RPSLS;

    // RPSLS: Returns a random move (including Lizard and Spock)
    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_rpslsMoves[_random.Next(_rpslsMoves.Length)]);
    }

    // Colonel Blotto: Random allocation that sums to 100
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        var troops = new int[5];
        var remaining = 100;
        
        for (int i = 0; i < 4; i++)
        {
            troops[i] = _random.Next(0, remaining + 1);
            remaining -= troops[i];
        }
        troops[4] = remaining;
        
        return Task.FromResult(troops);
    }

    // Penalty Kicks: Random direction
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_penaltyMoves[_random.Next(_penaltyMoves.Length)]);
    }

    // Security: Random action
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_securityMoves[_random.Next(_securityMoves.Length)]);
    }
}
