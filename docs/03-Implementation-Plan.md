# Tournament Engine C# Implementation Plan

## Overview

Build the Tournament Engine in C# as a console application with clean components, strong typing, and safe execution. Target .NET 8, MSTest for tests, and clear separation of concerns.

## Implementation Progress

**Overall Status:** 7/14 steps completed, 3 partially completed, 4 not started

- ✅ **Completed (7):** Steps 1, 2, 3, 5, 6, 9, 11
- ⏳ **Partial (3):** Steps 4, 8, 14  
- ❌ **Not Started (4):** Steps 7, 10, 12, 13

**Latest Achievement:** Multi-Tournament Orchestrator (Step 9) with comprehensive thread-safety documentation and 124 passing tests.

---

## Implementation Steps

### ✅ 1. Create Solution/Projects

**Status:** COMPLETED

**Projects:**
- `TournamentEngine.sln` - Solution file
- `TournamentEngine.Core/` - Core business logic
- `TournamentEngine.Console/` - CLI application
- `TournamentEngine.Tests/` - Unit and integration tests

**Configuration:**
- Set `TargetFramework` to `net8.0` via `Directory.Build.props`

---

### ✅ 2. Define Shared Contracts

**Status:** COMPLETED

**Location:** `TournamentEngine.Core/Common/`

**Contracts to define:**
- Bot interfaces and implementations
- MatchResult data structures
- TournamentState enums
- Custom exceptions (BotLoadException, BotExecutionException, etc.)

---

### ✅ 3. Provide Sample Bots and Tests

**Status:** COMPLETED

**Location:** `TournamentEngine.Tests/`

**Test Framework:** MSTest

**Test Coverage:**
- Bot loader validations
- RPSLS happy path
- Bracket advancement

**Dummy Bots:**
- Location: `TournamentEngine.Tests/DummyBots/`
- Purpose: Testing and validation

---

### ⏳ 4. Implement Game Runner

**Status:** PARTIALLY COMPLETED (MockGameRunner for testing only)

**Location:** `TournamentEngine.Core/GameRunner/GameRunner.cs`

**Features:**
- RPSLS full implementation
- Blotto validation
- Penalty/Security placeholders

**Timeout Enforcement:**
- **Option A:** `Task` + cancellation for soft limits
- **Option B:** External process + Job Objects for CPU/memory limits

---

### ✅ 5. Implement Tournament Manager

**Status:** COMPLETED (TournamentManager + GroupStageTournamentEngine)

**Location:** `TournamentEngine.Core/Tournament/TournamentManager.cs`

**Responsibilities:**
- Create group assignments (initial phase: ~N/10 groups)
- Run round-robin matches within groups
- Track group standings and points
- Determine group winners
- Create final group from winners
- Run final group round-robin
- Handle tiebreakers if needed
- Determine champion
- Group and standings summary DTOs

---

### ✅ 6. Implement Scoring System

**Status:** COMPLETED

**Location:** `TournamentEngine.Core/Scoring/ScoringSystem.cs`

**Features:**
- Statistics tracking
- Elimination rounds
- Rankings generation
- Summaries

---

### ❌ 7. Implement Output/Display

**Status:** NOT STARTED

**Location:** `TournamentEngine.Console/Display/ConsoleDisplay.cs`

**Features:**
- Round headers
- Match summaries
- Bracket view
- Final rankings
- Statistics display
- Structured logging

---

### ⏳ 8. Implement CLI Entrypoint

**Status:** SKELETON ONLY (TODO)

**Location:** `TournamentEngine.Console/Program.cs`

**Responsibilities:**
- Load `appsettings.json`
- Orchestrate bot loading
- Create bracket
- Run rounds
- Save results to JSON

---

### ✅ 9. Implement Multi-Tournament Orchestrator

**Status:** COMPLETED (All 8 phases + integration tests)

**Location:** `TournamentEngine.Core/Tournament/TournamentSeriesManager.cs`

**Responsibilities:**
- Run multiple tournaments sequentially
- Each tournament uses existing `TournamentManager` logic
- Support per-round game selection (same or different games)
- Aggregate overall bot scoring across all games
- Produce a combined series summary

**Notes:**
- Keep per-tournament results independent
- Provide series-level standings and statistics

---

### ❌ 10. Implement Minimal Game Modules

**Status:** NOT STARTED

**Location:** `TournamentEngine.Core/Games/`

