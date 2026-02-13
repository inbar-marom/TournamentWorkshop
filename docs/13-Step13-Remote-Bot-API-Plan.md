# Step 13: Remote Bot Registration API - Implementation Plan

## Objective

Implement a RESTful API service that allows remote bot submission and registration. The service receives bot code files via HTTP POST, stores them locally, and integrates with the Local Bot Loader (Step 12) for compilation and validation.

---

## Core Concept

**Remote API Registration Approach:**
- RESTful API service for remote bot submission
- Accept multiple files per team (single endpoint)
- Store submitted bots as team folders locally
- Delegate to Step 12 (Local Bot Loader) for actual loading
- Support multiple submissions per team (last wins)
- Thread-safe concurrent submissions

**Key Principles:**
- Each bot handles multiple game types (RPSLS, Blotto, Penalty, Security)
- Single IBot implementation delegates to game-specific internal classes
- Multi-file support allows code organization
- API downloads/stores bots locally, then Step 12 loader compiles them

---

## Architecture Overview

```
HTTP POST /api/bots/submit
  ↓
BotStorageService stores files to team folder
  ↓
Local Bot Loader (Step 12) loads from directory
  ↓
Tournament execution
```

---

## Components

### 1. API Service

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

### 2. API Endpoints

**Base URL:** `/api/bots`

#### `POST /api/bots/submit`
Submit a single bot (handles ALL game types)

**Request (Multi-file support):**
```json
{
  "teamName": "TeamRocket",
  "files": [
    {
      "fileName": "Bot.cs",
      "code": "public class RocketBot : IBot { /* handles all games */ }"
    },
    {
      "fileName": "AI.cs",
      "code": "public class GameAI { /* team's internal logic */ }"
    },
    {
      "fileName": "Utils.cs",
      "code": "public static class Utils { /* helper functions */ }"
    }
  ],
  "overwrite": true
}
```

**Notes:** 
- Single endpoint submits bot that handles **all game types** (RPSLS, Blotto, Penalty, Security)
- File names are team's choice (no requirements or conventions)
- Bot must have one IBot implementation that handles all games internally

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

#### `POST /api/bots/submit-batch`
Submit multiple bots at once (each bot handles all game types)

**Request:**
```json
{
  "bots": [
    {
      "teamName": "Team1",
      "files": [
        { "fileName": "Bot.cs", "code": "..." },
        { "fileName": "Helper.cs", "code": "..." }
      ]
    },
    {
      "teamName": "Team2",
      "files": [
        { "fileName": "AI.cs", "code": "..." }
      ]
    }
  ]
}
```

**Note:** Each bot submission contains a complete bot that handles all game types

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

#### `GET /api/bots/list`
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

#### `DELETE /api/bots/{teamName}`
Remove a submitted bot

**Response:**
```json
{
  "success": true,
  "message": "Bot TeamRocket deleted"
}
```

### 3. `BotStorageService`

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

### 4. Data Models

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

---

## Storage Structure

**Directory Layout (Multi-file support):**
```
bots/
├── TeamRocket_v2/
│   ├── Bot.cs                    (IBot implementation - handles all games)
│   ├── AILogic.cs                (team's internal organization)
│   ├── Strategies.cs             (team's internal organization)
│   └── Utils.cs                  (team's internal organization)
├── TeamBlue_v1/
│   └── BlueBot.cs                (single-file bot - also valid!)
├── TeamGreen_v3/
│   ├── Main.cs                   (IBot here - handles all games)
│   └── Helpers.cs                (team's choice of structure)
└── .metadata/
    ├── TeamRocket.json
    ├── TeamBlue.json
    └── TeamGreen.json
```

**Folder Pattern:** `{TeamName}_v{Version}/`

