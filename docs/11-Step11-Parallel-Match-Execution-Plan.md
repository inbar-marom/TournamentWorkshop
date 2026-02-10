# Step 11 - Parallel Match Execution Plan

## Goal

Enable parallel execution of matches within a round/phase while preserving deterministic results, thread safety, and cancellation behavior.

## Scope

- `TournamentManager` parallelizes match execution per batch.
- `TournamentSeriesManager` (when implemented) also supports parallel execution.
- `ScoringSystem` remains deterministic and thread-safe by applying updates in a single-threaded aggregation pass.

## Design Principles

- **Do not mutate shared state during parallel execution.**
- Execute matches concurrently, collect `MatchResult` objects, then apply updates sequentially.
- Preserve deterministic ordering when recording results (stable ordering by match list).
- Maintain cancellation checks before and during match execution.

## Proposed API Changes

- Add optional `maxDegreeOfParallelism` to tournament execution (config or constructor).
- If not configured, default to `Environment.ProcessorCount` with a reasonable upper bound (e.g., 8).

## Implementation Steps

1. Extend `TournamentConfig` with optional `MaxParallelMatches`.
2. Update `TournamentManager.RunTournamentAsync`:
   - For each match batch, execute via `Task.WhenAll` with degree-of-parallelism limiter.
   - Collect `MatchResult[]` in the same order as the input matches.
   - Record results sequentially using `_engine.RecordMatchResult`.
3. Ensure all shared structures in engine remain updated on the main thread only.
4. Add tests for parallel execution behavior.

## Tests

- **Unit tests** for `TournamentManager`:
  - Uses fake game runner with deterministic delays.
  - Verifies all matches recorded exactly once.
  - Verifies `RecordMatchResult` is called in a stable order.
  - Verifies cancellation interrupts execution.
- **Integration test** (optional):
  - Parallel execution does not change final results compared to sequential.

## Done Criteria

- All tests pass.
- Parallel mode produces same final rankings as sequential.
- No race conditions observed (thread-safe by design).
