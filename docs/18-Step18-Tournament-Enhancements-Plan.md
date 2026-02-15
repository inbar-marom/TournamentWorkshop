# Step 18: Tournament Engine Enhancements - TDD Implementation Plan

## Overview
Comprehensive enhancement of the tournament system including API improvements, game mechanics fixes, and tournament structure updates.

---

## Phase 1: Quick Wins - API & Game History (Estimated: 2-3 hours)

### 1.1: Increase Submission Size Limit ✓ Simple
**Goal:** Update API to accept up to 500KB total submission size

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/Api/BotEndpointsTests.cs`
   - Test: `SubmitBot_500KBPayload_Succeeds()`
   - Test: `SubmitBot_501KBPayload_ReturnsPayloadTooLarge()`

2. **Update Code:**
   - File: `TournamentEngine.Api/Endpoints/BotEndpoints.cs`
   - Change: `maxTotalSize = 500_000;` (line ~92)

3. **Run Tests:** Verify both new and existing tests pass

---

### 1.2: Add Bot Verification Endpoint ✓ Medium
**Goal:** Allow clients to verify bot code before submission

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/Api/BotVerificationTests.cs`
   - Test: `VerifyBot_ValidBot_ReturnsSuccess()`
   - Test: `VerifyBot_InvalidBot_ReturnsValidationErrors()`
   - Test: `VerifyBot_TimeoutBot_ReturnsTimeoutError()`

2. **Create Models:**
   - File: `TournamentEngine.Api/Models/BotModels.cs`
   ```csharp
   public class BotVerificationRequest
   {
       public required string TeamName { get; init; }
       public required List<BotFile> Files { get; init; }
       public GameType? GameType { get; init; } // Optional: verify for specific game
   }
   
   public class BotVerificationResult
   {
       public bool IsValid { get; init; }
       public List<string> Errors { get; init; } = new();
       public List<string> Warnings { get; init; } = new();
       public string Message { get; init; } = string.Empty;
   }
   ```

3. **Implement Endpoint:**
   - File: `TournamentEngine.Api/Endpoints/BotEndpoints.cs`
   - Add: `POST /api/bots/verify`
   - Logic: Use `BotLoader.LoadBotFromMemory()` → `GameRunner.ValidateBot()`

4. **Run Tests:** All verification tests pass

---

### 1.3: Add Zip Download Endpoint ✓ Simple
**Goal:** Serve pre-packaged bot templates as zip files

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/Api/ResourceEndpointsTests.cs`
   - Test: `DownloadBotTemplate_ReturnsZipFile()`
   - Test: `DownloadBotTemplate_InvalidName_Returns404()`

2. **Create Endpoint:**
   - File: `TournamentEngine.Api/Endpoints/ResourceEndpoints.cs` (new)
   ```csharp
   group.MapGet("/templates/{templateName}", DownloadTemplate)
       .WithName("DownloadBotTemplate");
   ```

3. **Hardcoded Templates Path:**
   - `TournamentEngine.Api/templates/` folder
   - `starter-bot.zip`, `advanced-bot.zip`

4. **Run Tests:** Endpoint returns zip files correctly

---

### 1.4: Add Game History to GameState ✓ Medium
**Goal:** Provide bots with their move history against current opponent

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/GameRunner/GameHistoryTests.cs`
   - Test: `RPSLS_SecondMatch_IncludesPreviousHistory()`
   - Test: `PenaltyKicks_ProvidesBothRolesHistory()`

2. **Update GameState Model:**
   - File: `TournamentEngine.Core/Common/GameState.cs`
   ```csharp
   public class GameState
   {
       // ... existing properties ...
       public List<RoundHistory> RoundHistory { get; init; } = new();
       public Dictionary<string, object> OpponentHistory { get; init; } = new();
   }
   
   public class RoundHistory
   {
       public int Round { get; init; }
       public string? MyMove { get; init; }
       public string? OpponentMove { get; init; }
       public string? Result { get; init; } // "Win", "Loss", "Draw"
   }
   ```

3. **Update All Executors:**
   - `RPSLSExecutor`, `BlottoExecutor`, `PenaltyExecutor`, `SecurityExecutor`
   - Store round results in GameState.RoundHistory
   - Track matches by opponent name

4. **Run Tests:** All game executors provide history correctly

---

## Phase 2: Game Mechanics - Role Randomization (Estimated: 1-2 hours)

### 2.1: Randomize Penalty Kicks Roles ✓ Medium
**Goal:** Randomly assign shooter/goalkeeper roles instead of always Bot1/Bot2

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/GameRunner/PenaltyKicksRandomizationTests.cs`
   - Test: `PenaltyKicks_MultipleMatches_AssignsRandomRoles()`
   - Test: `PenaltyKicks_RoleAssignment_IsFair()` (50/50 distribution over 100 matches)

2. **Update PenaltyExecutor:**
   - File: `TournamentEngine.Core/GameRunner/Executors/PenaltyExecutor.cs`
   - Add: `Random _random = new Random();`
   - Logic: `bool bot1IsShooter = _random.Next(2) == 0;`
   - Swap roles accordingly in execution loop

3. **Update Match Log:**
   - Clearly indicate which bot got which role

4. **Run Tests:** Role assignment is random and fair

---

### 2.2: Randomize Security Game Roles ✓ Medium
**Goal:** Randomly assign attacker/defender roles

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/GameRunner/SecurityGameRandomizationTests.cs`
   - Test: `SecurityGame_MultipleMatches_AssignsRandomRoles()`
   - Test: `SecurityGame_RoleAssignment_IsFair()`

