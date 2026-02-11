# Plan: Demo Bots and Quick Tournament

Goal: Add two simple RPSLS demo bots on disk and run a minimal tournament to validate the engine end-to-end.

## Deliverables
- Two demo bot source files under `demo_bots/`:
  - `TeamRock.cs` — always plays "Rock"
  - `TeamPaper.cs` — always plays "Paper"
- A tiny integration test (`DemoBotsTournamentTests.cs`) that runs a tournament with these two bots and asserts champion is TeamPaper.
- Optional console harness to run the same demo outside tests.

## Bot Requirements
- Implement the `IBot` interface from `TournamentEngine.Core.Common`.
- Provide valid implementations for all game methods:
  - `MakeMove` (RPSLS): returns a valid string among [Rock, Paper, Scissors, Lizard, Spock]
  - `AllocateTroops` (Blotto): returns 5 integers summing to 100
  - `MakePenaltyDecision` (Penalty): returns one of [Left, Right]
  - `MakeSecurityMove` (Security): returns one of [Attack, Defend]
- Keep bots deterministic and fast (no sleeps; return immediately).

## File Layout
- `workshop/demo_bots/TeamRock.cs`
- `workshop/demo_bots/TeamPaper.cs`

## Example Bot Skeleton

```csharp
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
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken ct) => Task.FromResult("Defend");
}
```

## Minimal Tournament Run (Test)
- Build `List<BotInfo>` for TeamRock and TeamPaper:
  - `TeamName` set to bot team
  - `GameType = GameType.RPSLS`
  - `FilePath` pointing to `demo_bots/TeamRock.cs` and `demo_bots/TeamPaper.cs`
  - `IsValid = true`
- Use `TournamentManager.RunTournamentAsync(bots, GameType.RPSLS, config)`
- Assert:
  - Tournament completed
  - Champion is `TeamPaper`
  - Match results exist

## Config
- Use a small config:
  - `Games = [RPSLS]`
  - `MoveTimeout = 1s`, `MaxRoundsRPSLS = 5–10`
  - Default logging/resources

## Optional Console Harness
- In `workshop/Program.cs`, instantiate two `BotInfo` for demo bots and run a single tournament; write summary to console.

## Notes
- The engine should not depend on compiling from `FilePath` for this demo; if bot loading is required, ensure these files compile and that the loader uses allowed namespaces only.
- Keep bots self-contained and free of external dependencies.
