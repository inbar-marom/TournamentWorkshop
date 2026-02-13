# Step 15: Implement Bot Submission Dashboard - Implementation Plan

## Objective

Create a web-based dashboard to display, manage, and monitor remotely submitted bots (from Step 13). Provide real-time visibility into submission status, validation results, and bot metadata with administrative controls.

---

## Core Concept

**Purpose:**
- Display all bots submitted via Step 13 API
- Show real-time validation status
- Manage bot submissions (delete, re-validate)
- Provide team feedback on submission status
- Support tournament organizers in bot management

**Integration Points:**
- Consumes Step 13 API (`/api/bots/list`, `/api/bots/submit`, `/api/bots/delete`)
- Integrates with Step 12 (BotLoader) for re-validation
- Real-time updates via SignalR
- Displays validation errors with helpful UI

---

## Architecture Overview

```
Browser (Dashboard UI)
  ↓
TournamentEngine.Dashboard/Pages/BotsPage.cshtml (Razor Page or Vue.js)
  ↓
TournamentEngine.Api/Services/BotStorageService
  ↓
TournamentEngine.Core/BotLoader/BotLoader.cs (Validation)
  ↓
Local File System (bots/ directory)
```

---

## Components

### 1. Dashboard UI

**Location:** `TournamentEngine.Dashboard/Pages/` or `TournamentEngine.Dashboard/wwwroot/`

**Technology:**
- Option A: Razor Pages with server-side rendering
- Option B: Vue.js/React SPA consuming APIs
- Option C: Built-in to existing dashboard (extend index.html)

**Recommendation:** Extend existing dashboard (Option C) as new tab or collapsible panel

### 2. Data Models

**Location:** `TournamentEngine.Api/Models/`

```csharp
public class BotDashboardDto
{
    public string TeamName { get; set; }
    public DateTime SubmissionTime { get; set; }
    public DateTime LastUpdatedTime { get; set; }
    public ValidationStatus Status { get; set; } // Valid, Invalid, Pending
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public int Version { get; set; }
    public string CompilationError { get; set; } // null if Valid
    public List<string> FileNames { get; set; }
    public List<BotVersionInfo> VersionHistory { get; set; }
}

public class BotVersionInfo
{
    public int Version { get; set; }
    public DateTime SubmissionTime { get; set; }
    public int FileCount { get; set; }
    public ValidationStatus Status { get; set; }
}

public enum ValidationStatus
{
    Valid,
    Invalid,
    Pending,
    ValidationInProgress
}
```

### 3. API Endpoints (Extended from Step 13)

**Location:** `TournamentEngine.Api/Endpoints/BotEndpoints.cs`

**New Endpoints:**

```csharp
// Get bot details with validation information
GET /api/bots/{teamName}
Response: BotDashboardDto

// Trigger re-validation of a bot
POST /api/bots/{teamName}/validate
Response: { success: bool, message: string, errors: string[] }

// Get validation history
GET /api/bots/{teamName}/history
Response: List<BotVersionInfo>

// Get compilation error details
GET /api/bots/{teamName}/errors
Response: { hasErrors: bool, errors: string[], details: string }
```

### 4. Dashboard Service

**Location:** `TournamentEngine.Dashboard/Services/BotDashboardService.cs`

**Responsibilities:**
- Fetch bot list from BotStorageService
- Call BotLoader to validate/re-validate bots
- Cache results with configurable TTL
- Handle real-time updates via SignalR
- Format data for UI consumption

### 5. SignalR Hub Events

**Location:** `TournamentEngine.Dashboard/Hubs/TournamentHub.cs`

**New Events:**
- `BotSubmittedEvent` - New bot submission
- `BotValidatedEvent` - Bot validation completed
- `BotDeletedEvent` - Bot removed
- `BotListUpdatedEvent` - Any bot list change
- `ValidationProgressEvent` - Re-validation in progress

---

## UI Specification

### View Modes

