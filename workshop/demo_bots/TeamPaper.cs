using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public sealed class TeamPaperBot : IBot
{
    public string TeamName => "TeamPaper";
    public GameType GameType => GameType.RPSLS;

    public Task<string> MakeMove(GameState gameState, CancellationToken ct) => Task.FromResult("Paper");
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken ct) => Task.FromResult(new[] { 20, 20, 20, 20, 20 });
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken ct) => Task.FromResult("Left");
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken ct)
    {
        var role = gameState.State.TryGetValue("Role", out var r) ? r?.ToString() : "Attacker";
        if (role == "Attacker")
            return Task.FromResult("2");  // Always attack high-value target
        else
            return Task.FromResult("0,5,25");  // Protect high-value target
    }
}
