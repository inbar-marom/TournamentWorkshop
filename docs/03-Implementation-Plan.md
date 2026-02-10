# Tournament Engine C# Implementation Plan

## Overview

Build the Tournament Engine in C# as a console application with clean components, strong typing, and safe execution. Target .NET 8, MSTest for tests, and clear separation of concerns.

---

## Implementation Steps

### 1. Create Solution/Projects

**Projects:**
- `TournamentEngine.sln` - Solution file
- `TournamentEngine.Core/` - Core business logic
- `TournamentEngine.Console/` - CLI application
- `TournamentEngine.Tests/` - Unit and integration tests

**Configuration:**
- Set `TargetFramework` to `net8.0` via `Directory.Build.props`

---

### 2. Define Shared Contracts

**Location:** `TournamentEngine.Core/Common/`

**Contracts to define:**
- Bot interfaces and implementations
- MatchResult data structures
- TournamentState enums
- Custom exceptions (BotLoadException, BotExecutionException, etc.)

---

### 3. Provide Sample Bots and Tests

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

### 4. Implement Game Runner

**Location:** `TournamentEngine.Core/GameRunner/GameRunner.cs`

**Features:**
- RPSLS full implementation
- Blotto validation
- Penalty/Security placeholders

**Timeout Enforcement:**
- **Option A:** `Task` + cancellation for soft limits
- **Option B:** External process + Job Objects for CPU/memory limits

---

### 5. Implement Tournament Manager

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

### 6. Implement Scoring System

**Location:** `TournamentEngine.Core/Scoring/ScoringSystem.cs`

**Features:**
- Statistics tracking
- Elimination rounds
- Rankings generation
- Summaries

---

### 7. Implement Output/Display

**Location:** `TournamentEngine.Console/Display/ConsoleDisplay.cs`

**Features:**
- Round headers
- Match summaries
- Bracket view
- Final rankings
- Statistics display
- Structured logging

---

### 8. Implement CLI Entrypoint

**Location:** `TournamentEngine.Console/Program.cs`

**Responsibilities:**
- Load `appsettings.json`
- Orchestrate bot loading
- Create bracket
- Run rounds
- Save results to JSON

---

### 9. Implement Minimal Game Modules

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

### 10. Implement Bot Loader

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

### 11. Optional API Skeleton

**Location:** `TournamentEngine.Api/`

**Stack:** ASP.NET Core minimal API

**Features:**
- Endpoints
- Request/response schemas
- API key auth
- Rate limiting middleware

**Status:** Optional for POC

---

### 12. Add Documentation and Configuration

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

- ✅ All core components implemented and tested
- ✅ RPSLS fully functional with 50+ rounds
- ✅ Tournament bracket correctly handles 30-120 bots
- ✅ Byes assigned correctly for odd numbers
- ✅ Results saved to JSON
- ✅ Console output readable and informative
- ✅ Error handling prevents engine crashes
- ✅ Bot timeouts enforced
- ✅ Unit tests pass with >80% coverage on core logic