**Files:**
- `Rpsls.cs` - Rock Paper Scissors Lizard Spock
- `Blotto.cs` - Colonel Blotto
- `Penalty.cs` - Penalty Kicks
- `Security.cs` - Security Game

**Contents:**
- Interfaces
- Constants
- Helper methods consumed by Game Runner

---

### ✅ 11. Add Parallel Match Execution

**Status:** COMPLETED (Thread-safe with extensive testing)

**Location:**
- `TournamentEngine.Core/Tournament/TournamentManager.cs`
- `TournamentEngine.Core/Tournament/TournamentSeriesManager.cs`
- `TournamentEngine.Core/Scoring/ScoringSystem.cs`

**Responsibilities:**
- Execute matches in parallel per round/phase
- Ensure deterministic aggregation of results
- Keep scoring and standings updates thread-safe
- Limit degree of parallelism
- Preserve cancellation behavior

**Notes:**
- Collect results concurrently, then apply updates in a single-threaded pass
- Avoid shared mutable state during match execution

---

### ❌ 12. Implement Bot Loader

**Status:** NOT STARTED

**Location:** `TournamentEngine.Core/BotLoader/BotLoader.cs`

**Implementation Options:**
- **Option A:** Roslyn scripting
- **Option B:** External process isolation

**Features:**
- Enforce allowed/blocked namespaces
- Size limits
- Signature checks
- Batch team load

---

### ❌ 13. Optional API Skeleton

**Status:** NOT STARTED (Optional)

**Location:** `TournamentEngine.Api/`

**Stack:** ASP.NET Core minimal API

**Features:**
- Endpoints
- Request/response schemas
- API key auth
- Rate limiting middleware

**Status:** Optional for POC

---

### ⏳ 14. Add Documentation and Configuration

**Status:** PARTIALLY COMPLETED (Plan docs exist, appsettings.json missing)

**Documentation:**
- `README.md` - Project overview and usage

**Configuration:**
- `TournamentEngine.Console/appsettings.json`
  - Timeouts
  - Resource limits
  - Logging settings
  - Game settings

---

## Further Considerations

### 1. Bot Sandboxing

**Option A:** `Task` + cancellation for soft limits
- Pros: Simple, built-in .NET features
- Cons: Not strict resource control

**Option B:** External process with Windows Job Objects for CPU/memory
- Pros: Strong resource isolation
- Cons: More complex implementation

**Option C:** Container isolation for stricter sandboxing
- Pros: Full isolation
- Cons: Requires Docker/container infrastructure

---

### 2. Performance

**Baseline:** Sequential execution

**Option A:** Parallelize matches per round via `Task.WhenAll`
- Use degree-of-parallelism to control

**Option B:** Cache bot compilation and minimize logging in hot paths
- Improve throughput for large tournaments

---

### 3. Penalty/Security Rules

**Approach:**
- Implement minimal viable rules
- OR defer with clear TODOs and stable interfaces
- Ensure interfaces are stable for future implementation

---

## Dependencies

### NuGet Packages

- MSTest framework (testing)
- Microsoft.Extensions.Configuration (appsettings.json)
- Microsoft.Extensions.Logging (structured logging)
- Newtonsoft.Json or System.Text.Json (JSON serialization)
- Microsoft.CodeAnalysis.CSharp.Scripting (Roslyn for bot loading)

---

## Project References

```
TournamentEngine.Console
  └─> TournamentEngine.Core

TournamentEngine.Tests
  └─> TournamentEngine.Core
```

---

## Success Criteria

### Completed ✅
- ✅ Core tournament engine architecture implemented (TournamentManager, GroupStageTournamentEngine)
- ✅ Scoring system with statistics and rankings implemented
- ✅ Multi-tournament orchestrator (TournamentSeriesManager) with series aggregation
- ✅ Parallel match execution with thread safety
- ✅ Tournament bracket correctly handles variable bot counts (group stage + final group)
- ✅ Comprehensive test suite: 124 tests passing (unit + integration)
- ✅ Error handling with custom exceptions
- ✅ Thread-safe concurrent match execution
- ✅ Cancellation token support throughout

### In Progress ⏳
- ⏳ Console application entrypoint (skeleton exists)
- ⏳ Configuration management (appsettings.json)
- ⏳ Real game runner implementation

### Not Started ❌
- ❌ Bot loader with sandboxing
- ❌ Game modules (RPSLS, Blotto, Penalty, Security implementations)
- ❌ Console display/output formatting
- ❌ Results export to JSON
- ❌ Bot timeout enforcement in real game execution