**Important Notes:**
- Each bot handles **all game types** through single IBot implementation
- Filename choices are **entirely up to the team** (no requirements)
- Folder must contain at least one .cs file with IBot implementation
- All .cs files in folder compile together as one unit
- Teams can name files anything they want (Bot.cs, AI.cs, Main.cs, Logic.cs, etc.)
- File organization is for team convenience - doesn't affect functionality

**Metadata JSON:**
```json
{
  "teamName": "TeamRocket",
  "currentVersion": 2,
  "currentFolderPath": "bots/TeamRocket_v2/",
  "fileCount": 4,
  "totalSizeBytes": 23456,
  "files": [
    "Bot.cs",
    "AI.cs",
    "Logic.cs",
    "Utils.cs"
  ],
  "submissionHistory": [
    { "version": 1, "time": "2026-02-11T09:00:00Z", "fileCount": 1 },
    { "version": 2, "time": "2026-02-11T10:30:00Z", "fileCount": 4 }
  ]
}
```

---

## API Authentication & Security

**Phase A (MVP):** No authentication (local development)

**Phase B (Future):**
- API key authentication
- Rate limiting per key
- Request size limits (max 100KB per bot)
- CORS configuration
- HTTPS only in production

---

## Integration Flow

### Tournament Execution with Remote API

```
Program.cs:
1. Initialize BotStorageService (if API enabled)
2. Start API service (optional)
3. Wait for registration period (if API enabled)
4. Call BotLoader.LoadBotsFromDirectory("bots/") (from Step 12)
5. Validate all bots loaded successfully
6. Pass List<BotInfo> to TournamentSeriesManager
7. Run tournaments
```

### Handling Resubmissions

**Scenario:** Team submits bot twice during registration period (improving their code)

**Flow:**
1. First submission: `TeamRocket_v1/` folder created with 1 file
2. Second submission: `TeamRocket_v2/` folder created with 4 files (refactored)
3. Before loading: Delete old version folders, keep only latest
4. BotLoader (Step 12) loads all files from `TeamRocket_v2/` folder
5. Tournament uses latest version (v2 with improved structure)

**Implementation:**
- `BotStorageService.GetCurrentBots()` returns only latest versions
- `BotLoader` ignores old version files
- Cleanup task deletes old versions after registration closes

---

## Implementation Steps (TDD)

### Step 2.1: API Project Setup
- Create TournamentEngine.Api project
- Add ASP.NET Core dependencies (.NET 8)
- Configure minimal API
- Setup logging and configuration

**Files:**
- `TournamentEngine.Api/TournamentEngine.Api.csproj`
- `TournamentEngine.Api/Program.cs`
- `TournamentEngine.Api/appsettings.json`

### Step 2.2: Bot Submission Endpoint
**Test:** `SubmitBot_ValidBot_StoresSuccessfully`
- POST valid single-file bot
- Verify file created in correct folder
- Verify response success
- Verify metadata created

**Test:** `SubmitBot_MultiFileBot_StoresAll`
- POST bot with 3 files
- Verify all files created
- Verify folder structure correct

**Implementation:**
- POST /api/bots/submit endpoint
- BotStorageService.StoreBot()
- File write logic with error handling

### Step 2.3: Thread Safety
**Test:** `SubmitBot_ConcurrentSubmissions_AllSucceed`
- Submit 10 different bots concurrently
- Verify all stored correctly
- Verify no file corruption
- Verify no data races

**Implementation:**
- SemaphoreSlim for file writes
- Lock around dictionary access
- Atomic file operations

### Step 2.4: Overwrite Logic
**Test:** `SubmitBot_SameTeamTwice_KeepsLatest`
- Submit TeamA (v1)
- Submit TeamA again (v2)
- Verify only v2 exists in active directory
- Verify v1 marked as old in metadata
- Verify version number incremented

**Implementation:**
- Version tracking in metadata
- Old folder cleanup or archival
- Latest version pointer

