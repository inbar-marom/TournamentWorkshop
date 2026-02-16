# Phase 3: Tournament Structure Overhaul - Detailed Implementation Plan

**Status:** Planning Phase  
**Estimated Effort:** 12-16 hours (split over 3-4 days)  
**Risk Level:** HIGH - Major architectural changes  
**Dependencies:** Phases 1.1-1.4, Phase 2 completed  

---

## Executive Summary

Phase 3 transforms the tournament from a single-game knockout structure to a multi-game (4 events), multi-stage (10 groups → finals) tournament with sophisticated tiebreakers. This is the most complex phase requiring careful TDD implementation and thorough testing.

### Current Architecture Analysis

**Current State:**
- **Single GameType** per tournament
- **Dynamic group creation** based on bot count
- **Single-elimination** advancement (winner of each group)
- **Group → Final → Tiebreaker** phase progression
- **1 point per game** win (already correct)

**Target State:**
- **4 GameTypes** per tournament (RPSLS, Blotto, Security, Penalty)
- **10 fixed groups** with random assignment
- **Round-robin** within each group for each game type
- **Aggregate scoring** across all 4 game types
- **Colonel Blotto tiebreakers** at every stage
- **Top 1 from each group** advances to finals (10 finalists)

---

## Architecture Impact Matrix

| Component | Change Type | Complexity | Test Coverage Needed |
|-----------|-------------|------------|---------------------|
| TournamentConfig | Add properties | Low | 5 tests |
| TournamentInfo | Restructure data | Medium | 10 tests |
| GroupStageTournamentEngine | Major refactor | **HIGH** | 30+ tests |
| ScoringSystem | Aggregate logic | Medium | 15 tests |
| TournamentManager | Multi-event orchestration | High | 20 tests |
| API Models | New DTOs | Low | 5 tests |
| Dashboard State | Event tracking | Medium | 10 tests |

**Total New Tests Required:** ~95 tests  
**Existing Tests to Update:** ~35 tests  

---

## Phase 3 Breakdown: 7 Sub-Phases

### Sub-Phase 3.1: Configuration & Models (2 hours)
**Goal:** Update data structures to support multi-game tournaments

### Sub-Phase 3.2: Random Group Assignment (2 hours)
**Goal:** Implement 10-group random distribution

### Sub-Phase 3.3: Multi-Game Execution Loop (3 hours)
**Goal:** Run 4 game types in sequence within each group

### Sub-Phase 3.4: Aggregate Scoring (2 hours)
**Goal:** Combine scores across all 4 game types

### Sub-Phase 3.5: Tiebreaker System (4 hours)
**Goal:** Colonel Blotto single-elimination for any ties

### Sub-Phase 3.6: Finals Stage (2 hours)
**Goal:** Top 10 compete in final round-robin

### Sub-Phase 3.7: Integration & Testing (3 hours)
**Goal:** End-to-end tournament flow validation

---

## Sub-Phase 3.1: Configuration & Models (TDD Guide)

### Current Code Analysis

**TournamentConfig.cs** - Current Properties:
```csharp
public class TournamentConfig
{
    public TimeSpan ImportTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MoveTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public int MaxParallelMatches { get; init; } = 1;
    public int MemoryLimitMB { get; init; } = 512;
    public int MaxRoundsRPSLS { get; init; } = 50;
    public string LogLevel { get; init; } = "INFO";
    public string LogFilePath { get; init; } = "tournament_log.txt";
    public string BotsDirectory { get; init; } = "bots";
    public string ResultsFilePath { get; init; } = "results.json";
}
```

**TournamentInfo.cs** - Current Structure:
```csharp
public class TournamentInfo
{
    public required string TournamentId { get; init; }
    public required GameType GameType { get; init; } // SINGLE GAME
    public required TournamentState State { get; set; }
    public List<BotInfo> Bots { get; init; } = new();
    public List<MatchResult> MatchResults { get; init; } = new();
    public Dictionary<int, List<string>> Bracket { get; init; } = new();
    public string? Champion { get; set; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public int CurrentRound { get; set; }
    public int TotalRounds { get; init; }
}
```

---

### Step 3.1.1: Extend TournamentConfig (30 min)

#### TDD Test First
**File:** `TournamentEngine.Tests/Common/TournamentConfigTests.cs` (NEW)

```csharp
[TestClass]
public class TournamentConfigTests
{
    [TestMethod]
    public void TournamentConfig_DefaultGameTypes_Contains4Games()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(4, config.GameTypes.Count);
        Assert.IsTrue(config.GameTypes.Contains(GameType.RPSLS));
        Assert.IsTrue(config.GameTypes.Contains(GameType.ColonelBlotto));
        Assert.IsTrue(config.GameTypes.Contains(GameType.PenaltyKicks));
        Assert.IsTrue(config.GameTypes.Contains(GameType.SecurityGame));
    }
    
    [TestMethod]
    public void TournamentConfig_DefaultGroupCount_Is10()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(10, config.GroupCount);
    }
    
    [TestMethod]
    public void TournamentConfig_CanCustomizeGameTypes()
    {
        // Arrange & Act
        var config = new TournamentConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto }
        };
        
        // Assert
        Assert.AreEqual(2, config.GameTypes.Count);
    }
    
    [TestMethod]
    public void TournamentConfig_CanCustomizeGroupCount()
    {
        // Arrange & Act
        var config = new TournamentConfig { GroupCount = 5 };
        
        // Assert
        Assert.AreEqual(5, config.GroupCount);
    }
    
    [TestMethod]
    public void TournamentConfig_FinalistsPerGroup_DefaultIs1()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(1, config.FinalistsPerGroup);
    }
}
```

#### Implementation
**File:** `TournamentEngine.Core/Common/TournamentConfig.cs`