#### 1. List View (Default)

**Display:**
```
┌─ BOT SUBMISSION DASHBOARD ────────────────────────────────────────┐
│                                                                    │
│ Total Bots: 8  |  Valid: 6  ✅  |  Invalid: 1  ❌  |  Pending: 1  ⏳
│                                                                    │
│ RECENT SUBMISSIONS (Last 10 bots)                                │
├─────────────────────────────────────────────────────────────────┤
│ Team Name      │ Submission    │ Status │ Files │ Size  │ Action│
├─────────────────────────────────────────────────────────────────┤
│ TeamRocket     │ 2026-02-13    │ ✅    │   3   │ 12KB  │ View  │
│                │ 10:30:00      │ Valid │       │       │ Delete│
├─────────────────────────────────────────────────────────────────┤
│ BlueTeam       │ 2026-02-13    │ ❌    │   1   │  5KB  │ View  │
│                │ 10:25:00      │ Invalid│      │       │ Delete│
│                │ Error: ...                                      │
├─────────────────────────────────────────────────────────────────┤
│ GreenBot       │ 2026-02-13    │ ⏳    │   2   │ 15KB  │ View  │
│                │ 10:20:00      │ Pending│      │       │ Re-val│
├─────────────────────────────────────────────────────────────────┤
```

**Table Columns:**
- Team Name (clickable → Detail view)
- Submission Time (formatted, sortable)
- Status (Valid ✅ / Invalid ❌ / Pending ⏳)
- File Count (searchable)
- Total Size (human-readable)
- Last Updated (relative time)
- Version (current version number)
- Actions (View, Validate, Delete)

**Sorting:**
- By submission time (newest first)
- By team name (A-Z)
- By status (Valid → Pending → Invalid)
- By file count

**Filtering:**
- Search by team name (real-time)
- Filter by status (Valid / Invalid / Pending)
- Filter by date range (submitted in last X hours)

**Pagination:**
- 10 / 25 / 50 bots per page
- Jump to page input

#### 2. Detail View

**Display:**
```
┌─ BOT DETAILS: TEAMROCKET ─────────────────────────────────────────┐
│                                                                     │
│ Status: ✅ VALID                                                   │
│ Version: v2 (Latest)                                               │
│ Submission Time: 2026-02-13 10:30:00 UTC                          │
│ Last Updated:    2026-02-13 10:30:00 UTC                          │
│                                                                     │
│ FILES (3 total, 12.5 KB)                                           │
│ • Bot.cs         (8.2 KB)   [Copy] [Download]                     │
│ • AI.cs          (2.1 KB)   [Copy] [Download]                     │
│ • Utils.cs       (2.2 KB)   [Copy] [Download]                     │
│                                                                     │
│ VERSION HISTORY                                                    │
│ v2  | 2026-02-13 10:30:00 | ✅ Valid   | 3 files | 12.5 KB        │
│ v1  | 2026-02-13 10:00:00 | ✅ Valid   | 1 file  | 8.0 KB         │
│                                                                     │
│ ACTIONS                                                            │
│ [Re-Validate] [Download All] [Delete]                            │
│                                                                     │
```

**Sections:**
1. Status badge and metadata
2. File listing with download options
3. Version history timeline
4. Compilation errors (if any)
5. Action buttons

**Actions:**
- View source code (inline)
- Download bot (zip of all files)
- Re-validate (async, shows progress)
- Delete submission
- View version details
- Copy compilation errors to clipboard

#### 3. Validation Error View

**Display (Inline in detail):**
```
┌─ COMPILATION ERRORS ──────────────────────────────────────────────┐
│ ❌ Build Failed                                                     │
│                                                                     │
│ Error CS0103: 'InvalidMethod' does not exist in the current scope │
│ File: Bot.cs, Line 42, Column 15                                  │
│                                                                     │
│ Error CS0246: The type or namespace 'UnknownClass' could not be   │
│ File: Bot.cs, Line 55, Column 8                                   │
│                                                                     │
│ [Copy Errors] [Download Source]                                   │
│                                                                     │
```