### Step 2.5: Batch Submission
**Test:** `SubmitBatch_MultipleBots_StoresAll`
- POST batch of 5 bots
- Verify all stored correctly
- Verify response counts match
- Handle partial failures

**Implementation:**
- POST /api/bots/submit-batch endpoint
- Parallel storage with controlled concurrency
- Result aggregation
- Individual error handling

### Step 2.6: List and Delete Operations
**Test:** `ListBots_ReturnsAllSubmissions`
- Submit 3 bots
- GET /api/bots/list
- Verify all 3 returned with metadata

**Test:** `DeleteBot_RemovesBot`
- Submit bot
- DELETE /api/bots/{teamName}
- Verify bot removed from storage
- Verify metadata updated

**Implementation:**
- GET /api/bots/list endpoint
- DELETE /api/bots/{teamName} endpoint
- Metadata management

### Step 2.7: Integration Test
**Test:** `EndToEnd_SubmitViaAPI_LoadAndRunTournament`
- Start API service
- Submit 10 bots via API
- Close registration
- Load bots via BotLoader (Step 12)
- Run tournament
- Verify all bots participated

**Implementation:**
- Full pipeline integration
- API → Storage → Loading → Execution

---

## Configuration

### appsettings.json

```json
{
  "BotApi": {
    "Enabled": false,
    "RegistrationDurationMinutes": 15,
    "Port": 5000,
    "MaxRequestSizeKB": 100,
    "RequireAuthentication": false,
    "CleanupOldVersions": true,
    "StorageDirectory": "bots/"
  }
}
```

---

## Error Handling Strategy

### Levels

1. **API Submission Errors:** Return detailed error response
   - HTTP 400 for validation errors (invalid JSON, missing fields)
   - HTTP 409 for conflicts (team name taken, overwrite disabled)
   - HTTP 413 for payload too large
   - HTTP 500 for server errors
   - Detailed error messages in response

2. **Storage Errors:** Partial success allowed
   - File write failures logged
   - Metadata preserved
   - Return error in response

3. **Critical Errors:** Throw exceptions
   - Directory creation failure
   - Permissions issues
   - Disk space exhaustion

### Logging

**Use structured logging throughout:**

```csharp
_logger.LogInformation("Received bot submission from team {TeamName} with {FileCount} files", 
    teamName, fileCount);
_logger.LogWarning("Bot submission failed for team {TeamName}: {Errors}", 
    teamName, string.Join(", ", errors));
_logger.LogError(ex, "Failed to store bot {TeamName}", teamName);
```

---

## Thread Safety

### BotStorageService
- **File writes:** SemaphoreSlim(1) ensures sequential writes per team
- **Dictionary access:** Lock around `_submissions` dictionary
- **Metadata updates:** Atomic file operations (write to temp, then move)
- **Concurrent submissions:** Different teams can submit simultaneously
- **Same team resubmission:** Serialized to prevent race conditions

---

## Testing Strategy

### Unit Tests (TournamentEngine.Tests/)

1. **BotStorageServiceTests.cs**
   - Store single bot
   - Store multi-file bot
   - Overwrite logic
   - Thread safety
   - Metadata management

2. **BotEndpointsTests.cs**
   - POST /api/bots/submit validation
   - Batch submission
   - List bots
   - Delete bot
   - Error responses

### Integration Tests (TournamentEngine.Tests/Integration/)

3. **BotApiIntegrationTests.cs**
   - Full API submission flow
   - Storage and retrieval
   - Concurrent submissions
   - Integration with BotLoader (Step 12)
   - End-to-end tournament execution

---

## Success Criteria

- ✅ API service running
- ✅ Submit bot via POST endpoint (single and multi-file)
- ✅ Store bot code locally with proper folder structure
- ✅ Thread-safe concurrent submissions
- ✅ Overwrite handling (latest version wins)
- ✅ Batch submission support
- ✅ List submitted bots with metadata
- ✅ Delete bot support
- ✅ Integration with Step 12 (Local Bot Loader)
- ✅ End-to-end API → Load → Tournament flow
- ✅ All unit tests pass
- ✅ Integration tests pass