```csharp
public class TournamentConfig
{
    // Existing properties...
    public TimeSpan ImportTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MoveTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public int MaxParallelMatches { get; init; } = 1;
    public int MemoryLimitMB { get; init; } = 512;
    public int MaxRoundsRPSLS { get; init; } = 50;
    public string LogLevel { get; init; } = "INFO";
    public string LogFilePath { get; init; } = "tournament_log.txt";
    public string BotsDirectory { get; init; } = "bots";
    public string ResultsFilePath { get; init; } = "results.json";
    
    // NEW: Multi-game tournament properties
    public List<GameType> GameTypes { get; init; } = new List<GameType>
    {
        GameType.RPSLS,
        GameType.ColonelBlotto,
        GameType.PenaltyKicks,
        GameType.SecurityGame
    };
    
    public int GroupCount { get; init; } = 10;
    public int FinalistsPerGroup { get; init; } = 1; // Top N from each group advance
    public bool UseTiebreakers { get; init; } = true;
    public GameType TiebreakerGameType { get; init; } = GameType.ColonelBlotto;
}
```

#### Run Tests
```bash
dotnet test --filter "TournamentConfigTests"
```
**Expected:** All 5 tests pass ✓

---

### Step 3.1.2: Create Multi-Game TournamentInfo (45 min)

#### Decision: Keep Backward Compatibility
**Strategy:** 
- Rename `TournamentInfo` → `EventInfo` (represents single game type)
- Create new `TournamentInfo` (represents full tournament with 4 events)
- Use adapter pattern during transition

#### TDD Test First
**File:** `TournamentEngine.Tests/Common/MultiGameTournamentInfoTests.cs` (NEW)

```csharp
[TestClass]
public class MultiGameTournamentInfoTests
{
    [TestMethod]
    public void TournamentInfo_Initialization_Creates4Events()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        
        // Act
        var tournament = TournamentInfo.Initialize(bots, config);
        
        // Assert
        Assert.AreEqual(4, tournament.Events.Count);
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.RPSLS));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.ColonelBlotto));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.PenaltyKicks));
        Assert.IsTrue(tournament.Events.ContainsKey(GameType.SecurityGame));
    }
    
    [TestMethod]
    public void TournamentInfo_AllEvents_ShareSameBots()
    {
        // Arrange
        var bots = CreateTestBots(20);
        var config = new TournamentConfig();
        
        // Act
        var tournament = TournamentInfo.Initialize(bots, config);
        
        // Assert
        foreach (var eventInfo in tournament.Events.Values)
        {
            Assert.AreEqual(20, eventInfo.Bots.Count);
            CollectionAssert.AreEquivalent(bots, eventInfo.Bots);
        }
    }
    
    [TestMethod]
    public void TournamentInfo_AggregateScores_CombinesAllEvents()
    {
        // Arrange
        var tournament = CreateTournamentWithResults();
        
        // Act
        var aggregateScores = tournament.GetAggregateScores();
        
        // Assert
        Assert.IsTrue(aggregateScores.ContainsKey("Bot1"));
        Assert.IsTrue(aggregateScores["Bot1"] > 0);
        // Score should be sum from all 4 game types
    }
    
    [TestMethod]
    public void TournamentInfo_CurrentEvent_TracksProgress()
    {
        // Arrange
        var tournament = CreateBasicTournament();
        
        // Act
        tournament.Events[GameType.RPSLS].State = TournamentState.Completed;
        
        // Assert
        Assert.AreEqual(GameType.ColonelBlotto, tournament.CurrentEvent);
    }
    
    private List<BotInfo> CreateTestBots(int count) { /* ... */ }
}
```

#### Implementation
**File:** `TournamentEngine.Core/Common/EventInfo.cs` (RENAME from TournamentInfo)

```csharp
namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a single event (game type) within a tournament
/// Previously called TournamentInfo - renamed for clarity
/// </summary>
public class EventInfo
{
    public required string EventId { get; init; }
    public required GameType GameType { get; init; }
    public required TournamentState State { get; set; }
    public List<BotInfo> Bots { get; init; } = new();
    public List<MatchResult> MatchResults { get; init; } = new();
    public Dictionary<int, List<string>> Bracket { get; init; } = new();
    public string? Champion { get; set; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public int CurrentRound { get; set; }
    public int TotalRounds { get; init; }
    
    // NEW: Group structure for multi-group tournaments
    public List<GroupInfo> Groups { get; init; } = new();
}

public class GroupInfo
{
    public required string GroupId { get; init; }
    public List<BotInfo> Bots { get; init; } = new();
    public Dictionary<string, int> Standings { get; init; } = new();
    public bool IsComplete { get; set; }
}
```

**File:** `TournamentEngine.Core/Common/TournamentInfo.cs` (NEW - Multi-event wrapper)

```csharp
namespace TournamentEngine.Core.Common;

/// <summary>
/// Represents a complete multi-game tournament
/// Contains 4 events (one per game type) with aggregate scoring
/// </summary>
public class TournamentInfo
{
    public required string TournamentId { get; init; }
    public required TournamentState State { get; set; }
    public Dictionary<GameType, EventInfo> Events { get; init; } = new();
    public List<BotInfo> RegisteredBots { get; init; } = new();
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public string? Champion { get; set; }
    
    public GameType CurrentEvent => Events
        .Where(e => e.Value.State == TournamentState.InProgress)
        .Select(e => e.Key)
        .FirstOrDefault();
    
    public Dictionary<string, int> GetAggregateScores()
    {
        var scores = new Dictionary<string, int>();
        
        foreach (var eventInfo in Events.Values)
        {
            foreach (var bot in eventInfo.Bots)
            {
                if (!scores.ContainsKey(bot.TeamName))
                    scores[bot.TeamName] = 0;
                
                // Add points from this event
                var botPoints = CalculateEventPoints(eventInfo, bot.TeamName);
                scores[bot.TeamName] += botPoints;
            }
        }
        
        return scores;
    }
    
    private int CalculateEventPoints(EventInfo eventInfo, string botName)
    {
        // Count wins from MatchResults
        return eventInfo.MatchResults
            .Count(m => m.WinnerName == botName);
    }
    
    public static TournamentInfo Initialize(List<BotInfo> bots, TournamentConfig config)
    {
        var tournamentId = Guid.NewGuid().ToString();
        var tournament = new TournamentInfo
        {
            TournamentId = tournamentId,
            State = TournamentState.Pending,
            RegisteredBots = bots,
            StartTime = DateTime.UtcNow
        };
        
        // Create event for each game type
        foreach (var gameType in config.GameTypes)
        {
            tournament.Events[gameType] = new EventInfo
            {
                EventId = $"{tournamentId}-{gameType}",
                GameType = gameType,
                State = TournamentState.Pending,
                Bots = new List<BotInfo>(bots),
                StartTime = DateTime.UtcNow,
                CurrentRound = 0,
                TotalRounds = config.GroupCount + 1 // Groups + Finals
            };
        }
        
        return tournament;
    }
}
```

