# Step 5: Tournament Manager Implementation Plan

## Overview

Implement `TournamentManager.cs` to manage group-stage tournament with two phases (initial groups → final group → tiebreaker), track standings, and determine champion.

**Location:** `TournamentEngine.Core/Tournament/TournamentManager.cs`  
**Interface:** `ITournamentManager` (already defined in Common/)  
**Dependencies:** `IGameRunner` (from step 4), `IScoringSystem` (for group standings), Core contracts

---

## Key Responsibilities

1. **Group Creation** - Divide bots into initial groups (~1/10 of participants each)
2. **Round-Robin Execution** - Generate and track all matches within groups (all vs all)
3. **Standings Management** - Track points, wins, losses for each bot in groups
4. **Group Winner Determination** - Identify winner(s) from each group
5. **Final Group Formation** - Create final group from all group winners
6. **Tiebreaker Handling** - Execute decisive matches if final group has ties
7. **Champion Determination** - Identify overall tournament winner

---

## Implementation Breakdown

### 1. Class Structure & Dependencies

```csharp
public class TournamentManager : ITournamentManager
{
    private readonly IGameRunner _gameRunner;
    private readonly IScoringSystem _scoringSystem;  // Required for group standings
    private TournamentInfo _tournamentInfo;
    private List<Group> _currentGroups;
    private Group? _finalGroup;
    private Queue<(IBot bot1, IBot bot2)> _pendingMatches;
    private Dictionary<string, MatchResult> _matchHistory; // by matchId
    private Dictionary<string, GroupStanding> _groupStandings; // by botName
}

public class Group
{
    public string GroupId { get; set; }
    public List<IBot> Bots { get; set; }
    public Dictionary<string, GroupStanding> Standings { get; set; }
    public bool IsComplete { get; set; }
}

public class GroupStanding
{
    public string BotName { get; set; }
    public int Points { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GoalDifferential { get; set; } // For tiebreakers
}
```

**Internal State to Track:**

- Initial groups (List<Group>)
- Final group (Group?)
- Group standings per bot
- Pending matches for current phase
- Match results by ID
- Current tournament phase (InitialGroups, FinalGroup, Tiebreaker, Complete)

---

### 2. Method Implementation Plan

#### A. `InitializeTournament(List<BotInfo> bots, GameType gameType, TournamentConfig config)`

**Purpose:** Setup group-stage tournament and initial state

**Steps:**

1. Validate bot count (minimum 10 recommended for group play, handle up to 120)
2. Create `TournamentInfo` object with new GUID
3. Convert `BotInfo` list to `IBot` instances (coordinate with step 4 for bot creation)
4. Calculate number of initial groups: `Math.Max(1, bots.Count / 10)`
5. Divide bots into groups via `CreateInitialGroups()`
6. Generate all round-robin matches for each group via `GenerateGroupMatches()`
7. Initialize group standings for all bots
8. Return initialized `TournamentInfo`

**Group Creation Logic:**

- Calculate group count: ~N/10 (e.g., 60 bots → 6 groups of 10)
- Random shuffle bots
- Distribute evenly into groups (handle remainder by adding to first groups)
- Create Group objects with standings tracking

**Round-Robin Match Generation:**

```csharp
// For each group, generate all possible pairings
foreach (var group in groups)
{
    for (int i = 0; i < group.Bots.Count; i++)
    {
        for (int j = i + 1; j < group.Bots.Count; j++)
        {
            matches.Add((group.Bots[i], group.Bots[j]));
        }
    }
}
// Total matches per group of N bots: N * (N-1) / 2
```

**Data Structure for Groups:**

```csharp
List<Group> InitialGroups // Phase 1 groups
Group FinalGroup // Phase 2 group (created after initial groups complete)
```

---

#### B. `GetNextMatches() → List<(IBot bot1, IBot bot2)>`

**Purpose:** Return matches to be played in current phase

**Implementation:**

- Return pending matches from `_pendingMatches` queue
- Format: list of bot pairs ready for `IGameRunner.ExecuteMatch()`
- Can return all matches for current phase (allows parallel execution)
- Or return batch size if managing parallelism within manager

---

#### C. `RecordMatchResult(MatchResult matchResult) → TournamentInfo`

**Purpose:** Update group standings after match completion

**Steps:**

1. Validate result format (bot names exist in current groups)
2. Store in `MatchHistory`
3. Update group standings based on `matchResult.Outcome`
4. Award points: Win=3pts, Draw=1pt each, Loss=0pts (configurable)
5. Update win/loss/draw counts
6. Update goal differential (score difference)
7. Check if current phase complete: `AllGroupMatchesFinished()`
8. If phase complete, trigger `AdvanceToNextPhase()`
9. Update `TournamentInfo` with current standings
10. Return updated `TournamentInfo`

**Outcome Handling:**

- `Player1Wins` → bot1 gets 3 points, bot2 gets 0
- `Player2Wins` → bot2 gets 3 points, bot1 gets 0
- `Draw` → both get 1 point
- `BothError` → both get 0 points (log error)
- `Player1Error` → bot2 gets 3 points (bot1 gets 0, log error)
- `Player2Error` → bot1 gets 3 points (bot2 gets 0, log error)

