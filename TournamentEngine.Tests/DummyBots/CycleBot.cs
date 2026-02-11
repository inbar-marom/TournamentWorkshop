using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.DummyBots;

/// <summary>
/// Simple RPSLS bot that cycles through moves
/// </summary>
public class CycleBot : IBot
{
    private int _moveCounter = 0;
    private readonly string[] _moves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };

    public string TeamName => "CycleBot";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        var move = _moves[_moveCounter % _moves.Length];
        _moveCounter++;
        return Task.FromResult(move);
    }

    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        // Aggressive allocation for first battlefield
        return Task.FromResult(new int[] { 40, 15, 15, 15, 15 });
    }

    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_moveCounter % 2 == 0 ? "Left" : "Right");
    }

    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        return Task.FromResult(_moveCounter % 2 == 0 ? "Attack" : "Defend");
    }
}