#### Run Tests
```bash
dotnet test --filter "MultiGameTournamentInfoTests"
```
**Expected:** All 4 tests pass ✓

---

## Sub-Phase 3.2: Random Group Assignment (TDD Guide)

### Current Implementation Analysis

**File:** `GroupStageTournamentEngine.cs` - Line 579

```csharp
internal List<Group> CreateInitialGroups(List<IBot> bots)
{
    // Current: Creates pow-of-2 groups dynamically based on bot count
    // Example: 16 bots → 4 groups of 4
    //          32 bots → 8 groups of 4
    //          20 bots → 5 groups of 4
    
    var groupCount = CalculateOptimalGroupCount(bots.Count);
    // ... pow-of-2 logic
}
```

**Target:** Always create exactly 10 groups with random assignment, regardless of bot count.

---

### Step 3.2.1: Group Assignment Algorithm (1 hour)

#### TDD Test First
**File:** `TournamentEngine.Tests/Tournament/GroupAssignmentTests.cs` (NEW)

```csharp
[TestClass]
public class GroupAssignmentTests
{
    [TestMethod]
    public void CreateGroups_100Bots_Creates10Groups()
    {
        // Arrange
        var bots = CreateTestBots(100);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        Assert.AreEqual(10, groups.Count);
    }
    
    [TestMethod]
    public void CreateGroups_100Bots_BalancedDistribution()
    {
        // Arrange
        var bots = CreateTestBots(100);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        foreach (var group in groups)
        {
            Assert.AreEqual(10, group.Bots.Count); // 100/10 = 10 per group
        }
    }
    
    [TestMethod]
    public void CreateGroups_75Bots_HandlesNonEvenSplit()
    {
        // Arrange
        var bots = CreateTestBots(75);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        Assert.AreEqual(10, groups.Count);
        
        // 75 bots / 10 groups = 7-8 per group
        var groupSizes = groups.Select(g => g.Bots.Count).ToList();
        Assert.IsTrue(groupSizes.All(size => size >= 7 && size <= 8));
        Assert.AreEqual(75, groupSizes.Sum()); // Total still 75
    }
    
    [TestMethod]
    public void CreateGroups_MultipleRuns_ProducesRandomDistribution()
    {
        // Arrange
        var bots = CreateTestBots(30);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act - Run 5 times
        var assignments = new List<Dictionary<string, string>>();
        for (int i = 0; i < 5; i++)
        {
            var groups = engine.CreateInitialGroups(bots, config);
            var assignment = new Dictionary<string, string>();
            
            foreach (var group in groups)
            {
                foreach (var bot in group.Bots)
                {
                    assignment[bot.TeamName] = group.GroupId;
                }
            }
            assignments.Add(assignment);
        }
        
        // Assert - At least one bot should be in different groups across runs
        bool foundDifference = false;
        var firstBot = bots[0].TeamName;
        
        var groupsForFirstBot = assignments.Select(a => a[firstBot]).Distinct().ToList();
        foundDifference = groupsForFirstBot.Count > 1;
        
        Assert.IsTrue(foundDifference, "Group assignment should be randomized");
    }
    
    [TestMethod]
    public void CreateGroups_AllBotsAssigned_NoDuplicates()
    {
        // Arrange
        var bots = CreateTestBots(50);
        var config = new TournamentConfig { GroupCount = 10 };
        var engine = new GroupStageTournamentEngine(null, null);
        
        // Act
        var groups = engine.CreateInitialGroups(bots, config);
        
        // Assert
        var allAssignedBots = groups.SelectMany(g => g.Bots).ToList();
        Assert.AreEqual(50, allAssignedBots.Count);
        
        var uniqueBots = allAssignedBots.Select(b => b.TeamName).Distinct().ToList();
        Assert.AreEqual(50, uniqueBots.Count, "No duplicates allowed");
    }
}
```

#### Implementation
**File:** `TournamentEngine.Core/Tournament/GroupStageTournamentEngine.cs`

**Update method signature:**
```csharp
// OLD
internal List<Group> CreateInitialGroups(List<IBot> bots)

// NEW
internal List<Group> CreateInitialGroups(List<IBot> bots, TournamentConfig config)
```

**New implementation:**
```csharp
internal List<Group> CreateInitialGroups(List<IBot> bots, TournamentConfig config)
{
    if (bots == null || bots.Count < config.GroupCount)
        throw new ArgumentException($"Need at least {config.GroupCount} bots for {config.GroupCount} groups");
    
    // Shuffle bots randomly
    var random = new Random();
    var shuffledBots = bots.OrderBy(x => random.Next()).ToList();
    
    // Calculate group sizes (handle non-even splits)
    int botsPerGroup = (int)Math.Ceiling((double)bots.Count / config.GroupCount);
    
    var groups = new List<Group>();
    
    for (int i = 0; i < config.GroupCount; i++)
    {
        var groupBots = shuffledBots
            .Skip(i * botsPerGroup)
            .Take(botsPerGroup)
            .ToList();
        
        if (groupBots.Count == 0)
            continue; // Skip empty groups if bots < GroupCount
        
        var group = new Group
        {
            GroupId = $"Group-{i + 1}",
            Bots = groupBots,
            Standings = new Dictionary<string, GroupStanding>(),
            IsComplete = false
        };
        
        // Initialize standings for each bot
        foreach (var bot in groupBots)
        {
            group.Standings[bot.TeamName] = new GroupStanding
            {
                BotName = bot.TeamName,
                Points = 0,
                Wins = 0,
                Losses = 0,
                Draws = 0,
                GoalDifferential = 0
            };
        }
        
        groups.Add(group);
    }
    
    Log($"Created {groups.Count} groups with {string.Join(", ", groups.Select(g => g.Bots.Count))} bots each");
    
    return groups;
}
```