---

#### D. `AdvanceToNextPhase() → TournamentInfo`

**Purpose:** Transition tournament to next phase after current phase completes

**Steps:**

1. Verify all current phase matches completed
2. Determine group winners by standings (highest points)
3. If in initial groups phase:
   - Extract winner from each group
   - Create final group with all winners
   - Generate round-robin matches for final group
   - Queue matches, transition to FinalGroup phase
4. If in final group phase:
   - Check for clear winner (single bot with most points)
   - If tied: transition to Tiebreaker phase
   - If clear winner: set `Champion`, mark state as `Completed`
5. If in tiebreaker phase:
   - Run decisive match(es) between tied bots
   - Determine champion
6. Return updated `TournamentInfo`

**Phase Transition Logic:**

```
Phase 1 (Initial Groups): N bots in ~N/10 groups → ~N/10 winners
Phase 2 (Final Group): ~N/10 winners in 1 group → 1 winner (or tied)
Phase 3 (Tiebreaker): Tied bots → 1 champion

Example (60 bots):
  Phase 1: 6 groups of 10 → 6 winners
  Phase 2: 1 group of 6 → 1 winner (or tiebreaker)
```

---

#### E. `IsTournamentComplete() → bool`

**Purpose:** Check if tournament has a winner

**Logic:**

- Return `TournamentInfo.State == TournamentState.Completed`
- Or check `TournamentInfo.Champion != null`

---

#### F. `GetTournamentInfo() → TournamentInfo`

**Purpose:** Return current tournament state

**Implementation:**

- Return copy/snapshot of `_tournamentInfo` with:
  - All match results played so far
  - Current bracket state
  - Current round
  - Champion (if complete)

---

### 3. Internal Helper Methods

#### `CreateInitialGroups(List<IBot> bots) → List<Group>`

- Calculate optimal group count (~bots.Count / 10)
- Shuffle bots randomly
- Distribute evenly into groups
- Initialize standings for each bot (0 points, 0 wins, etc.)
- Return list of Group objects

#### `GenerateGroupMatches(Group group) → List<(IBot, IBot)>`

- Generate all pairings within group (round-robin)
- For N bots: N*(N-1)/2 matches
- Return all match pairs for the group

#### `AllGroupMatchesFinished(Group group) → bool`

- Calculate expected matches: N*(N-1)/2
- Count completed matches for bots in this group
- Return true if counts match

#### `DetermineGroupWinner(Group group) → IBot`

- Sort group standings by:
  1. Points (descending)
  2. Head-to-head record (if tied)
  3. Goal differential (descending)
  4. Wins (descending)
- Return top bot
- If still tied: mark for tiebreaker phase

#### `ExecuteTiebreaker(List<IBot> tiedBots) → IBot`

- If 2 bots: run single sudden-death match
- If 3+ bots: run mini single-elimination bracket
- Call `IGameRunner.ExecuteMatch()` with same game type
- Return winner
- Cap retries (max 3 sudden-deaths, then random)

#### `GetGroupStandings(Group group) → List<GroupStanding>`

- Extract standings for all bots in group
- Sort by points (descending)
- Return ordered list for display

#### `UpdateStandings(GroupStanding standing, MatchResult result, bool isBot1) → void`

- Award points based on outcome
- Increment wins/losses/draws
- Update goal differential
- Update standing object

---

### 4. State Transitions & Error Handling

**Tournament State Machine:**

```
NotStarted → (InitializeTournament) → InitialGroups 
→ (All Initial Groups Complete) → FinalGroup 
→ (Final Group Complete, Clear Winner) → Completed
→ (Final Group Complete, Tied) → Tiebreaker
→ (Tiebreaker Complete) → Completed
```

**Error Scenarios:**

- Bot not in any group → throw `InvalidOperationException`
- Match result references unknown bot → throw `ArgumentException`
- `RecordMatchResult()` called twice for same match → validate and ignore duplicate
- Tournament already complete, still calling methods → guard against this
- Group has no clear winner after standings → trigger tiebreaker
- All bots in group error out → handle gracefully, award to random or disqualify group

**Logging:**

- Log group creation and assignments
- Log each phase transition
- Log group standings after each match
- Log group winners
- Log match outcomes for replay
- Log tiebreaker execution
- Log errors/disqualifications

---

### 5. Concurrency & Performance

**Parallel Match Execution Options:**

#### Option A (Simple): Sequential

- Call `GetNextMatches()` → return all group matches for current phase
- Caller runs them in parallel via `Task.WhenAll`
- Caller calls `RecordMatchResult()` for each result
- TournamentManager stays sequential
- **Benefit:** Simple state management, easier to debug

#### Option B (Advanced): Async within manager

- `ExecuteGroupPhase()` method that parallelizes locally
- Own cancellation/timeout handling
- Requires more complex state management
- **Benefit:** Better encapsulation, manager controls concurrency

**Recommendation:** Go with **Option A** for simplicity. Client (Console Program) handles parallelization.

