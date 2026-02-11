# Step 12: Local Bot Loader - Implementation Plan

## Objective

Implement a local file-based bot loading system that supports multi-file bot compilation, validation, and sandboxing. The system must compile bot code using Roslyn, enforce security restrictions, and support parallel batch loading for performance.

---

## Core Concept

**Local Directory Loading Approach:**
- Load bot folders from local directory (multi-file support)
- Each team can submit multiple .cs files organized in a folder
- Compile all files together using Roslyn
- Validate single IBot implementation exists
- Enforce namespace restrictions across all files
- Populate `BotInfo.BotInstance` with compiled bot
- Support parallel loading for performance

**Key Principles:**
- Each bot handles multiple game types (RPSLS, Blotto, Penalty, Security)
- Single IBot implementation delegates to game-specific internal classes
- Multi-file support allows code organization (e.g., RpslsStrategy.cs, BlottoStrategy.cs)
- Each team's bot compiles into an isolated assembly

---

## Architecture Overview

```
BotLoader.LoadBotsFromDirectoryAsync()
  ↓
For each team folder (parallel):
  ├─ Collect all .cs files
  ├─ Validate total size limit (200KB)
  ├─ Validate each file size (50KB per file)
  ├─ Compile all files together with Roslyn
  ├─ Validate exactly one IBot implementation
  ├─ Check namespace restrictions across all files
  ├─ Create bot instance
  └─ Return BotInfo with BotInstance
```

---

## Components

### 1.1. `IBotLoader` Interface

**Location:** `TournamentEngine.Core/Common/IBotLoader.cs`

```csharp
public interface IBotLoader
{
    /// <summary>
    /// Loads all bots from the specified directory.
    /// Scans for team folders, each containing one or more .cs files.
    /// </summary>
    Task<List<BotInfo>> LoadBotsFromDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads a single bot from a team folder.
    /// Compiles all .cs files in the folder together.
    /// </summary>
    Task<BotInfo> LoadBotFromFolderAsync(
        string teamFolder,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates bot code files without compiling.
    /// Accepts multiple file contents for multi-file bots.
    /// </summary>
    BotValidationResult ValidateBotCode(Dictionary<string, string> files);
}
```

### 1.2. `BotLoader` Implementation

**Location:** `TournamentEngine.Core/BotLoader/BotLoader.cs`

**Dependencies:**
- `Microsoft.CodeAnalysis.CSharp.Scripting` - Roslyn for compilation
- `System.IO` - File operations

**Compilation Model:**

Each team folder is compiled into an **isolated assembly**:
```csharp
// TeamRocket_v2/ → Assembly "TeamRocket_Bot"
// TeamBlue_v1/   → Assembly "TeamBlue_Bot"
// TeamGreen_v3/  → Assembly "TeamGreen_Bot"
```

**Multi-file compilation process:**
1. Collect all .cs files from team folder
2. Parse each file into a `SyntaxTree` using Roslyn
3. Create single `CSharpCompilation` with ALL syntax trees
4. Compile into one assembly (in-memory, not saved to disk)
5. Load assembly and extract IBot implementation
6. Instantiate bot and store in `BotInfo.BotInstance`

**Isolation guarantees:**
- Each team's files compile **together** but **separately from other teams**
- Teams can use any namespace/class names without conflicts
- Example: Both TeamA and TeamB can have `namespace MyBot` and `class Strategy`
- No cross-contamination - each assembly is independent

**Key Methods:**

1. **`LoadBotsFromDirectoryAsync()`**
   - Scan directory for team folders
   - Call `LoadBotFromFolderAsync()` for each team folder in parallel
   - Collect results with error handling
   - Return list of `BotInfo` (valid and invalid)
   - Use controlled parallelism for performance (max 4 concurrent loads by default)

2. **`LoadBotFromFolderAsync()`**
   - Scan folder for all .cs files
   - Validate file count and sizes (per-file 50KB, total 200KB)
   - Read all file contents
   - Validate code before compilation (fast fail)
   - Compile all files together using Roslyn
   - Validate exactly one class implements `IBot`
   - Check namespace restrictions across all files
   - Create bot instance
   - Return `BotInfo` with populated `BotInstance`