#### Update Callers
**Find all calls to `CreateInitialGroups`:**
```csharp
// Line 79 - Update to pass config
_currentGroups = CreateInitialGroups(botAdapters, config);
```

#### Run Tests
```bash
dotnet test --filter "GroupAssignmentTests"
```
**Expected:** All 5 tests pass ✓

---

## Sub-Phase 3.3: Multi-Game Execution Loop (TDD Guide)

### Complexity Analysis
**Current:** Tournament runs single game type  
**Target:** Tournament runs 4 game types sequentially, aggregates results

**Execution Flow:**
```
For each group (1-10):
    For each game type (RPSLS, Blotto, Security, Penalty):
        Run round-robin matches within group
        Update group standings
    Calculate aggregate scores across all 4 games
    Determine group winner(s)
```

---

### Step 3.3.1: Multi-Game Round-Robin (2 hours)

#### TDD Test First
**File:** `TournamentEngine.Tests/Tournament/MultiGameExecutionTests.cs` (NEW)

```csharp
[TestClass]
public class MultiGameExecutionTests
{
    [TestMethod]
    public async Task Tournament_Runs4GameTypes_InSequence()
    {
        // Arrange
        var bots = CreateTestBots(30);
        var config = new TournamentConfig(); // 4 game types
        var gameRunner = new MockGameRunner();
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        
        // Act
        var tournament = engine.InitializeTournament(bots, config);
        
        // Execute all matches
        while (engine.GetNextMatches().Any())
        {
            var matches = engine.GetNextMatches();
            foreach (var (bot1, bot2) in matches)
            {
                var result = await gameRunner.ExecuteMatch(bot1, bot2, config, CancellationToken.None);
                engine.RecordMatchResult(result);
            }
        }
        
        // Assert
        Assert.AreEqual(4, tournament.Events.Count);
        
        foreach (var eventInfo in tournament.Events.Values)
        {
            Assert.AreEqual(TournamentState.Completed, eventInfo.State);
            Assert.IsTrue(eventInfo.MatchResults.Count > 0);
        }
    }
    
    [TestMethod]
    public async Task Group_WithNBots_PlaysRoundRobinForEachGame()
    {
        // Arrange: Group with 5 bots
        var bots = CreateTestBots(5);
        var config = new TournamentConfig();
        var engine = new GroupStageTournamentEngine(/* ... */);
        
        // Act
        var matches = engine.GenerateGroupMatches(bots, config);
        
        // Assert
        // Round-robin for 5 bots = C(5,2) = 10 matches
        // 4 game types × 10 matches = 40 total matches
        Assert.AreEqual(40, matches.Count);
        
        var matchesByGameType = matches.GroupBy(m => m.GameType).ToList();
        Assert.AreEqual(4, matchesByGameType.Count);
        
        foreach (var gameGroup in matchesByGameType)
        {
            Assert.AreEqual(10, gameGroup.Count());
        }
    }
    
    [TestMethod]
    public void MultiGame_Standings_AggregatesAcrossGameTypes()
    {
        // Arrange
        var group = CreateGroupWithResults();
        
        // Simulate:
        // Bot1 wins 3/3 in RPSLS
        // Bot1 wins 2/3 in Blotto
        // Bot1 wins 1/3 in Penalty
        // Bot1 wins 3/3 in Security
        // Total: 9 wins
        
        // Act
        var standings = CalculateAggregateStandings(group);
        
        // Assert
        Assert.AreEqual(9, standings["Bot1"].Points);
    }
}
```

#### Implementation Strategy

**Option A: Sequential Execution** (Simpler, recommended for Phase 3)
```csharp
foreach (var gameType in config.GameTypes)
{
    foreach (var group in groups)
    {
        var matches = GenerateRoundRobinMatches(group, gameType);
        foreach (var match in matches)
        {
            var result = await ExecuteMatch(match);
            RecordResult(result, gameType);
        }
    }
}
```

**Option B: Parallel Execution** (More complex, future optimization)
```csharp
var allMatches = config.GameTypes
    .SelectMany(gameType => groups.SelectMany(group => GenerateMatches(group, gameType)))
    .ToList();
    
await Parallel.ForEachAsync(allMatches, async (match, ct) => {
    var result = await ExecuteMatch(match, ct);
    RecordResult(result);
});
```

**Decision:** Start with Option A (sequential) for predictability and easier debugging.

---

### Step 3.3.2: Update GroupStageTournamentEngine.InitializeTournament()

**File:** `TournamentEngine.Core/Tournament/GroupStageTournamentEngine.cs`

**Current signature:**
```csharp
public TournamentInfo InitializeTournament(List<BotInfo> bots, GameType gameType, TournamentConfig config)
```

**NEW signature (breaking change):**
```csharp
public TournamentInfo InitializeTournament(List<BotInfo> bots, TournamentConfig config)
// gameType parameter removed - now uses config.GameTypes
```

**Implementation:**
```csharp
public TournamentInfo InitializeTournament(List<BotInfo> bots, TournamentConfig config)
{
    if (bots == null || bots.Count < config.GroupCount)
        throw new ArgumentException($"At least {config.GroupCount} bots required", nameof(bots));
    if (config == null)
        throw new ArgumentNullException(nameof(config));

    lock (_stateLock)
    {
        var tournamentId = Guid.NewGuid().ToString();
        _tournamentInfo = TournamentInfo.Initialize(bots, config);
        _tournamentInfo.State = TournamentState.InProgress;
        
        // Create groups
        var botAdapters = CreateBotAdapters(bots, config.GameTypes[0]); // Use first game for initialization
        _currentGroups = CreateInitialGroups(botAdapters, config);
        _groupStandings = BuildStandingsIndex(_currentGroups);
        
        // Generate ALL matches for ALL game types
        var allMatches = new List<(IBot bot1, IBot bot2, GameType gameType)>();
        
        foreach (var gameType in config.GameTypes)
        {
            var gameMatches = GenerateAllGroupMatches(_currentGroups, gameType);
            allMatches.AddRange(gameMatches.Select(m => (m.bot1, m.bot2, gameType)));
        }
        
        _pendingMatches = new Queue<(IBot bot1, IBot bot2, GameType gameType)>(allMatches);
        _currentPhaseExpectedMatches = allMatches.Count;
        _currentPhaseRecordedResults = 0;
        _currentPhase = TournamentPhase.InitialGroups;
        
        Log($"Initialized multi-game tournament: {config.GameTypes.Count} games × {_currentGroups.Count} groups = {allMatches.Count} matches");
        
        return _tournamentInfo;
    }
}
```