**Note:** Round-robin within groups means many more matches than single-elimination
- Initial groups: Sum of all group matches (~N*(N/10-1)/2 per group)
- Example: 60 bots in 6 groups of 10 = 6 * 45 = 270 matches in phase 1
- Final group: 6 bots = 15 matches in phase 2
- Total: ~285 matches vs ~59 in single-elimination

---

### 6. Data Persistence & DTO

**Tournament Summary DTO:**

```csharp
public class GroupStageSummary
{
    public string PhaseId { get; set; } // "InitialGroups", "FinalGroup", "Tiebreaker"
    public List<GroupSummary> Groups { get; set; }
    public List<string> PhaseWinners { get; set; }
}

public class GroupSummary
{
    public string GroupId { get; set; }
    public List<GroupStanding> Standings { get; set; }
    public List<(string bot1, string bot2, string result)> Matches { get; set; }
    public string Winner { get; set; }
}
```

**For Console Output:**

- Method to export group standings as JSON
- Method to export all match results
- Method to generate group tables (like soccer standings)
- Method to display phase progression

---

## Implementation Order

### 1. Core Structure

- Create class skeleton with `_tournamentInfo`, groups, and standings
- Implement `InitializeTournament()` with group creation
- Test with dummy bots (12, 30, 60 bots)

### 2. Group Management

- Implement `CreateInitialGroups()` and `GenerateGroupMatches()`
- Implement `GetNextMatches()` to return group matches
- Test group creation and match generation

### 3. Match Recording & Standings

- Implement `RecordMatchResult()` with standings updates
- Implement `UpdateStandings()` helper
- Test point allocation, win/loss tracking

### 4. Phase Progression

- Implement `DetermineGroupWinner()` with tiebreaker rules
- Implement `AdvanceToNextPhase()` for initial→final→tiebreaker
- Test phase transitions and winner advancement

### 5. Final Group & Completion

- Implement final group round-robin
- Implement `IsTournamentComplete()`
- Test tournament completion and champion determination

### 6. Tie-Breaking

- Implement `ExecuteTiebreaker()` for final group ties
- Add retry/fallback to random
- Test various tie scenarios

### 7. Output & Stats

- Add group standings export methods
- Add phase summary DTOs
- Add logging throughout

---

## Testing Strategy

### Unit Tests (MSTest)

1. Group creation with various bot counts (12, 30, 60, 120 bots)
2. Round-robin match generation (verify N*(N-1)/2 matches per group)
3. Standings updates after match results
4. Point allocation (win=3, draw=1, loss=0)
5. Group winner determination with various scenarios
6. Tiebreaker logic (points equal, head-to-head, goal differential)
7. Phase advancement (initial groups → final group)
8. Final group round-robin and winner determination
9. Tiebreaker phase execution
10. Tournament completion detection
11. Error handling (invalid bots, duplicate records, all errors in group)

### Integration Tests

1. Full tournament simulation with dummy bots (60 bots)
2. Verify group stage produces expected number of matches
3. Verify all bots play correct number of matches (N-1 per group)
4. Verify final winner is deterministic with clear standings
5. Verify tiebreaker executes when needed
6. Performance test: time to complete 270+ matches

---

## Notes & Considerations

### Open Decisions

- **Points System:** Win=3, Draw=1, Loss=0 (standard) or Win=1, Loss=0 (simpler)?
- **Group Tiebreakers:** Order of tiebreaker criteria (points → head-to-head → goal diff → wins)?
- **Tie-Breaking Matches:** How many sudden-death attempts before random winner?
- **Group Size:** Target ~10 bots per group or adjust based on total count?
- **Logging:** How detailed? Will this affect performance for 120 bots?
- **Move Timeouts:** Handled by `IGameRunner` (step 4), TournamentManager just uses it

### Scaling Considerations

- Verify performance with 120 bots × 4 games
- **Match Count Example (60 bots):**
  - Phase 1: 6 groups of 10 = 6 × 45 = 270 matches
  - Phase 2: 1 group of 6 = 15 matches
  - Total per game: ~285 matches
  - 4 games: ~1,140 matches total
- **120 bots:**
  - Phase 1: 12 groups of 10 = 12 × 45 = 540 matches
  - Phase 2: 1 group of 12 = 66 matches
  - Total per game: ~606 matches
  - 4 games: ~2,424 matches total
- Ensure efficient data structures and minimal overhead per match
- Consider parallel execution for initial group matches (groups are independent)

---

## Success Criteria

- ✅ Groups correctly created for all bot counts (10-120)
- ✅ Round-robin matches generated correctly (N*(N-1)/2 per group)
- ✅ Standings accurately track points, wins, losses, draws
- ✅ Group winners correctly determined with tiebreakers
- ✅ Phase transitions work (initial groups → final group → tiebreaker)
- ✅ Tournament completes with single champion
- ✅ All match results recorded and standings updated
- ✅ Tiebreakers execute when needed
- ✅ Error scenarios handled gracefully
- ✅ Unit tests pass with >90% coverage
- ✅ Performance acceptable for 120 bots (~600 matches per game)