---

## Dependencies

### NuGet Packages
- ASP.NET Core (included in .NET 8)
- `Microsoft.Extensions.Configuration` - Configuration
- `System.Text.Json` - JSON serialization
- `Microsoft.Extensions.Logging` - Logging

---

## Example Usage

### API Submission

```bash
# Submit a multi-file bot (handles all game types)
curl -X POST http://localhost:5000/api/bots/submit \
  -H "Content-Type: application/json" \
  -d '{
    "teamName": "TeamRocket",
    "files": [
      {
        "fileName": "Bot.cs",
        "code": "public class RocketBot : IBot { /* all games */ }"
      },
      {
        "fileName": "AI.cs",
        "code": "public class GameAI { /* team logic */ }"
      }
    ]
  }'

# List all bots
curl http://localhost:5000/api/bots/list

# Delete a bot
curl -X DELETE http://localhost:5000/api/bots/TeamRocket
```

### Integration with Tournament

```csharp
// 1. Start API (optional)
if (config.BotApi.Enabled)
{
    var apiHost = Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webBuilder => 
            webBuilder.UseStartup<Startup>())
        .Build();
    await apiHost.StartAsync();
}

// 2. Wait for registration
await Task.Delay(TimeSpan.FromMinutes(config.RegistrationDurationMinutes));

// 3. Load bots using Step 12 BotLoader
var botLoader = new BotLoader(logger, configuration);
var bots = await botLoader.LoadBotsFromDirectoryAsync("bots/");

// 4. Run tournament
var validBots = bots.Where(b => b.IsValid).ToList();
await tournamentManager.RunTournamentAsync(validBots, ...);
```

---

## Files to Create

1. `TournamentEngine.Api/Program.cs`
2. `TournamentEngine.Api/Endpoints/BotEndpoints.cs`
3. `TournamentEngine.Api/Services/BotStorageService.cs`
4. `TournamentEngine.Api/Models/BotSubmissionRequest.cs`
5. `TournamentEngine.Api/Models/BotSubmissionMetadata.cs`
6. `TournamentEngine.Api/Models/BotSubmissionResult.cs`
7. `TournamentEngine.Api/appsettings.json`
8. `TournamentEngine.Api/TournamentEngine.Api.csproj`
9. `TournamentEngine.Tests/BotStorageServiceTests.cs`
10. `TournamentEngine.Tests/BotEndpointsTests.cs`
11. `TournamentEngine.Tests/Integration/BotApiIntegrationTests.cs`

---

## Future Enhancements (Out of Scope)

1. **Advanced Security:**
   - API key authentication
   - Rate limiting by IP/key
   - Code scanning/validation before storage
   - HTTPS enforcement
   - CORS configuration

2. **Enhanced Features:**
   - WebSocket for real-time submission status
   - Bot leaderboard preview
   - Code validation before submission
   - Submission history tracking
   - Team dashboard

3. **Scalability:**
   - Database for metadata (vs. JSON files)
   - Distributed storage
   - Load balancing
   - Caching layer

4. **Monitoring:**
   - Submission metrics
   - Storage usage tracking
   - API performance monitoring
   - Alert system for failures

---

## Dependencies on Other Steps

**Requires:**
- ✅ Step 12 (Local Bot Loader): Must be complete for bot compilation and loading
- ✅ Core contracts (IBot, BotInfo, etc.): From Step 2

**Provides:**
- Remote bot submission capability
- Team self-service registration
- Dynamic bot updates during registration period

---

## Status

**Current:** IN PROGRESS - Starting Step 2.1 (API Project Setup)

**Approach:** Test-Driven Development (TDD) - Write tests first, then implementation

**Next:** Step 2.1 - Create API project with ASP.NET Core 8.0 minimal API setup