---

## Sub-Phase 3.4: Aggregate Scoring (TDD Guide)

### Step 3.4.1: Scoring Across Game Types (1.5 hours)

#### TDD Test First
**File:** `TournamentEngine.Tests/Scoring/AggregrateScoringTests.cs`

```csharp
[TestClass]
public class AggregateScoringTests
{
    [TestMethod]
    public void Bot_Wins3Of4Games_Gets3Points()
    {
        // Arrange
        var results = new Dictionary<GameType, string>
        {
            { GameType.RPSLS, "Bot1" },          // Win
            { GameType.ColonelBlotto, "Bot1" },  // Win
            { GameType.PenaltyKicks, "Bot2" },   // Loss
            { GameType.SecurityGame, "Bot1" }    // Win
        };
        
        // Act
        var points = CalculateAggregatePoints("Bot1", results);
        
        // Assert
        Assert.AreEqual(3, points);
    }
    
    [TestMethod]
    public void Group_AfterAll4Games_RankedByTotalWins()
    {
        // Arrange
        var group = CreateGroupWith5Bots();
        SimulateAllGameTypes(group);
        
        // Bot1: 4 wins (1 per game type)
        // Bot2: 3 wins total
        // Bot3: 2 wins total
        // Bot4: 1 win total
        // Bot5: 0 wins
        
        // Act
        var rankedBots = RankBotsByAggregateScore(group);
        
        // Assert
        Assert.AreEqual("Bot1", rankedBots[0].BotName);
        Assert.AreEqual("Bot2", rankedBots[1].BotName);
        Assert.AreEqual("Bot3", rankedBots[2].BotName);
    }
}
```

#### Implementation
**File:** `TournamentEngine.Core/Scoring/ScoringSystem.cs` (extend)

```csharp
public class ScoringSystem : IScoringSystem
{
    // Existing single-game methods...
    
    // NEW: Multi-game aggregate scoring
    public Dictionary<string, int> CalculateAggregateScores(
        Dictionary<GameType, List<MatchResult>> resultsByGame)
    {
        var aggregateScores = new Dictionary<string, int>();
        
        foreach (var (gameType, matchResults) in resultsByGame)
        {
            foreach (var matchResult in matchResults)
            {
                // Award 1 point per game win
                if (matchResult.Outcome == MatchOutcome.Player1Wins)
                {
                    AddPoints(aggregateScores, matchResult.Bot1Name, 1);
                }
                else if (matchResult.Outcome == MatchOutcome.Player2Wins)
                {
                    AddPoints(aggregateScores, matchResult.Bot2Name, 1);
                }
                // Draws: no points awarded
            }
        }
        
        return aggregateScores;
    }
    
    private void AddPoints(Dictionary<string, int> scores, string botName, int points)
    {
        if (!scores.ContainsKey(botName))
            scores[botName] = 0;
        scores[botName] += points;
    }
    
    public List<(string BotName, int Points)> RankBots(Dictionary<string, int> scores)
    {
        return scores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
}
```

---

## Sub-Phase 3.5: Tiebreaker System (TDD Guide) - MOST COMPLEX

### Architecture Decision

**Tiebreaker Scenarios:**
1. **Group stage ties:** Multiple bots with same aggregate score
2. **Final stage ties:** Top finalists tied for champion

**Tiebreaker Game:** Colonel Blotto (strategic, decisive, no randomness once deployed)

**Tiebreaker Format:** Single-elimination bracket
- 2-way tie: 1 match (winner advances)
- 3-way tie: 3 matches (bracket of 4, 1 bye)
- 4-way tie: 3 matches (full bracket)
- 5+ way tie: Build full elimination tree

---

### Step 3.5.1: Detect Ties (30 min)

#### TDD Test First
**File:** `TournamentEngine.Tests/Tournament/TiebreakerDetectionTests.cs`

```csharp
[TestClass]
public class TiebreakerDetectionTests
{
    [TestMethod]
    public void DetectTies_2BotsWithSameScore_ReturnsTie()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 10 },
            { "Bot2", 10 },
            { "Bot3", 5 }
        };
        
        // Act
        var ties = DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count);
        Assert.AreEqual(2, ties[0].Count);
        CollectionAssert.Contains(ties[0], "Bot1");
        CollectionAssert.Contains(ties[0], "Bot2");
    }
    
    [TestMethod]
    public void DetectTies_3WayTie_Returns3BotGroup()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 12 },
            { "Bot2", 12 },
            { "Bot3", 12 },
            { "Bot4", 8 }
        };
        
        // Act
        var ties = DetectTies(standings);
        
        // Assert
        Assert.AreEqual(1, ties.Count);
        Assert.AreEqual(3, ties[0].Count);
    }
    
    [TestMethod]
    public void DetectTies_NoTies_ReturnsEmpty()
    {
        // Arrange
        var standings = new Dictionary<string, int>
        {
            { "Bot1", 15 },
            { "Bot2", 10 },
            { "Bot3", 5 }
        };
        
        // Act
        var ties = DetectTies(standings);
        
        // Assert
        Assert.AreEqual(0, ties.Count);
    }
}
```

---

### Step 3.5.2: Build Elimination Bracket (1.5 hours)

#### TDD Test First
**File:** `TournamentEngine.Tests/Tournament/TiebreakerBracketTests.cs`