---

## Technical Implementation Steps

### Step 1: Create Bot Dashboard Data Service

**TDD Tests:**
- `GetAllBotsAsync_ReturnsAllBots`
- `GetBotDetailsAsync_ReturnsBotWithMetadata`
- `ValidateBotAsync_CallsLoaderAndUpdatesStatus`
- `SearchBots_FiltersByTeamName`
- `SortBots_BySubmissionTime`

**Implementation:**
- `BotDashboardService` class
- Methods for CRUD + validation
- Caching layer (optional)
- Error handling

### Step 2: Create API Endpoints

**TDD Tests:**
- `GetBotDetails_ValidBot_Returns200`
- `GetBotDetails_InvalidBot_Returns400`
- `ValidateBot_Calls Loader_Returns Result`
- `GetBotHistory_ReturnAllVersions`

**Implementation:**
- New endpoint methods in `BotEndpoints.cs`
- Request/response models
- Error handling with HTTP status codes

### Step 3: Extend SignalR Hub

**TDD Tests:**
- `BotSubmitted_EmitsEvent_ClientReceives`
- `BotValidated_EmitsEvent_ClientReceives`
- `BotDeleted_EmitsEvent_ClientReceives`

**Implementation:**
- New hub methods
- Event broadcasting
- Client-side listeners

### Step 4: Build Dashboard UI

**Implementation:**
- Razor page or Vue.js component
- HTML/CSS for list and detail views
- JavaScript for interactions
- Real-time SignalR integration
- Sorting, filtering, search
- Pagination

### Step 5: Add Real-Time Updates

**Implementation:**
- SignalR event listeners
- Auto-refresh on changes
- Progress indicators for validation
- Toast notifications
- Error message display

### Step 6: Styling & Responsiveness

**Implementation:**
- Bootstrap/Tailwind integration
- Mobile-responsive design
- Accessibility (WCAG 2.1 AA)
- Dark mode support (optional)

---

## Data Flow

### Bot Submission → Display

```
1. Bot submitted via API (Step 13)
2. Stored in bots/ directory
3. BotDashboardService notified
4. BotValidationInProgress event sent
5. BotLoader validates bot (Step 12)
6. ValidationState updated (Valid/Invalid)
7. BotValidated event sent
8. Dashboard updates in real-time
```

### List View Refresh

```
1. User opens dashboard
2. GET /api/bots/list called
3. Response includes all bots + metadata
4. List rendered with status badges
5. Auto-refresh every 5 seconds (configurable)
6. SignalR events trigger immediate updates
```

### Detail View Validation

```
1. User clicks "Validate" button
2. POST /api/bots/{team}/validate called
3. Progress indicator shown
4. BotLoader compiles and validates
5. Result returned (errors or success)
6. Detail view updated
7. Toast notification shown
```

---

## Configuration

### appsettings.json

```json
{
  "BotDashboard": {
    "Enabled": true,
    "RefreshIntervalSeconds": 5,
    "MaxBotsPerPage": 25,
    "ShowVersionHistory": true,
    "AllowDelete": true,
    "AllowRevalidate": true,
    "CacheDurationSeconds": 30
  }
}
```

---

## Error Handling

### Scenarios

1. **Bot not found**
   - Display 404 message
   - Suggest refresh

2. **Validation in progress**
   - Show loading spinner
   - Disable actions
   - Display eta (if available)

3. **Network error**
   - Show retry button
   - Cache last known state
   - Toast notification

4. **Compilation error**
   - Display errors with line numbers
   - Show affected files
   - Suggest fixes (if possible)
   - Copy-to-clipboard for error text

5. **Deletion failed**
   - Show error message
   - Keep bot in list
   - Log detailed error

---

## Testing Strategy

### Unit Tests