3. **`ValidateBotCode()`**
   - Check syntax errors across all files
   - Verify exactly one IBot interface implementation
   - Check required methods (GetMoveAsync for all game types)
   - Validate namespace usage in all files
   - Verify total size limits
   - Return validation result without compilation

### 1.3. `BotValidationResult`

**Location:** `TournamentEngine.Core/Common/BotValidationResult.cs`

```csharp
public class BotValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> BlockedNamespaces { get; init; } = new();
}
```

---

## Validation Rules

### Namespace Restrictions
**Allowed:**
- `System`
- `System.Collections.Generic`
- `System.Linq`
- `System.Threading.Tasks`
- `TournamentEngine.Core.Common` (for IBot, GameState, etc.)

**Blocked:**
- `System.IO` (no file access)
- `System.Net` (no network)
- `System.Reflection` (no reflection)
- `System.Runtime.InteropServices` (no native code)
- Any external assembly references

### Code Constraints
- **Per-file limit:** 50 KB per individual .cs file
- **Total bot limit:** 200 KB for all files combined per team
- **Max files per bot:** 10 files (to prevent abuse)
- Must implement `IBot` interface **exactly once** across all files
- IBot implementation must have parameterless constructor
- Must have team name (class name or TeamName attribute)
- No unsafe code blocks in any file
- All files must compile together successfully

**Multi-Game Type Support:**
- Each bot must implement `IBot` interface **exactly once**
- The single `IBot` implementation must handle **all game types** (RPSLS, Blotto, Penalty, Security)
- How teams organize code internally is **completely flexible**

**Single IBot API Requirement:**
```csharp
public interface IBot
{
    // Must handle ALL game types through this single method
    Task<string> GetMoveAsync(GameState state, CancellationToken cancellationToken);
    
    // GameState.GameType tells the bot which game is being played
}
```

**Teams choose their own organization:**
- **Option 1: Single file** (simple bots)
- **Option 2: Multiple files** (complex bots with shared utilities)
- **Option 3: Game-specific strategy classes** (optional pattern, not required)

**Example - Multi-file bot with internal organization:**
```csharp
// TeamRocketBot.cs (IBot implementation)
public class TeamRocketBot : IBot
{
    public async Task<string> GetMoveAsync(GameState state, CancellationToken ct)
    {
        // Team decides how to handle different games
        return state.GameType switch
        {
            GameType.RPSLS => HandleRpsls(state),
            GameType.ColonelBlotto => new BlottoAI().GetMove(state),
            // Team's internal design choice
        };
    }
    
    private string HandleRpsls(GameState state) { /* ... */ }
}

// BlottoAI.cs (optional helper class, team's choice)
// Utilities.cs (optional shared code, team's choice)
```

**Compilation Model:**
- All team files compile together into **one assembly** (`{TeamName}_Bot`)
- Classes in different files can reference each other freely
- Each team's assembly is **isolated** - no conflicts between teams
- TeamA and TeamB can both have a "Strategy" class without conflict

### Compilation Settings
- Target: .NET 8.0
- Language version: C# 12
- Optimization: Release
- Allow nullable reference types

---

## Error Handling

**Error Categories:**

1. **File Errors:** File not found, access denied, invalid encoding
2. **Compilation Errors:** Syntax errors, missing references
3. **Validation Errors:** Missing IBot implementation, blocked namespaces
4. **Runtime Errors:** Constructor failure, initialization exceptions

