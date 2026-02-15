$botsDir = "TournamentEngine.Dashboard\bots"
$botFolders = Get-ChildItem -Path $botsDir -Directory

$strategies = @(
    'return Task.FromResult("Rock");',
    'return Task.FromResult("Paper");',
    'return Task.FromResult("Scissors");',
    'return Task.FromResult("Lizard");',
    'return Task.FromResult("Spock");',
    'var moves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" }; return Task.FromResult(moves[gameState.CurrentRound % 5]);',
    'var moves = new[] { "Paper", "Scissors", "Rock", "Spock", "Lizard" }; return Task.FromResult(moves[gameState.CurrentRound % 5]);',
    'var moves = new[] { "Scissors", "Rock", "Paper", "Lizard", "Spock" }; return Task.FromResult(moves[gameState.CurrentRound % 5]);',
    'var moves = new[] { "Lizard", "Spock", "Rock", "Paper", "Scissors" }; return Task.FromResult(moves[gameState.CurrentRound % 5]);',
    'var moves = new[] { "Spock", "Lizard", "Paper", "Scissors", "Rock" }; return Task.FromResult(moves[gameState.CurrentRound % 5]);'
)

$i = 0
foreach ($folder in $botFolders) {
    $botFile = Join-Path $folder.FullName "Bot.cs"
    if (Test-Path $botFile) {
        $teamName = $folder.Name -replace '_v\d+$', ''
        $namespace = $teamName -replace '[^a-zA-Z0-9]', '_'
        $className = "${namespace}Bot"
        
        # Cycle through strategies
        $strategy = $strategies[$i % $strategies.Count]
        
        $botCode = @"

using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

namespace ${namespace}
{
    public class ${className} : IBot
    {
        public string TeamName => "$teamName";
        public GameType GameType => GameType.RPSLS;

        public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            $strategy
        }

        public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { 20, 20, 20, 20, 20 });
        }

        public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            return Task.FromResult("KickLeft");
        }

        public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            return Task.FromResult("Scan");
        }
    }
}
"@
        
        $botCode | Set-Content -Path $botFile
        Write-Host "Updated $teamName with strategy $($i % $strategies.Count)"
        $i++
    }
}

Write-Host "`nUpdated $i bot files with varied strategies"