```csharp
[TestClass]
public class TiebreakerBracketTests
{
    [TestMethod]
    public void BuildBracket_2Bots_Creates1Match()
    {
        // Arrange
        var tiedBots = new List<string> { "Bot1", "Bot2" };
        
        // Act
        var bracket = BuildEliminationBracket(tiedBots);
        
        // Assert
        Assert.AreEqual(1, bracket.TotalMatches);
        Assert.AreEqual(1, bracket.Rounds.Count);
        Assert.AreEqual(1, bracket.Rounds[0].Matches.Count);
    }
    
    [TestMethod]
    public void BuildBracket_4Bots_Creates3Matches()
    {
        // Arrange
        var tiedBots = new List<string> { "Bot1", "Bot2", "Bot3", "Bot4" };
        
        // Act
        var bracket = BuildEliminationBracket(tiedBots);
        
        // Assert
        // Round 1: 2 matches (semifinals)
        // Round 2: 1 match (final)
        Assert.AreEqual(3, bracket.TotalMatches);
        Assert.AreEqual(2, bracket.Rounds.Count);
        Assert.AreEqual(2, bracket.Rounds[0].Matches.Count); // Semis
        Assert.AreEqual(1, bracket.Rounds[1].Matches.Count); // Final
    }
    
    [TestMethod]
    public void BuildBracket_3Bots_HandlesOddNumber()
    {
        // Arrange
        var tiedBots = new List<string> { "Bot1", "Bot2", "Bot3" };
        
        // Act
        var bracket = BuildEliminationBracket(tiedBots);
        
        // Assert
        // Give highest-seeded bot a bye
        // Round 1: 1 match (Bot2 vs Bot3)
        // Round 2: 1 match (winner vs Bot1)
        Assert.AreEqual(2, bracket.TotalMatches);
        Assert.IsTrue(bracket.HasByes);
    }
}
```

#### Implementation
**File:** `TournamentEngine.Core/Tournament/TiebreakerService.cs` (NEW)

```csharp
public class TiebreakerService
{
    private readonly IGameRunner _gameRunner;
    private readonly TournamentConfig _config;
    
    public async Task<string> ResolveTie(
        List<string> tiedBotNames,
        Dictionary<string, IBot> botLookup,
        CancellationToken cancellationToken)
    {
        if (tied BotNames.Count < 2)
            throw new ArgumentException("Need at least 2 bots for tiebreaker");
        
        if (tiedBotNames.Count == 2)
        {
            // Simple 1v1 match
            return await ExecuteSingleMatch(
                botLookup[tiedBotNames[0]], 
                botLookup[tiedBotNames[1]], 
                cancellationToken);
        }
        
        // Build elimination bracket
        var bracket = BuildEliminationBracket(tiedBotNames);
        
        // Execute bracket rounds
        foreach (var round in bracket.Rounds)
        {
            foreach (var match in round.Matches)
            {
                if (match.HasBye)
                {
                    match.Winner = match.Bot1; // Auto-advance
                    continue;
                }
                
                var result = await _gameRunner.ExecuteMatch(
                    botLookup[match.Bot1],
                    botLookup[match.Bot2],
                    _config.TiebreakerGameType,
                    _config,
                    cancellationToken);
                
                match.Winner = result.WinnerName;
            }
        }
        
        // Return final winner
        return bracket.Champion;
    }
    
    private EliminationBracket BuildEliminationBracket(List<string> bots)
    {
        // Seed bots (could use current standings for seeding)
        var seededBots = new List<string>(bots);
        
        // Calculate bracket size (next power of 2)
        int bracketSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(bots.Count, 2)));
        int byes = bracketSize - bots.Count;
        
        var bracket = new EliminationBracket
        {
            TotalBots = bots.Count,
            BracketSize = bracketSize,
            HasByes = byes > 0,
            Rounds = new List<BracketRound>()
        };
        
        // Round 1
        var round1Matches = new List<BracketMatch>();
        
        for (int i = 0; i < bots.Count; i += 2)
        {
            if (i + 1 < bots.Count)
            {
                // Normal pairing
                round1Matches.Add(new BracketMatch
                {
                    Bot1 = bots[i],
                    Bot2 = bots[i + 1],
                    HasBye = false
                });
            }
            else
            {
                // Odd number - give bye
                round1Matches.Add(new BracketMatch
                {
                    Bot1 = bots[i],
                    Bot2 = null,
                    HasBye = true
                });
            }
        }
        
        bracket.Rounds.Add(new BracketRound { Matches = round1Matches });
        
        // Build subsequent rounds
        var previousRoundWinners = round1Matches.Count;
        int roundNum = 2;
        
        while (previousRoundWinners > 1)
        {
            var nextRound = new BracketRound
            {
                Matches = Enumerable.Range(0, previousRoundWinners / 2)
                    .Select(_ => new BracketMatch { HasBye = false })
                    .ToList()
            };
            
            bracket.Rounds.Add(nextRound);
            previousRoundWinners /= 2;
            roundNum++;
        }
        
        return bracket;
    }
}

public class EliminationBracket
{
    public int TotalBots { get; set; }
    public int BracketSize { get; set; }
    public bool HasByes { get; set; }
    public List<BracketRound> Rounds { get; set; } = new();
    public int TotalMatches => Rounds.Sum(r => r.Matches.Count);
    public string? Champion => Rounds.Last().Matches.First().Winner;
}

public class BracketRound
{
    public List<BracketMatch> Matches { get; set; } = new();
}

public class BracketMatch
{
    public required string Bot1 { get; set; }
    public string? Bot2 { get; set; }
    public bool HasBye { get; set; }
    public string? Winner { get; set; }
}
```

---

## Sub-Phase 3.6: Finals Stage (TDD Guide)

### Step 3.6.1: Advance Top from Each Group (1 hour)

#### TDD Test First
**File:** `TournamentEngine.Tests/Tournament/AdvancementTests.cs`

