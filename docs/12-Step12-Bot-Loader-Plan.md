# Step 12: Bot Loader - Implementation Plan

## Objective

Implement a comprehensive bot loading system that supports both local file-based loading and remote API-based registration. The system must compile, validate, and sandbox bot code while ensuring thread safety, uniqueness, and robust error handling.

---

## Core Concept

**Two-Phase Approach:**

**Phase 1: Local Directory Loading**
- Load bot folders from local directory (multi-file support)
- Each team can submit multiple .cs files organized in a folder
- Compile all files together using Roslyn
- Validate single IBot implementation exists
- Enforce namespace restrictions across all files
- Populate `BotInfo.BotInstance` with compiled bot

**Phase 2: Remote API Registration**
- RESTful API service for remote bot submission
- Accept multiple files per team (single endpoint)
- Store submitted bots as team folders locally
- Delegate to Phase 1 for actual loading
- Support multiple submissions per team (last wins)
- Thread-safe concurrent submissions

**Key Principles:**
- Each bot handles multiple game types (RPSLS, Blotto, Penalty, Security)
- Single IBot implementation delegates to game-specific internal classes
- Multi-file support allows code organization (e.g., RpslsStrategy.cs, BlottoStrategy.cs)
- Phase 2 downloads/stores bots locally, then Phase 1 loads them

---

## Architecture Overview

```
Phase 2 (Optional):
  HTTP POST /api/bots/submit
    ↓
  Store bot files to team folder
    ↓
Phase 1:
  BotLoader.LoadBotsFromDirectory()
    ↓
  For each team folder:
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

## Phase 1: Local Directory Loading

### Components

#### 1.1. `IBotLoader` Interface

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

#### 1.2. `BotLoader` Implementation

**Location:** `TournamentEngine.Core/BotLoader/BotLoader.cs`

**Dependencies:**
- `Microsoft.CodeAnalysis.CSharp.Scripting` - Roslyn for compilation
- `System.IO` - File operations

**Key Methods:**

1. **`LoadBotsFromDirectoryAsync()`**
   - Scan directory for team folders
   - Call `LoadBotFromFolderAsync()` for each team folder
   - Collect results with error handling
   - Return list of `BotInfo` (valid and invalid)

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

#### 1.3. `BotValidationResult`

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

### Validation Rules

#### Namespace Restrictions
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

#### Code Constraints
- **Per-file limit:** 50 KB per individual .cs file
- **Total bot limit:** 200 KB for all files combined per team
- **Max files per bot:** 10 files (to prevent abuse)
- Must implement `IBot` interface **exactly once** across all files
- IBot implementation must have parameterless constructor
- Must have team name (class name or TeamName attribute)
- No unsafe code blocks in any file
- All files must compile together successfully

**Multi-Game Type Support:**
- Each bot must handle all game types (RPSLS, Blotto, Penalty, Security)
- Single IBot implementation, typically delegates to game-specific classes:
  ```csharp
  // TeamRocketBot.cs (main file with IBot implementation)
  public class TeamRocketBot : IBot
  {
      public async Task<string> GetMoveAsync(GameState state, CancellationToken ct)
      {
          return state.GameType switch
          {
              GameType.RPSLS => new RpslsStrategy().GetMove(state),
              GameType.ColonelBlotto => new BlottoStrategy().GetMove(state),
              // ...
          };
      }
  }
  
  // RpslsStrategy.cs (separate file for RPSLS logic)
  // BlottoStrategy.cs (separate file for Blotto logic)
  ```

#### Compilation Settings
- Target: .NET 8.0
- Language version: C# 12
- Optimization: Release
- Allow nullable reference types

### Error Handling

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

## Phase 2: Remote API Registration Service

### Components

#### 2.1. API Service

**Location:** `TournamentEngine.Api/` (New project)

**Stack:** ASP.NET Core Minimal API (.NET 8)

**Project Structure:**
```
TournamentEngine.Api/
├── Program.cs
├── Endpoints/
│   └── BotEndpoints.cs
├── Services/
│   └── BotStorageService.cs
├── Models/
│   ├── BotSubmissionRequest.cs
│   └── BotSubmissionResponse.cs
└── appsettings.json
```

#### 2.2. API Endpoints

**Base URL:** `/api/bots`

##### `POST /api/bots/submit`
Submit a single bot

**Request (Multi-file support):**
```json
{
  "teamName": "TeamRocket",
  "files": [
    {
      "fileName": "TeamRocketBot.cs",
      "code": "public class TeamRocketBot : IBot { ... }"
    },
    {
      "fileName": "RpslsStrategy.cs",
      "code": "public class RpslsStrategy { ... }"
    },
    {
      "fileName": "BlottoStrategy.cs",
      "code": "public class BlottoStrategy { ... }"
    }
  ],
  "overwrite": true
}
```

**Note:** `gameType` removed - each bot handles all game types

**Response:**
```json
{
  "success": true,
  "teamName": "TeamRocket",
  "submissionId": "guid",
  "message": "Bot submitted successfully",
  "errors": []
}
```

##### `POST /api/bots/submit-batch`
Submit multiple bots at once

**Request:**
```json
{
  "bots": [
    {
      "teamName": "Team1",
      "files": [
        { "fileName": "Team1Bot.cs", "code": "..." },
        { "fileName": "Strategy.cs", "code": "..." }
      ]
    },
    {
      "teamName": "Team2",
      "files": [
        { "fileName": "Team2Bot.cs", "code": "..." }
      ]
    }
  ]
}
```

**Response:**
```json
{
  "successCount": 2,
  "failureCount": 0,
  "results": [
    { "teamName": "Team1", "success": true, "submissionId": "guid1" },
    { "teamName": "Team2", "success": true, "submissionId": "guid2" }
  ]
}
```

##### `GET /api/bots/list`
List all submitted bots

**Response:**
```json
{
  "bots": [
    {
      "teamName": "TeamRocket",
      "fileCount": 3,
      "totalSizeBytes": 15234,
      "submissionTime": "2026-02-11T10:30:00Z",
      "version": 2
    }
  ]
}
```

##### `DELETE /api/bots/{teamName}`
Remove a submitted bot

**Response:**
```json
{
  "success": true,
  "message": "Bot TeamRocket deleted"
}
```

#### 2.3. `BotStorageService`

**Location:** `TournamentEngine.Api/Services/BotStorageService.cs`

**Responsibilities:**
- Store submitted bot code to local directory
- Track submission history per team
- Handle overwrites (keep only latest)
- Thread-safe concurrent submissions
- Enforce team name uniqueness

**Key Methods:**

```csharp
public class BotStorageService
{
    private readonly string _storageDirectory;
    private readonly SemaphoreSlim _semaphore; // For thread safety
    private readonly Dictionary<string, BotSubmissionMetadata> _submissions;
    private readonly object _lock = new();
    
