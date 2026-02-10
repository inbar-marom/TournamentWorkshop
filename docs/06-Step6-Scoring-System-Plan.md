# Scoring System Implementation Plan

## Goals

- Implement `IScoringSystem` in `TournamentEngine.Core/Scoring/ScoringSystem.cs`.
- Provide consistent match scoring, standings updates, rankings, and statistics.
- Keep logic deterministic and independent of tournament format.

## Scope

- Match score calculation from `MatchResult` and `MatchOutcome`.
- Standings update based on match results.
- Current rankings and final rankings.
- Tournament statistics.

## Design Notes

- Use the existing points model: win = 3, draw = 1, loss = 0.
- Ensure draw and error outcomes are handled consistently.
- Keep `IScoringSystem` stateless and pure where possible.

## Implementation Steps

1. Create `TournamentEngine.Core/Scoring/ScoringSystem.cs`.
2. Implement `CalculateMatchScore(MatchResult)`.
3. Implement `UpdateStandings(MatchResult, Dictionary<string, TournamentStanding>)`:
   - Ensure entries exist for both teams.
   - Update points, wins/draws/losses, goals/score metrics, games played.
4. Implement `GetCurrentRankings(TournamentInfo)`:
   - Order by points, then tiebreakers (goal differential, wins, name).
5. Implement `GenerateFinalRankings(TournamentInfo)`:
   - Use `GetCurrentRankings` ordering and assign placements.
6. Implement `CalculateStatistics(TournamentInfo)`:
   - Total matches, rounds, average duration, error counts.
7. Wire `ScoringSystem` into engine construction (where applicable).

## Tests

- Add unit tests for `ScoringSystem` in `TournamentEngine.Tests`:
  - Score calculation for each `MatchOutcome`.
  - Standings update for win/draw/loss.
  - Ranking ordering and tiebreakers.
  - Statistics aggregation on sample data.

## Done Criteria

- All new tests pass.
- Scoring system outputs consistent standings and rankings.
- Engine uses scoring system without breaking existing behavior.
