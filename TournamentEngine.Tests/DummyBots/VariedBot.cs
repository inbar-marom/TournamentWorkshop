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

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        var index = (gameState.CurrentRound + _seed) % Moves.Length;
        return Task.FromResult(Moves[index]);
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        var pattern = BlottoPatterns[(_seed - 1) % BlottoPatterns.Length];
        return Task.FromResult(pattern.ToArray());
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        var decision = ((gameState.CurrentRound + _seed) % 2 == 0) ? "Left" : "Right";
        return Task.FromResult(decision);
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult("Scan");
    }
}
