# Step 9: Multi-Tournament Orchestrator - Implementation Plan

## Objective

Implement a `TournamentSeriesManager` that orchestrates multiple tournaments sequentially, where each tournament may use a different game type, and produces aggregated series-level standings and statistics across all tournaments.

---

## Core Concept

**Series Structure:**
- Run N tournaments sequentially
- Each tournament can use a different `GameType`
- All bots participate in all tournaments
- Individual tournament results are preserved
- Series-level scores aggregate across all tournaments
- Final series standings rank bots by total accumulated score

---

## Components

### 1. `TournamentSeriesConfig`
**Location:** `TournamentEngine.Core/Common/TournamentSeriesConfig.cs`

**Purpose:** Configuration for a tournament series

**Properties:**
```csharp
- List<GameType> GameTypes           // Games for each tournament in order
- TournamentConfig BaseConfig        // Shared config for all tournaments
- int TournamentsCount              // Number of tournaments (can be same game repeated)
- bool AggregateScores              // Whether to sum scores across tournaments
```

**Notes:**
- If `GameTypes.Count < TournamentsCount`, cycle through the list
- If `GameTypes.Count == 1` and `TournamentsCount > 1`, repeat same game

---

### 2. `SeriesStanding`
**Location:** `TournamentEngine.Core/Common/SeriesStanding.cs`

**Purpose:** Bot's performance across entire series

**Properties:**
```csharp
- string BotName
- int TotalSeriesScore              // Sum of scores from all tournaments
- int TournamentsWon                // Number of tournament championships
- int TotalWins                     // Sum of match wins across all tournaments
- int TotalLosses                   // Sum of match losses across all tournaments
- Dictionary<GameType, int> ScoresByGame  // Breakdown per game type
- List<int> TournamentPlacements    // Placement in each tournament (1st, 2nd, etc.)
```

---

### 3. `TournamentSeriesInfo`
**Location:** `TournamentEngine.Core/Common/TournamentSeriesInfo.cs`

**Purpose:** Complete series results and metadata

**Properties:**
```csharp
- string SeriesId                   // Unique identifier
- List<TournamentInfo> Tournaments  // Individual tournament results
- List<SeriesStanding> SeriesStandings  // Aggregated standings
- string SeriesChampion             // Bot with highest total series score
- DateTime StartTime
- DateTime? EndTime
- TournamentSeriesConfig Config
```

---

### 4. `TournamentSeriesManager`
**Location:** `TournamentEngine.Core/Tournament/TournamentSeriesManager.cs`

**Purpose:** Orchestrates multiple tournaments

**Dependencies:**
- `ITournamentManager` (for running individual tournaments)
- `IScoringSystem` (for series-level aggregation)

**Key Method:**
```csharp
Task<TournamentSeriesInfo> RunSeriesAsync(
    List<BotInfo> bots,
    TournamentSeriesConfig config,
    CancellationToken cancellationToken = default)
```

**Responsibilities:**
1. Initialize series with unique ID
2. For each tournament in the series:
   - Determine game type (cycle through config.GameTypes)
   - Run tournament via `ITournamentManager.RunTournamentAsync`
   - Collect tournament results
3. Aggregate scores across all tournaments
4. Calculate series standings
5. Determine series champion
6. Return complete `TournamentSeriesInfo`

---

## Implementation Steps (TDD)

### Phase 1: Data Structures
1. Create `TournamentSeriesConfig` with validation
2. Create `SeriesStanding` 
3. Create `TournamentSeriesInfo`
4. Write unit tests for data structure validation

### Phase 2: Series Manager - Single Tournament
**Test:** `RunSeriesAsync_WithSingleTournament_ProducesCorrectSeriesInfo`
- Run series with 1 tournament
- Verify series standings match tournament standings
- Verify series champion matches tournament champion

**Implementation:**
- Basic `TournamentSeriesManager` constructor
- Initialize series
- Run single tournament
- Map tournament results to series results

### Phase 3: Multiple Tournaments - Same Game
**Test:** `RunSeriesAsync_WithMultipleTournamentsSameGame_AggregatesScores`
- Run 3 tournaments, all RPSLS
- Verify total series score = sum of individual tournament scores
- Verify series champion is bot with highest total

**Implementation:**
- Loop over tournaments count
- Aggregate scores from multiple tournaments
- Calculate series standings

### Phase 4: Multiple Tournaments - Different Games
**Test:** `RunSeriesAsync_WithDifferentGameTypes_RunsEachGameInOrder`
- Run series with [RPSLS, Blotto, RPSLS]
- Verify each tournament used correct game type
- Verify scores aggregated correctly