    Task<BotSubmissionResult> StoreBot(BotSubmissionRequest request);
    Task<List<BotSubmissionMetadata>> GetAllSubmissions();
    Task<bool> DeleteBot(string teamName);
    Task<BotSubmissionMetadata?> GetSubmission(string teamName);
}
```

**Thread Safety:**
- Use `SemaphoreSlim` to control concurrent file writes
- Use `lock` around `_submissions` dictionary access
- Atomic file write operations
- Transaction-like behavior: write to temp file, then rename

**Uniqueness Enforcement:**
- Team name is the unique key
- Case-insensitive comparison
- Resubmission with same team name overwrites previous
- Track version number per team

#### 2.4. Data Models

**`BotSubmissionRequest.cs`:**
```csharp
public class BotSubmissionRequest
{
    public required string TeamName { get; init; }
    public required List<BotFile> Files { get; init; }
    public bool Overwrite { get; init; } = true;
}

public class BotFile
{
    public required string FileName { get; init; }
    public required string Code { get; init; }
}
```

**`BotSubmissionMetadata.cs`:**
```csharp
public class BotSubmissionMetadata
{
    public required string TeamName { get; init; }
    public required string FolderPath { get; init; }
    public List<string> FilePaths { get; init; } = new();
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTime SubmissionTime { get; init; }
    public int Version { get; init; } // Increments on resubmission
    public string SubmissionId { get; init; }
}
```

**`BotSubmissionResult.cs`:**
```csharp
public class BotSubmissionResult
{
    public bool Success { get; init; }
    public string? TeamName { get; init; }
    public string? SubmissionId { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = new();
}
```

### Storage Structure

**Directory Layout (Multi-file support):**
```
bots/
├── TeamRocket_v2/
│   ├── TeamRocketBot.cs          (IBot implementation)
│   ├── RpslsStrategy.cs          (RPSLS game logic)
│   ├── BlottoStrategy.cs         (Blotto game logic)
│   └── Utilities.cs              (shared helper functions)
├── TeamBlue_v1/
│   └── TeamBlueBot.cs            (single-file bot)
├── TeamGreen_v3/
│   ├── TeamGreenBot.cs
│   └── GameStrategies.cs
└── .metadata/
    ├── TeamRocket.json
    ├── TeamBlue.json
    └── TeamGreen.json
```

**Folder Pattern:** `{TeamName}_v{Version}/`
**Note:** Each team has a folder containing one or more .cs files

**Metadata JSON:**
```json
{
  "teamName": "TeamRocket",
  "currentVersion": 2,
  "currentFolderPath": "bots/TeamRocket_v2/",
  "fileCount": 4,
  "totalSizeBytes": 23456,
  "files": [
    "TeamRocketBot.cs",
    "RpslsStrategy.cs",
    "BlottoStrategy.cs",
    "Utilities.cs"
  ],
  "submissionHistory": [
    { "version": 1, "time": "2026-02-11T09:00:00Z", "fileCount": 1 },
    { "version": 2, "time": "2026-02-11T10:30:00Z", "fileCount": 4 }
  ]
}
```

### API Authentication & Security

**Phase 2A (MVP):** No authentication (local development)

**Phase 2B (Future):**
- API key authentication
- Rate limiting per key
- Request size limits (max 100KB per bot)
- CORS configuration
- HTTPS only in production

---

## Integration Flow

### Tournament Execution with Bot Loader

```
Program.cs:
1. Initialize BotStorageService (if API enabled)
2. Wait for registration period (if API enabled)
3. Call BotLoader.LoadBotsFromDirectory("bots/")
4. Validate all bots loaded successfully
5. Pass List<BotInfo> to TournamentSeriesManager
6. Run tournaments
```

### Handling Resubmissions

**Scenario:** Team submits bot twice during registration period (improving their code)

**Flow:**
1. First submission: `TeamRocket_v1/` folder created with 1 file
2. Second submission: `TeamRocket_v2/` folder created with 4 files (refactored)
3. Before loading: Delete old version folders, keep only latest
4. BotLoader loads all files from `TeamRocket_v2/` folder
5. Tournament uses latest version (v2 with improved structure)

**Implementation:**
- `BotStorageService.GetCurrentBots()` returns only latest versions
- `BotLoader` ignores old version files
- Cleanup task deletes old versions after registration closes

---

## Implementation Steps (TDD)

### Phase 1: Local Directory Loading

#### Step 1.1: Single-File Bot Compilation
**Test:** `LoadBotFromFolder_SingleFile_CompilesSuccessfully`
- Create simple valid bot in single file
- Load and compile
- Verify BotInstance is not null
- Verify IsValid = true

**Implementation:**
- Basic Roslyn compilation for single file
- Create bot instance
- Populate BotInfo

#### Step 1.1b: Multi-File Bot Compilation
**Test:** `LoadBotFromFolder_MultipleFiles_CompilesAllTogether`
- Create bot with 3 files (main + 2 strategy files)
- Load and compile all together
- Verify BotInstance is not null
- Verify all classes accessible

**Implementation:**
- Collect all .cs files from folder
- Compile all files together with Roslyn
- Create bot instance from compiled assembly

#### Step 1.2: Validation
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

#### Step 1.3: Namespace Restrictions
**Test:** `LoadBotFromFolder_BlockedNamespaceInAnyFile_ReturnsError`
- Main file is clean
- Second file uses System.IO
- Verify blocked namespace detected across all files
- Verify error message indicates which file

**Implementation:**
- Parse using statements in all files
- Check against blocked list
- Add to ValidationErrors with file name

#### Step 1.4: Batch Loading
**Test:** `LoadBotsFromDirectory_MultipleFiles_LoadsAll`
- Create directory with 5 bot files
- Load all
- Verify 5 BotInfo returned
- Mix of valid and invalid

**Implementation:**
- Directory scanning
- Parallel loading
- Error collection

#### Step 1.5: Error Handling
**Test:** `LoadBotFromFile_FileNotFound_ThrowsBotLoadException`
- Non-existent file path
- Verify exception thrown

**Implementation:**
- File existence check
- Proper exception types

### Phase 2: Remote API Service

#### Step 2.1: API Project Setup
- Create TournamentEngine.Api project
- Add ASP.NET Core dependencies
- Configure minimal API

#### Step 2.2: Bot Submission Endpoint
**Test:** `SubmitBot_ValidBot_StoresSuccessfully`
- POST valid bot
- Verify file created
- Verify response success

**Implementation:**
- POST /api/bots/submit endpoint
- BotStorageService.StoreBot()
- File write logic

#### Step 2.3: Thread Safety
**Test:** `SubmitBot_ConcurrentSubmissions_AllSucceed`
- Submit 10 different bots concurrently
- Verify all stored
- Verify no corruption

**Implementation:**
- SemaphoreSlim for file writes
- Lock around dictionary access

#### Step 2.4: Overwrite Logic
**Test:** `SubmitBot_SameTeamTwice_KeepsLatest`
- Submit TeamA
- Submit TeamA again
- Verify only v2 exists
- Verify v1 marked as old

**Implementation:**
- Version tracking
- Old file cleanup

#### Step 2.5: Batch Submission
**Test:** `SubmitBatch_MultipleBots_StoresAll`
- POST batch of 5 bots
- Verify all stored
- Verify response counts

**Implementation:**
- POST /api/bots/submit-batch endpoint
- Parallel storage
- Result aggregation

#### Step 2.6: Integration Test
**Test:** `EndToEnd_SubmitViaAPI_LoadAndRunTournament`
- Submit 10 bots via API
- Close registration
- Load bots via BotLoader
- Run tournament
- Verify all bots participated

**Implementation:**
- Full pipeline integration

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
  },
  "BotApi": {
    "Enabled": false,
    "RegistrationDurationMinutes": 15,
    "Port": 5000,
    "MaxRequestSizeKB": 100,
    "RequireAuthentication": false,
    "CleanupOldVersions": true
  }
}
```

---

## Error Handling Strategy

### Levels

1. **Individual Bot Errors:** Don't fail entire load operation
   - Store in `BotInfo.ValidationErrors`
   - Set `IsValid = false`
   - Continue loading other bots

2. **Directory Load Errors:** Partial success allowed
   - Return all BotInfo (valid and invalid)
   - Log directory-level issues
   - Caller decides minimum valid bot count

3. **API Submission Errors:** Return detailed error response
   - HTTP 400 for validation errors
   - HTTP 500 for server errors
   - Detailed error messages

4. **Critical Errors:** Throw exceptions
   - Directory not found
   - Roslyn compilation infrastructure failure
   - File system permissions

### Logging

**Use structured logging throughout:**

```csharp
_logger.LogInformation("Loading bots from {Directory}", directory);
_logger.LogWarning("Bot {TeamName} failed validation: {Errors}", 
    teamName, string.Join(", ", errors));
_logger.LogError(ex, "Failed to compile bot {FilePath}", filePath);
```

---

## Thread Safety

### Phase 1: BotLoader
- **Stateless design:** No shared mutable state
- **Parallel loading:** Use `Parallel.ForEachAsync` with degree of parallelism
- **Thread-safe:** Each bot loaded independently

### Phase 2: BotStorageService
- **File writes:** SemaphoreSlim(1) ensures sequential writes
- **Dictionary access:** Lock around `_submissions` dictionary
- **Metadata updates:** Atomic file operations (write to temp, then move)

---

## Testing Strategy

### Unit Tests (TournamentEngine.Tests/)

1. **BotLoaderTests.cs**
   - Valid bot compilation
   - Invalid bot handling
   - Namespace restriction enforcement
   - Error collection
   - Batch loading

2. **BotValidationTests.cs**
   - Interface validation
   - Namespace checking
   - Code size limits

### Integration Tests (TournamentEngine.Tests/Integration/)

3. **BotLoaderIntegrationTests.cs**
   - Load real bot files
   - Execute loaded bots
   - Full compilation pipeline

4. **BotApiIntegrationTests.cs** (Phase 2)
   - API submission
   - Storage and retrieval
   - Concurrent submissions
   - End-to-end flow

---

## Success Criteria

### Phase 1
- ✅ Load bots from directory
- ✅ Compile using Roslyn
- ✅ Validate IBot interface
- ✅ Enforce namespace restrictions
- ✅ Handle compilation errors gracefully
- ✅ Return BotInfo with BotInstance populated
- ✅ Support batch loading
- ✅ All unit tests pass
- ✅ Integration test with actual bot execution

### Phase 2
- ✅ API service running
- ✅ Submit bot via POST endpoint
- ✅ Store bot code locally
- ✅ Thread-safe concurrent submissions
- ✅ Overwrite handling (latest version wins)
- ✅ Batch submission support
- ✅ List submitted bots
- ✅ Delete bot support
- ✅ Integration with Phase 1 loading
- ✅ End-to-end API → Load → Tournament flow

---

## Dependencies

### NuGet Packages

**Phase 1:**
- `Microsoft.CodeAnalysis.CSharp.Scripting` - Roslyn compilation
- `Microsoft.CodeAnalysis.CSharp` - Syntax analysis
- `Microsoft.Extensions.Logging` - Logging

**Phase 2:**
- ASP.NET Core (included in .NET 8)
- `Microsoft.Extensions.Configuration` - Configuration
- `System.Text.Json` - JSON serialization

---

## Future Enhancements (Out of Scope)

1. **Advanced Sandboxing:**
   - AppDomain isolation (deprecated in .NET Core)
   - Process isolation with IPC
   - Memory limits per bot

2. **Hot Reload:**
   - Reload bots without restarting tournament
   - Watch directory for changes

3. **Bot Versioning:**
   - Keep multiple versions active
   - A/B testing between versions

4. **API Enhancements:**
   - WebSocket for real-time submission status
   - Bot leaderboard preview
   - Code validation before submission

5. **Security:**
   - Code signing
   - Virus scanning
   - Rate limiting by IP
   - API key management

---

## Example Usage

### Phase 1: Local Loading

```csharp
var botLoader = new BotLoader(logger, configuration);
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

### Phase 2: API Submission

```bash
# Submit a multi-file bot
curl -X POST http://localhost:5000/api/bots/submit \
  -H "Content-Type: application/json" \
  -d '{
    "teamName": "TeamRocket",
    "files": [
      {
        "fileName": "TeamRocketBot.cs",
        "code": "public class TeamRocketBot : IBot { ... }"
      },
      {
        "fileName": "RpslsStrategy.cs",
        "code": "public class RpslsStrategy { ... }"
      }
    ]
  }'

# List all bots
curl http://localhost:5000/api/bots/list

# Load and run tournament
dotnet run --project TournamentEngine.Console
```

---

## Files to Create

### Phase 1
1. `TournamentEngine.Core/Common/IBotLoader.cs`
2. `TournamentEngine.Core/Common/BotValidationResult.cs`
3. `TournamentEngine.Core/BotLoader/BotLoader.cs`
4. `TournamentEngine.Tests/BotLoaderTests.cs`
5. `TournamentEngine.Tests/BotValidationTests.cs`
6. `TournamentEngine.Tests/Integration/BotLoaderIntegrationTests.cs`

### Phase 2
7. `TournamentEngine.Api/Program.cs`
8. `TournamentEngine.Api/Endpoints/BotEndpoints.cs`
9. `TournamentEngine.Api/Services/BotStorageService.cs`
10. `TournamentEngine.Api/Models/BotSubmissionRequest.cs`
11. `TournamentEngine.Api/Models/BotSubmissionMetadata.cs`
12. `TournamentEngine.Api/Models/BotSubmissionResult.cs`
13. `TournamentEngine.Api/appsettings.json`
14. `TournamentEngine.Tests/Integration/BotApiIntegrationTests.cs`
