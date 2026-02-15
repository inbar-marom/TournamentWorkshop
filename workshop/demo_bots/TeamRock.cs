using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public sealed class TeamRockBot : IBot
{
    public string TeamName => "TeamRock";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken ct) => Task.FromResult("Rock");
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken ct) => Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken ct) => Task.FromResult("Left");
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken ct)
    {
        var role = gameState.State.TryGetValue("Role", out var r) ? r?.ToString() : "Attacker";
        if (role == "Attacker")
            return Task.FromResult("1");  // Always attack middle target
        else
            return Task.FromResult("10,10,10");  // Even distribution
    }
}