**Implementation:**
- Cycle through `GameTypes` list
- Track scores by game type in `SeriesStanding.ScoresByGame`

### Phase 5: Series Standings Calculation
**Test:** `RunSeriesAsync_CalculatesSeriesStandingsCorrectly`
- Verify series standings ordered by total score
- Verify tiebreakers: total wins, then fewest losses, then alphabetical

**Implementation:**
- Aggregate logic for `SeriesStanding`
- Sorting/ranking logic

### Phase 6: Tournament Wins Tracking
**Test:** `RunSeriesAsync_TracksTournamentWins`
- Bot wins 2 out of 3 tournaments
- Verify `TournamentsWon == 2`
- Verify `TournamentPlacements == [1, 2, 1]`

**Implementation:**
- Track tournament placements per bot
- Count tournament wins

### Phase 7: Cancellation Support
**Test:** `RunSeriesAsync_CancellationDuringSecondTournament_ThrowsOperationCanceledException`
- Start series with 3 tournaments
- Cancel during 2nd tournament
- Verify cancellation propagates

**Implementation:**
- Pass `cancellationToken` to each `RunTournamentAsync` call
- Check cancellation between tournaments

### Phase 8: Series Statistics
**Test:** `RunSeriesAsync_CalculatesSeriesStatistics`
- Verify total matches across all tournaments
- Verify aggregate duration
- Verify breakdown by game type

**Implementation:**
- Create `CalculateSeriesStatistics()` method
- Aggregate statistics from all tournaments

---

## Test Coverage

### Unit Tests (TournamentSeriesManagerTests.cs)
1. Single tournament series
2. Multiple tournaments - same game
3. Multiple tournaments - different games
4. Series standings calculation and ordering
5. Tournament wins tracking
6. Placements tracking
7. Scores by game type breakdown
8. Cancellation support
9. Empty bot list throws exception
10. Null config throws exception

### Integration Tests (TournamentSeriesIntegrationTests.cs)
1. End-to-end series with real `TournamentManager` and `ScoringSystem`
2. 3 tournaments with different games (RPSLS, Blotto, RPSLS)
3. Verify series champion correctness
4. Verify individual tournament results preserved

---

## Data Flow

```
TournamentSeriesManager.RunSeriesAsync()
  ┌─► Tournament 1 (RPSLS)
  │     ├─ TournamentManager.RunTournamentAsync()
  │     └─ TournamentInfo → store results
  │
  ├─► Tournament 2 (Blotto)
  │     ├─ TournamentManager.RunTournamentAsync()
  │     └─ TournamentInfo → store results
  │
  ├─► Tournament 3 (RPSLS)
  │     ├─ TournamentManager.RunTournamentAsync()
  │     └─ TournamentInfo → store results
  │
  └─► Aggregate Results
        ├─ Sum scores per bot across all tournaments
        ├─ Track tournament wins and placements
        ├─ Calculate series standings
        ├─ Determine series champion
        └─ Return TournamentSeriesInfo
```

---

## Key Design Decisions

### 1. Sequential Execution
- Tournaments run one after another (not in parallel)
- Rationale: Predictable state, simpler logic, clear progression
- Future enhancement: Could parallelize tournaments if needed

### 2. Score Aggregation
- Simple sum of tournament scores
- Alternative considered: Weighted average, but sum is simpler and more intuitive

### 3. Series Champion Determination
- Bot with highest total series score
- Tiebreakers: Total wins → Fewest losses → Tournament wins → Alphabetical

### 4. Configuration
- `TournamentSeriesConfig` wraps `TournamentConfig`
- Series-specific settings (game types, tournament count) separate from per-tournament settings
- All tournaments share same `BaseConfig` (timeouts, parallel matches, etc.)

### 5. Result Preservation
- Individual tournament results stored in `TournamentSeriesInfo.Tournaments`
- Allows post-series analysis of individual tournaments
- Series standings derive from but don't replace tournament standings

---

## Thread Safety

### Current Design: Sequential Tournament Execution
- **No shared mutable state** between tournaments
- Each tournament runs **sequentially** with its own `TournamentInfo` instance
- Aggregation happens **after all tournaments complete**
- **Thread-safe** because there is no concurrent access to shared state
- Underlying `ITournamentManager` and `IScoringSystem` are stateless and thread-safe