```csharp
[TestClass]
public class AdvancementTests
{
    [TestMethod]
    public void SelectFinalists_10Groups_Returns10Bots()
    {
        // Arrange
        var groups = CreateGroups(10, botsPerGroup: 8);
        foreach (var group in groups)
        {
            SimulateMatches(group); // Create standings
        }
        
        var config = new TournamentConfig { FinalistsPerGroup = 1 };
        
        // Act
        var finalists = SelectFinalists(groups, config);
        
        // Assert
        Assert.AreEqual(10, finalists.Count);
    }
    
    [TestMethod]
    public void SelectFinalists_TopBotFromEachGroup_Advances()
    {
        // Arrange
        var groups = CreateGroups(3, botsPerGroup: 5);
        groups[0].Standings["Bot1"].Points = 20; // Group 1 winner
        groups[1].Standings["Bot6"].Points = 18; // Group 2 winner
        groups[2].Standings["Bot11"].Points = 22; // Group 3 winner
        
        // Act
        var finalists = SelectFinalists(groups, new TournamentConfig());
        
        // Assert
        Assert.IsTrue(finalists.Any(b => b.TeamName == "Bot1"));
        Assert.IsTrue(finalists.Any(b => b.TeamName == "Bot6"));
        Assert.IsTrue(finalists.Any(b => b.TeamName == "Bot11"));
    }
    
    [TestMethod]
    public void SelectFinalists_TieInGroup_RunsTiebreaker()
    {
        // Arrange
        var groups = CreateGroups(1, botsPerGroup: 5);
        groups[0].Standings["Bot1"].Points = 15; // Tied for 1st
        groups[0].Standings["Bot2"].Points = 15; // Tied for 1st
        groups[0].Standings["Bot3"].Points = 10;
        
        var tiebreakerService = new Mock<ITiebreakerService>();
        tiebreakerService
            .Setup(t => t.ResolveTie(It.IsAny<List<string>>(), It.IsAny<Dictionary<string, IBot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Bot1");
        
        // Act
        var finalists = SelectFinalists(groups, new TournamentConfig(), tiebreakerService.Object);
        
        // Assert
        Assert.AreEqual(1, finalists.Count);
        Assert.AreEqual("Bot1", finalists[0].TeamName);
        tiebreakerService.Verify(t => t.ResolveTie(It.IsAny<List<string>>(), It.IsAny<Dictionary<string, IBot>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

### Step 3.6.2: Finals Round-Robin (1 hour)

#### TDD Test First
```csharp
[TestMethod]
public async Task Finals_10Bots_PlayRoundRobinAll4Games()
{
    // Arrange
    var finalists = CreateTestBots(10);
    var config = new TournamentConfig();
    
    // Act
    var finalMatches = GenerateFinalMatches(finalists, config);
    
    // Assert
    // C(10, 2) = 45 unique pairings
    // 45 pairings × 4 game types = 180 matches
    Assert.AreEqual(180, finalMatches.Count);
    
    var byGameType = finalMatches.GroupBy(m => m.GameType).ToList();
    Assert.AreEqual(4, byGameType.Count);
    foreach (var gameGroup in byGameType)
    {
        Assert.AreEqual(45, gameGroup.Count());
    }
}