2. **Update SecurityExecutor:**
   - File: `TournamentEngine.Core/GameRunner/Executors/SecurityExecutor.cs`
   - Same pattern as Penalty Kicks

3. **Run Tests:** Role assignment is random and fair

---

## Phase 3: Tournament Structure Overhaul (Estimated: 6-8 hours)

### 3.1: Multi-Game Tournament Support ✓ Complex
**Goal:** Tournament runs 4 different game types (events)

**Current Architecture Analysis:**
- Current: Single GameType per tournament
- Needed: 4 GameTypes per tournament with separate scoring

**TDD Steps:**
1. **Write Test First:**
   - `TournamentEngine.Tests/Tournament/MultiGameTournamentTests.cs`
   - Test: `Tournament_Runs4GameTypes_CombinesScores()`
   - Test: `Tournament_EachBotPlays_AllGameTypes()`

2. **Update TournamentConfig:**
   ```csharp
   public class TournamentConfig
   {
       // ... existing ...
       public List<GameType> GameTypes { get; init; } = new();
       public int GroupCount { get; init; } = 10;
   }
   ```

3. **Update TournamentInfo:**
   ```csharp
   public class TournamentInfo
   {
       // ... existing ...
       public List<GameType> GameTypes { get; init; } = new();
       public Dictionary<GameType, List<MatchResult>> ResultsByGame { get; init; } = new();
       public Dictionary<string, Dictionary<GameType, int>> ScoresByBotAndGame { get; init; } = new();
   }
   ```

4. **Refactor GroupStageTournamentEngine:**
   - Support multiple game loops
   - Each group plays round-robin for each game type
   - Aggregate scores across all game types

5. **Run Tests:** Multi-game tournaments work correctly

---

### 3.2: 10-Group Random Assignment ✓ Medium
**Goal:** Divide bots into 10 groups randomly

**TDD Steps:**
1. **Write Test First:**
   - Test: `Tournament_100Bots_Creates10Groups()`
   - Test: `Tournament_RandomGroupAssignment_IsBalanced()`
   - Test: `Tournament_75Bots_Creates10GroupsWithRemainder()`

2. **Update CreateInitialGroups():**
   ```csharp
   private List<Group> CreateInitialGroups(List<IBot> bots)
   {
       var random = new Random();
       var shuffled = bots.OrderBy(x => random.Next()).ToList();
       var groupSize = (int)Math.Ceiling(bots.Count / 10.0);
       
       return Enumerable.Range(0, 10)
           .Select(i => new Group
           {
               GroupId = i + 1,
               Bots = shuffled.Skip(i * groupSize).Take(groupSize).ToList()
           })
           .Where(g => g.Bots.Any())
           .ToList();
   }
   ```

3. **Run Tests:** Groups are balanced and random

---

### 3.3: Win-Based Point System ✓ Medium
**Goal:** 1 point per game win (not per round), match-level scoring

**Current Issue:** Might already be implemented correctly - verify!

**TDD Steps:**
1. **Write Test First:**
   - Test: `Scoring_RPSLSGame_WinnerGets1Point()`
   - Test: `Scoring_10RoundGame_SinglePointForWinner()`
   - Test: `Scoring_DrawGame_BothGetZeroPoints()`

2. **Verify Scoring System:**
   - File: `TournamentEngine.Core/Scoring/ScoringSystem.cs`
   - Ensure: 1 win = 1 point in group standings
   - Not: Accumulated score from individual rounds

3. **Update if Needed:**
   - Fix any scoring logic that counts rounds instead of games

4. **Run Tests:** Point system is game-based, not round-based

---

### 3.4: Tiebreaker System with Colonel Blotto ✓ Complex
**Goal:** Resolve ties at all stages using Blotto elimination tournament

**TDD Steps:**
1. **Write Test First:**
   - Test: `GroupStage_3WayTie_RunsBlottoTiebreaker()`
   - Test: `FinalStage_2WayTie_RunsBlottoTiebreaker()`
   - Test: `Tiebreaker_4Players_RunsEliminationTree()`

2. **Create Tiebreaker Service:**
   - File: `TournamentEngine.Core/Tournament/TiebreakerService.cs`
   ```csharp
   public class TiebreakerService
   {
       public async Task<List<IBot>> ResolveTie(
           List<IBot> tiedBots,
           IGameRunner gameRunner,
           TournamentConfig config,
           CancellationToken cancellationToken)
       {
           // Run single-elimination bracket using Colonel Blotto
           // Return bots in ranked order
       }
   }
   ```