```csharp
[TestClass]
public class BotDashboardServiceTests
{
    [TestMethod]
    public async Task GetAllBots_ReturnsAllWithStatus()
    
    [TestMethod]
    public async Task ValidateBot_CallsLoader_UpdatesStatus()
    
    [TestMethod]
    public async Task SearchBots_FiltersByTeamName()
}
```

### Integration Tests

```csharp
[TestClass]
public class BotDashboardIntegrationTests
{
    [TestMethod]
    public async Task API_GetBotList_Returns200()
    
    [TestMethod]
    public async Task API_ValidateBot_ReturnsValidationResult()
    
    [TestMethod]
    public async Task SignalR_BotSubmitted_NotifiesAllClients()
}
```

### UI Tests (Selenium/Playwright)

```javascript
describe('Bot Dashboard', () => {
    test('List view displays all bots', async () => {})
    test('Search filters bots by name', async () => {})
    test('Click detail opens detail view', async () => {})
    test('Re-validate button triggers validation', async () => {})
})
```

---

## Success Criteria

- ✅ Display all submitted bots with metadata (name, submission time, status, file count, size)
- ✅ Real-time status updates via SignalR
- ✅ Search and filter functionality
- ✅ Sorting by multiple columns
- ✅ Detail view showing files and version history
- ✅ Display compilation errors with helpful context
- ✅ Re-validate individual bots
- ✅ Delete bot submissions
- ✅ Download bot source code
- ✅ Mobile-responsive design
- ✅ Toast notifications for actions
- ✅ Progress indicators for long operations
- ✅ Comprehensive error handling
- ✅ All CRUD operations working
- ✅ Integration with Step 13 API
- ✅ Integration with Step 12 (BotLoader) for validation
- ✅ Unit tests covering all services (80%+ coverage)
- ✅ Integration tests for API endpoints
- ✅ UI functional tests for critical paths

---

## Files to Create

1. `TournamentEngine.Dashboard/Services/BotDashboardService.cs`
2. `TournamentEngine.Api/Endpoints/BotDashboardEndpoints.cs` (extended endpoints)
3. `TournamentEngine.Dashboard/Pages/BotsPage.cshtml` or `wwwroot/bots.html`
4. `TournamentEngine.Dashboard/wwwroot/js/bot-dashboard.js`
5. `TournamentEngine.Dashboard/wwwroot/css/bot-dashboard.css`
6. `TournamentEngine.Tests/BotDashboardServiceTests.cs`
7. `TournamentEngine.Tests/Integration/BotDashboardIntegrationTests.cs`
8. `TournamentEngine.Tests/BotDashboardUITests.cs` (optional)

---

## Dependencies

### NuGet Packages
- (All existing packages should be sufficient)
- Optional: Selenium/Playwright for UI testing

### Internal Dependencies
- Step 13 (BotStorageService)
- Step 12 (BotLoader for re-validation)
- TournamentEngine.Dashboard (SignalR, Razor Pages)
- TournamentEngine.Api (HTTP endpoints)

---

## Future Enhancements (Out of Scope)

1. **Advanced Features:**
   - Bot performance metrics (if tournament run)
   - Comparison view (compare bots side-by-side)
   - Visual bot health score
   - Notification system (email on validation failure)

2. **Security:**
   - Authentication/authorization for dashboard access
   - Audit log for bot operations
   - Rate limiting on re-validation
   - Code sanitization before display

3. **Performance:**
   - Async validation queue (instead of immediate)
   - Batch validation API
   - Compression for large file downloads
   - Database storage instead of JSON files

4. **UX Improvements:**
   - Drag-and-drop upload for new bots
   - Bot template library
   - Inline code editor
   - Live preview of compilation results
   - Webhook integration for team notifications

---

## Status

**Current:** NOT STARTED - Depends on Step 13 completion (DONE)

**Approach:** Test-Driven Development (TDD) - Write tests first, then implementation

**Next:** Step 1 - Create Bot Dashboard Data Service with TDD tests

---