[TestMethod]
public void Finals_AfterAllMatches_DetermineChampion()
{
    // Arrange
    var finalists = CreateTestBots(10);
    var results = SimulateFinalMatches(finalists);
    
    // Assume Bot1 wins most games
    
    // Act
    var champion = DetermineChampion(results);
    
    // Assert
    Assert.IsNotNull(champion);
    Assert.AreEqual("Bot1", champion.TeamName);
}
```

---

## Sub-Phase 3.7: Integration & End-to-End Testing (3 hours)

### Step 3.7.1: Full Tournament Simulation

```csharp
[TestClass]
public class FullMultiGameTournamentTests
{
    [TestMethod]
    public async Task EndToEnd_100Bots_CompletesFullTournament()
    {
        // Arrange
        var bots = CreateTestBots(100);
        var config = new TournamentConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame },
            GroupCount = 10,
            FinalistsPerGroup = 1
        };
        
        var gameRunner = new RealGameRunner(); // Not mocked - full integration
        var scoringSystem = new ScoringSystem();
        var engine = new GroupStageTournamentEngine(gameRunner, scoringSystem);
        
        // Act
        var tournament = engine.InitializeTournament(bots, config);
        
        // Stage 1: Group matches
        while (tournament.State == TournamentState.GroupStage)
        {
            var matches = engine.GetNextMatches();
            foreach (var (bot1, bot2) in matches)
            {
                var result = await gameRunner.ExecuteMatch(bot1, bot2, config, CancellationToken.None);
                engine.RecordMatchResult(result);
            }
            
            if (engine.IsPhaseComplete())
            {
                tournament = engine.AdvanceToNextRound();
            }
        }
        
        // Stage 2: Finals
        while (tournament.State == TournamentState.Finals)
        {
            // ... similar execution
        }
        
        // Assert
        Assert.AreEqual(TournamentState.Completed, tournament.State);
        Assert.IsNotNull(tournament.Champion);
        
        // Verify all 4 game types were played
        foreach (var gameType in config.GameTypes)
        {
            var gamesOfType = tournament.MatchResults
                .Count(m => m.GameType == gameType);
            Assert.IsTrue(gamesOfType > 0, $"No matches played for {gameType}");
        }
        
        // Verify 10 finalists competed
        var finalistsInFinals = tournament.MatchResults
            .Where(m => m.IsFinalsMatch)
            .SelectMany(m => new[] { m.Bot1Name, m.Bot2Name })
            .Distinct()
            .Count();
        Assert.AreEqual(10, finalistsInFinals);
    }
}
```

---

## Implementation Checklist

### Pre-Implementation
- [ ] Review this detailed plan with team
- [ ] Create feature branch: `feature/phase3-multi-game-tournament`
- [ ] Set up test data generators for 10-100 bot scenarios
- [ ] Configure parallel test execution for faster feedback

### Sub-Phase 3.1: Configuration & Models
- [ ] Test: TournamentConfigTests (5 tests)
- [ ] Implement: Extended TournamentConfig
- [ ] Test: MultiGameTournamentInfoTests (4 tests)
- [ ] Implement: EventInfo + new TournamentInfo
- [ ] Run tests, commit: "3.1: Multi-game config and models"

### Sub-Phase 3.2: Random Group Assignment
- [ ] Test: GroupAssignmentTests (5 tests)
- [ ] Implement: Updated CreateInitialGroups()
- [ ] Update all callers
- [ ] Run tests, commit: "3.2: 10-group random assignment"

### Sub-Phase 3.3: Multi-Game Execution
- [ ] Test: MultiGameExecutionTests (3 tests)
- [ ] Implement: Sequential game execution loop
- [ ] Update InitializeTournament signature
- [ ] Run tests, commit: "3.3: Multi-game round-robin execution"

### Sub-Phase 3.4: Aggregate Scoring
- [ ] Test: AggregateScoringTests (2 tests)
- [ ] Implement: CalculateAggregateScores() in ScoringSystem
- [ ] Integrate with group standings
- [ ] Run tests, commit: "3.4: Aggregate scoring across games"

### Sub-Phase 3.5: Tiebreaker System
- [ ] Test: TiebreakerDetectionTests (3 tests)
- [ ] Test: TiebreakerBracketTests (3 tests)
- [ ] Implement: TiebreakerService complete
- [ ] Integrate with group/finals advancement
- [ ] Run tests, commit: "3.5: Colonel Blotto tiebreakers"

### Sub-Phase 3.6: Finals Stage
- [ ] Test: AdvancementTests (3 tests)
- [ ] Implement: SelectFinalists() with tiebreaker support
- [ ] Test: Finals round-robin (2 tests)
- [ ] Implement: Final stage execution
- [ ] Run tests, commit: "3.6: Finals stage with top 10"

### Sub-Phase 3.7: Integration Testing
- [ ] Test: End-to-end tournament (1 large test)
- [ ] Performance test: 100 bots complete in <5 minutes
- [ ] Stress test: Edge cases (11 bots, 200 bots, ties at every stage)
- [ ] Update Dashboard integration
- [ ] Update API endpoints
- [ ] Run ALL tests (626+ expected)
- [ ] Commit: "3.7: Phase 3 integration complete"

---

## Risk Management

### High-Risk Areas

1. **Breaking Changes to Public API**
   - **Risk:** `InitializeTournament()` signature change breaks existing code
   - **Mitigation:** 
     - Create `InitializeTournament_v2()` first
     - Deprecate old version with clear migration guide
     - Update all callers in same commit

2. **Race Conditions in Multi-Game Execution**
   - **Risk:** Parallel execution could corrupt shared state
   - **Mitigation:**
     - Start with sequential execution only
     - Add extensive lock analysis
     - Use immutable data structures where possible

3. **Tiebreaker Infinite Loops**
   - **Risk:** Tie → Blotto match → Draw → Tie again
   - **Mitigation:**
     - Blotto rarely draws (troop allocations differ)
     - Add max iteration limit (3 rematches)
     - Fallback: Lexicographic ordering by bot name

### Medium-Risk Areas

1. **Performance Degradation**
   - **Risk:** 4× matches = 4× time
   - **Mitigation:** 
     - Benchmark before/after
     - Target: <5 min for 100 bots
     - Profile hot paths

2. **Memory Usage Spike**
   - **Risk:** Storing 4× match results
   - **Mitigation:**
     - Stream results to disk
     - Limit in-memory match history

### Low-Risk Areas

1. **Test Maintenance Burden**
   - Tests are additive, not replacing existing
   - Clear naming conventions
   - Shared test utilities

---

## Rollback Strategy

### If Phase 3 Fails Midway

1. **Revert to Main Branch**
   ```bash
   git checkout main
   git merge --no-ff feature/phase3-multi-game-tournament --strategy=ours
   ```

2. **Salvage Completed Sub-Phases**
   - Cherry-pick commits from successful sub-phases
   - Example: Keep 3.1 & 3.2 if 3.5 fails disastrously

3. **Feature Flag Approach** (If partially complete)
   ```csharp
   if (config.EnableMultiGameMode)
   {
       // New Phase 3 code
   }
   else
   {
       // Old single-game code (backward compat)
   }
   ```

---

## Success Metrics

### Functional Criteria
- [x] Phase 1-2 complete (prerequisites)
- [ ] All 95 new tests pass
- [ ] All 626 existing tests still pass
- [ ] 100-bot tournament completes successfully
- [ ] Champion is deterministic (same bots, same seed → same result)

### Performance Criteria
- [ ] 100 bots complete in <5 minutes (currently ~2 min for single game)
- [ ] Memory usage < 1GB peak
- [ ] No test takes >30 seconds

### Quality Criteria
- [ ] Test coverage >85% on new code
- [ ] Zero compiler warnings
- [ ] All public APIs documented
- [ ] Migration guide written

---

## Post-Phase 3 Work

### Immediate Follow-ups
1. Update workshop documentation
2. Create demo tournament with 50 sample bots
3. Dashboard support for multi-game visualization
4. API endpoint updates for Phase 3 structure

### Future Enhancements (Phase 4+)
1. Parallel match execution (performance)
2. Configurable tiebreaker games
3. Swiss-system alternative to round-robin
4. Replay/simulation mode
5. Bot performance analytics per game type

---

## Estimated Timeline

| Sub-Phase | Days | Parallel? | Blockers |
|-----------|------|-----------|----------|
| 3.1 Config/Models | 0.5 | No | None |
| 3.2 Group Assignment | 0.5 | No | 3.1 |
| 3.3 Multi-Game Loop | 1.0 | No | 3.1, 3.2 |
| 3.4 Aggregate Scoring | 0.5 | Yes (with 3.5) | 3.3 |
| 3.5 Tiebreakers | 1.5 | No | 3.4 |
| 3.6 Finals | 0.5 | No | 3.5 |
| 3.7 Integration | 1.0 | No | All |
| **Total** | **5.5 days** | | |

**Contingency:** +2 days (total 7-8 days for Phase 3)

---

## Questions for Review

1. **Approval:** Is this level of detail sufficient to begin implementation?
2. **Prioritization:** Should we split Phase 3 into Phase 3A (multi-game) and Phase 3B (tiebreakers)?
3. **Testing:** Do we need load tests for >100 bots, or is 100 sufficient?
4. **Backward Compat:** Keep old single-game mode or force migration?
5. **Parallel Execution:** Should Sub-Phase 3.3 include parallel matches, or defer to Phase 4?

---

**Ready to proceed? Let's start with Sub-Phase 3.1!**