### Parallel Matches Within Tournaments
- Each tournament inherits `MaxParallelMatches` from `BaseConfig`
- Parallel execution happens **within** each tournament, not across tournaments
- Safe because each tournament has isolated state managed by `GroupStageTournamentEngine`

### Future Enhancement: Parallel Tournament Execution
**If you modify `TournamentSeriesManager` to run tournaments in parallel, you MUST:**

1. **Synchronize Tournament Collection Access**
   - Use a thread-safe collection (e.g., `ConcurrentBag<TournamentInfo>`) for `seriesInfo.Tournaments`
   - OR add lock synchronization around `Tournaments.Add(tournamentInfo)`

2. **Ensure Aggregation Happens After All Tournaments**
   - Use `Task.WhenAll()` to await all parallel tournament tasks
   - Only call `CalculateSeriesStandings()` and `CalculateSeriesStatistics()` after all tasks complete

3. **Example Pattern for Parallel Execution:**
   ```csharp
   var tournamentTasks = config.GameTypes.Select(async gameType =>
   {
       var tournamentInfo = await _tournamentManager.RunTournamentAsync(
           bots, gameType, config.BaseConfig, cancellationToken);
       
       lock (seriesInfo.Tournaments) // Synchronize collection access
       {
           seriesInfo.Tournaments.Add(tournamentInfo);
       }
   });
   
   await Task.WhenAll(tournamentTasks); // Wait for all to complete
   
   // Now safe to aggregate
   CalculateSeriesStandings(seriesInfo);
   CalculateSeriesStatistics(seriesInfo);
   ```

4. **Why This Is Safe:**
   - `ITournamentManager` and `IScoringSystem` have no shared mutable state
   - Each tournament operates on its own `TournamentInfo` instance
   - Only the series-level collection access needs synchronization

**Note:** Code comments in `TournamentSeriesManager.cs` document these requirements for future developers.

---

## Validation Rules

### TournamentSeriesConfig Validation
- At least 1 game type required
- Tournament count must be > 0
- Base config cannot be null

### RunSeriesAsync Validation
- Bots list must have at least 2 bots
- All bots must participate in all tournaments (no dropouts currently supported)

---

## Future Enhancements (Out of Scope)

1. **Parallel Tournament Execution:** Run independent tournaments in parallel
   - See "Thread Safety" section above for implementation requirements
   - Would reduce total series execution time
   - Requires synchronization around `seriesInfo.Tournaments` collection

2. **Weighted Scoring:** Different tournaments contribute different weights to series score

3. **Bot Eliminations:** Bots can be eliminated after poor performance

4. **Dynamic Game Selection:** Choose next game based on current standings

5. **Series Formats:** Round-robin of tournaments, playoffs, etc.

---

## Success Criteria

- ✅ Run series with multiple tournaments
- ✅ Each tournament can use different game type
- ✅ Series standings aggregate scores correctly
- ✅ Series champion determined by highest total score
- ✅ Individual tournament results preserved
- ✅ Cancellation works during series
- ✅ All unit tests pass
- ✅ Integration test with real components passes
- ✅ Thread-safe for concurrent match execution within tournaments

---

## Example Usage

```csharp
var bots = LoadBots(); // 10 bots

var seriesConfig = new TournamentSeriesConfig
{
    GameTypes = new List<GameType> { GameType.RPSLS, GameType.Blotto, GameType.RPSLS },
    TournamentsCount = 3,
    BaseConfig = new TournamentConfig
    {
        Games = 50,
        MoveTimeout = TimeSpan.FromSeconds(1),
        MaxParallelMatches = 4
    }
};

var seriesManager = new TournamentSeriesManager(tournamentManager, scoringSystem);
var seriesInfo = await seriesManager.RunSeriesAsync(bots, seriesConfig);

Console.WriteLine($"Series Champion: {seriesInfo.SeriesChampion}");
foreach (var standing in seriesInfo.SeriesStandings)
{
    Console.WriteLine($"{standing.BotName}: {standing.TotalSeriesScore} points, " +
                      $"{standing.TournamentsWon} tournament wins");
}
```

---

## Files to Create

1. `TournamentEngine.Core/Common/TournamentSeriesConfig.cs`
2. `TournamentEngine.Core/Common/SeriesStanding.cs`
3. `TournamentEngine.Core/Common/TournamentSeriesInfo.cs`
4. `TournamentEngine.Core/Tournament/TournamentSeriesManager.cs`
5. `TournamentEngine.Tests/TournamentSeriesManagerTests.cs`
6. `TournamentEngine.Tests/Integration/TournamentSeriesIntegrationTests.cs`