3. **Integration Points:**
   - After each group: Check for ties, run tiebreaker
   - After final stage: Check for ties, run tiebreaker
   - Generate clear bracket structure

4. **Run Tests:** Tiebreakers produce consistent, fair rankings

---

### 3.5: Final Stage Advancement ✓ Medium
**Goal:** Top N from each group advance, compete in final round-robin

**TDD Steps:**
1. **Write Test First:**
   - Test: `Groups_TopBot_AdvancesToFinalStage()`
   - Test: `FinalStage_10Winners_PlayRoundRobin()`

2. **Update Advancement Logic:**
   ```csharp
   private List<IBot> SelectGroupWinners(List<Group> groups)
   {
       return groups
           .Select(g => g.Standings.OrderByDescending(s => s.Points).First())
           .Select(s => s.Bot)
           .ToList();
   }
   ```

3. **Final Stage Execution:**
   - Create single "final group" from winners
   - Run 4-game round-robin among finalists
   - Apply same tiebreaker if needed

4. **Run Tests:** Advancement and final stage work correctly

---

## Phase 4: Fix Integration Tests (Estimated: 2-3 hours)

### 4.1: Analyze Hub Test Failures ✓ Debugging
**Goal:** Understand why 9 Hub integration tests are failing

**Investigation Steps:**
1. **Read Test Code:**
   - `TournamentEngine.Tests/Integration/FullStackIntegrationTests.cs`
   - Identify common failure patterns

2. **Check Issues:**
   - SignalR connection timing?
   - Event broadcasting race conditions?
   - Missing await statements?

3. **Write Diagnostic Tests:**
   - Add logging to track event flow
   - Add delays/synchronization if needed

---

### 4.2: Fix Hub Real-Time Issues ✓ Fix
**Common Issues to Check:**
- Hub connection lifecycle
- Event subscription timing
- Async/await patterns
- Message serialization

**TDD Steps:**
1. **One Test at a Time:**
   - Fix: `Hub_RealTimeProgress_MatchesStreamAsTheyComplete()`
   - Fix: `Hub_StreamsRoundStartEvents()`
   - etc.

2. **Each Fix:**
   - Identify root cause
   - Add synchronization if needed
   - Verify no regression

3. **Run Full Suite:** All 626 tests pass

---

## Implementation Order Summary

### Sprint 1 (Day 1): Quick Wins
1. ✅ Size limit (15 min)
2. ✅ Zip endpoint (30 min)
3. ✅ Verification endpoint (1 hour)
4. ✅ Game history (2 hours)

### Sprint 2 (Day 2): Role Randomization
1. ✅ Penalty Kicks roles (1 hour)
2. ✅ Security Game roles (1 hour)
3. ✅ Testing and validation (1 hour)

### Sprint 3 (Day 3-4): Tournament Structure
1. ✅ Multi-game support (3 hours)
2. ✅ 10-group assignment (1 hour)
3. ✅ Win-based scoring verification (1 hour)
4. ✅ Tiebreaker system (3 hours)
5. ✅ Final stage advancement (2 hours)

### Sprint 4 (Day 5): Integration Tests
1. ✅ Hub test analysis (1 hour)
2. ✅ Fix Hub issues (2-3 hours)
3. ✅ Full regression testing (1 hour)

---

## Testing Strategy

### Unit Tests
- Each executor has role randomization tests
- Scoring system has game-win tests
- Group creation has randomization tests
- Tiebreaker has bracket generation tests

### Integration Tests
- Full tournament with 4 game types
- Tiebreaker scenarios
- Multi-stage advancement
- Hub event streaming (fix existing)

### Test Coverage Goals
- All game executors: 90%+
- Tournament engine: 85%+
- API endpoints: 80%+
- Overall: 85%+

---

## Success Criteria

### Functional
- ✅ All 7 requirements implemented
- ✅ All tests pass (626+ tests)
- ✅ No regressions in existing functionality

### Quality
- ✅ TDD approach maintained throughout
- ✅ Clear commit messages per feature
- ✅ Documentation updated
- ✅ Performance acceptable (tournaments complete in reasonable time)

---

## Risk Mitigation

### High Risk Items
1. **Multi-game tournament refactor** - Large architectural change
   - Mitigation: Feature flag, incremental rollout
   
2. **Hub test fixes** - Complex async issues
   - Mitigation: Add extensive logging, one test at a time

### Medium Risk Items
1. **Tiebreaker implementation** - New complex logic
   - Mitigation: Comprehensive test suite first

2. **Role randomization** - Changes game semantics
   - Mitigation: Seed-based randomization for reproducibility

---

## Notes

- **Backward Compatibility:** Maintain single-game tournament support
- **Configuration:** All new features configurable via TournamentConfig
- **Logging:** Add detailed logging for debugging tournament flow
- **Documentation:** Update API docs and workshop guides

---

## Next Steps After This Plan

1. Review and approve plan
2. Create GitHub issues for each phase
3. Set up feature branches
4. Begin Sprint 1 implementation
5. Daily standup to track progress
