# Step 4 Plan: Game Runner Implementation (C#)

Implement a deterministic, testable Game Runner that executes matches between two bots per game type, enforces timeouts, validates outputs, captures logs, and returns a `MatchResult` via existing interfaces. Keep it modular so game-specific logic is pluggable and reusable.

## Goals
- Execute matches for supported games (RPSLS, Blotto, Penalty, Security)
- Enforce timeouts and handle bot errors without crashing the engine
- Validate outputs per game rules
- Produce detailed `MatchResult` including logs, scores, and outcome
- Keep design extensible and unit-test friendly

## Interfaces and Contracts (Existing)
- `IBot`: Bot contract with async methods (`MakeMove`, `AllocateTroops`, etc.)
- `IGame`: Game contract with rules (`GetInitialState`, `IsGameOver`, `ApplyMove`, etc.)
- `IGameRunner`: Runner contract (`ExecuteMatch(...)`, `ValidateBot(...)`)
- `MatchResult`, `MatchOutcome`, `GameState`, `TournamentConfig`

## Implementation Steps
1. Create `TournamentEngine.Core/GameRunner/GameRunner.cs`
   - Implement `IGameRunner`
   - Orchestrates match execution for a given `GameType` or `IGame` instance
   - Manages timeouts (`TournamentConfig.MoveTimeout`) via `CancellationToken`
   - Captures per-round logs and final outcome

2. Add per-game executors `TournamentEngine.Core/GameRunner/Executors/`
   - `RpslsExecutor.cs`: 50 rounds, valid moves = [Rock, Paper, Scissors, Lizard, Spock], scoring and outcome
   - `BlottoExecutor.cs`: `int[5]`, each ≥0, sum=100; battlefield comparisons to compute winner
   - `PenaltyExecutor.cs`: decisions [Left, Right]; minimal rules per spec
   - `SecurityExecutor.cs`: actions [Attack, Defend]; minimal rules per spec
   - Common `IGameExecutor` interface to standardize `Execute(IBot a, IBot b, CancellationToken ct)` returning `MatchResult`

3. Timeouts and Error Handling
   - Wrap bot calls in `Task` with `CancellationToken` and timeout
   - Translate exceptions/timeouts to `MatchOutcome.PlayerXError`
   - If both bots error: per spec, random winner (deterministic seed for tests)
   - Append errors to `MatchResult.Errors`

4. Validation per Game
   - RPSLS: output is one of the five allowed strings
   - Blotto: array length = 5, sum = 100, values ≥ 0
   - Penalty: decision in [Left, Right]
   - Security: action in [Attack, Defend]
   - Invalid output → bot error and outcome per spec

5. Result Composition
   - Populate `MatchResult`: bot names, `GameType`, `Outcome`, `WinnerName`, scores, `StartTime/EndTime/Duration`
   - `MatchLog`: round-by-round moves, validations, scoring notes

6. Tests (`TournamentEngine.Tests/GameRunner/`)
   - `RpslsGameRunnerTests.cs`: happy path, invalid move, timeout handling, draw scenarios
   - `BlottoValidationTests.cs`: valid vs invalid allocations, battlefield wins
   - `ExecutorSelectionTests.cs`: `IGameRunner` routes to correct executor
   - Ensure determinism (seed any randomness)

## Design Notes
- Determinism: Seed randomness in executors for reproducible tests
- Extensibility: New games add a new executor; `GameRunner` stays thin
- Performance: Sequential by default; later can parallelize per round
- Sandboxing: In-process timeouts first; later consider external process/Job Objects if stricter isolation needed

## Deliverables
- Source files under `TournamentEngine.Core/GameRunner/` and `.../Executors/`
- Unit tests under `TournamentEngine.Tests/GameRunner/`
- Minimal README updates referencing Step 4 behavior