**Strategy:**
- Collect all errors in `BotInfo.ValidationErrors`
- Set `BotInfo.IsValid = false` for invalid bots
- Continue loading other bots (don't fail entire batch)
- Log errors with structured logging

**Exception Types:**
- `BotLoadException` - Overall loading failure
- `BotCompilationException` - Compilation failure
- `BotValidationException` - Validation failure

---

## Technical: Multi-File Compilation & Isolation

### How Multi-File Compilation Works

**Teams submit multiple .cs files that need to reference each other:**

```csharp
// File: Bot.cs
public class MyBot : IBot
{
    public async Task<string> GetMoveAsync(GameState state, CancellationToken ct)
    {
        return Helper.Calculate(state); // References class in another file
    }
}

// File: Helper.cs
public static class Helper
{
    public static string Calculate(GameState state) { /* ... */ }
}
```

**BotLoader compiles them together:**

```csharp
// Simplified compilation logic
public async Task<BotInfo> LoadBotFromFolderAsync(string teamFolder)
{
    // 1. Collect all .cs files
    var files = Directory.GetFiles(teamFolder, "*.cs");
    
    // 2. Parse each file into a syntax tree
    var syntaxTrees = new List<SyntaxTree>();
    foreach (var file in files)
    {
        var code = await File.ReadAllTextAsync(file);
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, path: file));
    }
    
    // 3. Create compilation with ALL files together
    var compilation = CSharpCompilation.Create(
        assemblyName: $"{teamName}_Bot_{Guid.NewGuid()}",
        syntaxTrees: syntaxTrees,  // All files in one compilation!
        references: GetMetadataReferences(), // System.dll, etc.
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
    );
    
    // 4. Compile to in-memory assembly
    using var ms = new MemoryStream();
    var result = compilation.Emit(ms);
    
    if (!result.Success)
    {
        return new BotInfo { IsValid = false, ValidationErrors = result.Diagnostics };
    }
    
    // 5. Load assembly and find IBot implementation
    ms.Seek(0, SeekOrigin.Begin);
    var assembly = Assembly.Load(ms.ToArray());
    var botType = assembly.GetTypes().Single(t => typeof(IBot).IsAssignableFrom(t));
    
    // 6. Create instance
    var botInstance = (IBot)Activator.CreateInstance(botType);
    
    return new BotInfo
    {
        TeamName = teamName,
        BotInstance = botInstance,
        IsValid = true
    };
}
```

**Result:** All team files become part of **one assembly** where classes can reference each other freely.

### How Bot Isolation Works

**Problem:** Multiple teams might use same namespaces/class names:
```csharp
// TeamA/Bot.cs
namespace MyBot { public class Strategy : IBot { } }

// TeamB/Bot.cs  
namespace MyBot { public class Strategy : IBot { } }  // Same names!
```

**Solution:** Each team compiles into a **separate assembly** with unique name:

```
Compilation 1: "TeamA_Bot_{guid1}" contains MyBot.Strategy (TeamA's version)
Compilation 2: "TeamB_Bot_{guid2}" contains MyBot.Strategy (TeamB's version)
```

**At runtime:**
- TeamA's assembly loads with its own MyBot.Strategy
- TeamB's assembly loads with its own MyBot.Strategy  
- No conflict - they're in different assemblies
- Tournament only interacts via `IBot` interface

**Memory Isolation:**
```csharp
// Runtime - each bot's instance is separate
var teamABot = teamAAssembly.CreateInstance("MyBot.Strategy"); // TeamA's class
var teamBBot = teamBAssembly.CreateInstance("MyBot.Strategy"); // TeamB's class

// Call same interface method on different implementations
await teamABot.GetMoveAsync(state, ct);  // TeamA's logic
await teamBBot.GetMoveAsync(state, ct);  // TeamB's logic
```

**Guarantees:**
✅ No namespace conflicts  
✅ No class name conflicts  
✅ No static variable sharing  
✅ Teams completely isolated  
✅ Each team's code only sees their own classes

---

## Implementation Steps (TDD)

### Step 1.1: Single-File Bot Compilation
**Test:** `LoadBotFromFolder_SingleFile_CompilesSuccessfully`
- Create simple valid bot in single file
- Load and compile
- Verify BotInstance is not null
- Verify IsValid = true

**Implementation:**
- Basic Roslyn compilation for single file
- Create bot instance
- Populate BotInfo

### Step 1.1b: Multi-File Bot Compilation
**Test:** `LoadBotFromFolder_MultipleFiles_CompilesAllTogether`
- Create bot with 3 files (main + 2 strategy files)
- Load and compile all together
- Verify BotInstance is not null
- Verify all classes accessible

**Implementation:**
- Collect all .cs files from folder
- Compile all files together with Roslyn
- Create bot instance from compiled assembly

### Step 1.2: Validation
**Test:** `LoadBotFromFolder_InvalidBot_ReturnsValidationErrors`
- Bot missing IBot implementation
- Verify IsValid = false
- Verify ValidationErrors populated

**Test:** `LoadBotFromFolder_MultipleIBotImplementations_ReturnsError`
- Two files both implement IBot
- Verify error about multiple implementations

**Test:** `LoadBotFromFolder_ExceedsTotalSize_ReturnsError`
- Submit 5 files, each 50KB (total 250KB > 200KB limit)
- Verify size limit error

**Implementation:**
- Add interface validation (exactly one IBot)
- Check total size across all files
- Collect compilation errors

### Step 1.3: Namespace Restrictions
**Test:** `LoadBotFromFolder_BlockedNamespaceInAnyFile_ReturnsError`
- Main file is clean
- Second file uses System.IO
- Verify blocked namespace detected across all files
- Verify error message indicates which file

**Implementation:**
- Parse using statements in all files
- Check against blocked list
- Add to ValidationErrors with file name

### Step 1.4: Batch Directory Loading
**Test:** `LoadBotsFromDirectory_MultipleBots_LoadsAll`
- Create directory with 5 bot folders
- Load all bots sequentially first
- Verify 5 BotInfo returned
- Handle mix of valid and invalid bots

**Test:** `LoadBotsFromDirectory_SomeInvalidBots_LoadsValidOnesAndReportsInvalid`
- Directory with 3 valid and 2 invalid bots
- Verify all 5 BotInfo returned
- Verify 3 have IsValid = true
- Verify 2 have IsValid = false with errors

**Test:** `LoadBotsFromDirectory_EmptyDirectory_ReturnsEmptyList`
- Empty directory
- Verify empty list returned (no errors)

**Implementation:**
- Directory scanning for team folders
- Sequential loading initially
- Error collection per bot
- Continue loading on individual bot failures

### Step 1.5: Parallel Loading for Performance
**Test:** `LoadBotsFromDirectory_MultipleBots_LoadsAllInParallel`
- Create directory with 10 bot folders
- Load all bots in parallel
- Verify all 10 BotInfo returned
- Verify parallel execution is faster than sequential

**Test:** `LoadBotsFromDirectory_ParallelLoading_IsThreadSafe`
- Load 20 bots in parallel
- Verify no race conditions
- Verify each bot loaded independently
- Verify all results correct

**Implementation:**
- Use `Parallel.ForEachAsync` or `Task.WhenAll` for concurrent loading
- Add `MaxDegreeOfParallelism` configuration (default: 4)
- Ensure thread safety (no shared mutable state)
- Each bot compilation is independent
- Aggregate results safely

**Configuration:**
```json
{
  "BotLoader": {
    "MaxConcurrentLoads": 4
  }
}
```

**Example Implementation:**
```csharp
public async Task<List<BotInfo>> LoadBotsFromDirectoryAsync(
    string directory, 
    CancellationToken cancellationToken = default)
{
    var teamFolders = Directory.GetDirectories(directory);
    var results = new ConcurrentBag<BotInfo>();
    
    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = _maxConcurrentLoads,
        CancellationToken = cancellationToken
    };
    
    await Parallel.ForEachAsync(teamFolders, parallelOptions, async (folder, ct) =>
    {
        var botInfo = await LoadBotFromFolderAsync(folder, ct);
        results.Add(botInfo);
    });
    
    return results.ToList();
}
```

---

## Thread Safety

### BotLoader Implementation
- **Stateless design:** No shared mutable state
- **Parallel loading:** Use `Parallel.ForEachAsync` with degree of parallelism
- **Thread-safe:** Each bot loaded independently
- **Result collection:** Use `ConcurrentBag<BotInfo>` or thread-safe aggregation

---

## Configuration

### appsettings.json

```json
{
  "BotLoader": {
    "BotsDirectory": "bots/",
    "MaxFileCountPerBot": 10,
    "MaxBotFileSizeKB": 50,
    "MaxTotalBotSizeKB": 200,
    "MaxConcurrentLoads": 4,
    "AllowedNamespaces": [
      "System",
      "System.Collections.Generic",
      "System.Linq",
      "System.Threading.Tasks",
      "TournamentEngine.Core.Common"
    ],
    "BlockedNamespaces": [
      "System.IO",
      "System.Net",
      "System.Reflection",
      "System.Runtime.InteropServices"
    ]
  }
}
```

---

## Testing Strategy

### Unit Tests (TournamentEngine.Tests/)

1. **BotLoaderTests.cs**
   - Valid bot compilation (single and multi-file)
   - Invalid bot handling
   - Namespace restriction enforcement
   - Error collection
   - Batch loading
   - Parallel loading
   - Thread safety

2. **BotValidationTests.cs**
   - Interface validation
   - Namespace checking
   - Code size limits

### Integration Tests (TournamentEngine.Tests/Integration/)

3. **BotLoaderIntegrationTests.cs**
   - Load real bot files
   - Execute loaded bots
   - Full compilation pipeline
   - Performance benchmarks for parallel loading

---

## Success Criteria

- ✅ Load bots from directory
- ✅ Compile using Roslyn (single and multi-file)
- ✅ Validate IBot interface
- ✅ Enforce namespace restrictions
- ✅ Handle compilation errors gracefully
- ✅ Return BotInfo with BotInstance populated
- ✅ Support batch loading
- ✅ Support parallel loading with configurable concurrency
- ✅ Thread-safe implementation
- ✅ All unit tests pass
- ✅ Integration test with actual bot execution

---

## Dependencies

### NuGet Packages
- `Microsoft.CodeAnalysis.CSharp.Scripting` - Roslyn compilation
- `Microsoft.CodeAnalysis.CSharp` - Syntax analysis
- `Microsoft.Extensions.Logging` - Logging
- `Microsoft.Extensions.Configuration` - Configuration

---

## Example Usage

### Local Loading

```csharp
var botLoader = new BotLoader(logger, configuration);

// Load all bots in parallel (max 4 concurrent)
var bots = await botLoader.LoadBotsFromDirectoryAsync("bots/");

// Filter to valid bots only
var validBots = bots.Where(b => b.IsValid).ToList();

// Log errors for invalid bots
foreach (var bot in bots.Where(b => !b.IsValid))
{
    logger.LogWarning("Bot {TeamName} failed: {Errors}", 
        bot.TeamName, 
        string.Join(", ", bot.ValidationErrors));
}

// Run tournament with valid bots (each bot handles all game types)
await tournamentManager.RunTournamentAsync(validBots, GameType.RPSLS, ...);

// Same bots can play different games
await tournamentManager.RunTournamentAsync(validBots, GameType.ColonelBlotto, ...);
```

---

## Files to Create/Modify

1. `TournamentEngine.Core/Common/IBotLoader.cs` - ✅ EXISTS
2. `TournamentEngine.Core/Common/BotValidationResult.cs` - TODO
3. `TournamentEngine.Core/BotLoader/BotLoader.cs` - ✅ PARTIAL (Steps 1.1-1.3 complete)
4. `TournamentEngine.Tests/BotLoaderTests.cs` - ✅ PARTIAL (13 tests, 4 pending implementation)
5. `TournamentEngine.Tests/BotValidationTests.cs` - TODO
6. `TournamentEngine.Tests/Integration/BotLoaderIntegrationTests.cs` - TODO

---

## Current Progress

**Completed:**
- ✅ Step 1.1: Single-file bot compilation
- ✅ Step 1.1b: Multi-file bot compilation
- ✅ Step 1.2: Validation (size limits, multiple IBot detection)
- ✅ Step 1.3: Namespace restrictions

**In Progress (TDD Red Phase):**
- 🔄 Step 1.4: Batch directory loading (tests created)
- 🔄 Step 1.5: Parallel loading (tests created)

**Next:**
- Implement `LoadBotsFromDirectoryAsync()` method (GREEN phase)
- Add parallel loading with `Parallel.ForEachAsync`
- Add concurrency configuration
- All 13 BotLoader tests should pass
