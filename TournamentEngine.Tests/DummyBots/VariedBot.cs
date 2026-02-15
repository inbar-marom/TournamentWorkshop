using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.DummyBots;

/// <summary>
/// Bot with varied strategies based on a seed value.
/// Different instances produce different move patterns, troop allocations, and penalty decisions.
/// </summary>
public class VariedBot : IBot
{
    private static readonly string[] Moves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
    private static readonly int[][] BlottoPatterns =
    {
        new[] { 20, 20, 20, 20, 20 },
        new[] { 30, 30, 20, 10, 10 },
        new[] { 40, 20, 20, 10, 10 },
        new[] { 10, 10, 20, 30, 30 },
        new[] { 50, 10, 10, 10, 20 },
        new[] { 25, 25, 25, 15, 10 },
        new[] { 34, 33, 33, 0, 0 },
        new[] { 60, 10, 10, 10, 10 }
    };

    private readonly int _seed;

    public VariedBot(string teamName, int seed)
    {
        TeamName = teamName;
        _seed = seed;
    }

    public string TeamName { get; }
    public GameType GameType => GameType.RPSLS;

    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        var index = (gameState.CurrentRound + _seed) % Moves.Length;
        return Moves[index];
    }

    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        var pattern = BlottoPatterns[(_seed - 1) % BlottoPatterns.Length];
        return pattern.ToArray();
    }

    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        var decision = ((gameState.CurrentRound + _seed) % 2 == 0) ? "Left" : "Right";
        return decision;
    }

    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        var role = gameState.State.TryGetValue("Role", out var r) ? r?.ToString() : "Attacker";
        if (role == "Attacker")
        {
            // Pick target based on seed
            return ((_seed + gameState.CurrentRound) % 3).ToString();
        }
        else
        {
            // Different defense patterns based on seed
            var patterns = new[] { "10,10,10", "5,10,15", "2,8,20", "15,10,5" };
            return patterns[_seed % patterns.Length];
        }
    }
}
