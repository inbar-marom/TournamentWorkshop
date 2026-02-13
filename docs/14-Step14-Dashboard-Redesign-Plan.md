# Step 14: Dashboard Redesign - Implementation Plan

## Overview
Complete redesign of the Tournament Dashboard with updated terminology and enhanced UI/UX for real-time tournament monitoring.

## Dashboard Mock-up

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Productivity with AI Tournament Dashboard          🎮 In Progress │ ● Connected │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ Tournament Status Card                                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│ Event Progress (33% Complete)                                               │
│ ┌──────────────────────────────────────────────────────────────────┐       │
│ │████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│       │
│ │  [✓ RPSLS]  →  [⏳ Colonel Blotto]  →  [⏸️ Penalty Kicks]       │       │
│ └──────────────────────────────────────────────────────────────────┘       │
│                                                                              │
│ ┌──────────────────────────────┬──────────────────────────────────┐        │
│ │ Event Champions               │ Overall Leaders                  │        │
│ ├──────────────────────────────┼──────────────────────────────────┤        │
│ │                              │ ⚠️ Not Final                     │        │
│ │ Event: RPSLS                 │                                  │        │
│ │ 🏆 Winner: Team10            │ 🥇 1. Team10  19 pts             │        │
│ │                              │ 🥈 2. Team2   16 pts             │        │
│ │ Event: Colonel Blotto        │ 🥉 3. Team7   16 pts             │        │
│ │ ⏳ In Progress               │    4. Team5   14 pts             │        │
│ │                              │    5. Team3   12 pts             │        │
│ │ Event: Penalty Kicks         │                                  │        │
│ │ ⏸️ Pending                   │                                  │        │
│ └──────────────────────────────┴──────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ Events Details                                                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│ ┌──────────────────┬──────────────────────┬──────────────────────────┐    │
│ │ Recent Matches   │ Now Running Event    │ Group Standings          │    │
│ ├──────────────────┼──────────────────────┼──────────────────────────┤    │
│ │                  │ 🔴 LIVE              │ Group A (auto-cycles)    │    │
│ │ Team10 vs Team2  │                      │                          │    │
│ │ Colonel Blotto   │ Event: Colonel       │ 1. Team10    9 pts       │    │
│ │ Winner: Team10   │        Blotto        │ 2. Team2     7 pts       │    │
│ │ (8-2)            │                      │ 3. Team5     5 pts       │    │
│ │                  │ Stage: Group Stage   │                          │    │
│ │ Team7 vs Team3   │                      │ (switches to Group B     │    │
│ │ Colonel Blotto   │ Match 45/135         │  after 5 seconds)        │    │
│ │ Winner: Team7    │                      │                          │    │
│ │ (6-4)            │ Groups Active: A,B   │                          │    │
│ │                  │                      │                          │    │
│ │ [Last 10 shown]  │ Progress: 33%        │                          │    │
│ │                  │                      │                          │    │
│ └──────────────────┴──────────────────────┴──────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ ▶ Connection Log (collapsed by default)                                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key UI Elements

1. **Top Bar**: Title + Tournament Status (🎮/🏆/⏸️) + Connection indicator
2. **Tournament Status Card**:
   - Progress bar showing completion percentage with event buttons
   - Event Champions: List of all events with status (🏆/⏳/⏸️)
   - Overall Leaders: Top 5 with medals (🥇🥈🥉) and "⚠️ Not Final" disclaimer
3. **Events Details Card**:
   - Recent Matches: Last 10 matches (bulk refreshed)
   - Now Running Event: Live event info with 🔴 LIVE indicator
   - Group Standings: Auto-cycles through all groups every 5 seconds
4. **Connection Log**: Collapsed `<details>` with [Get Current State] [Clear Log] [Clear All Data] buttons

## Terminology Changes

### Current → New Mapping
- **Series** → **Tournament** (the whole event)
- **Tournament** → **Event** (each game type: RPSLS, Colonel Blotto, Penalty Kicks)
- **Group Stage** → **Group Stage** (first level with multiple groups)
- Add: **Playoff Groups** (second level where top teams compete)
- **Match** → **Match** (unchanged - game between two teams)
- **Bot/Team** → **Team** (unchanged)

---

## Phase 1: Backend Foundation Changes

### 1.1 Core Model Updates

**Files to Modify:**
- `TournamentEngine.Core/Common/Dashboard/TournamentStateDto.cs`
- `TournamentEngine.Core/Tournament/TournamentInfo.cs`
- `TournamentEngine.Core/Tournament/TournamentSeriesInfo.cs`

**Changes:**
```csharp
// Rename TournamentSeriesInfo → TournamentInfo
// Rename TournamentInfo → EventInfo

public class TournamentInfo
{
    public Guid TournamentId { get; set; }
    public string TournamentName { get; set; }
    public TournamentStatus Status { get; set; }
    public List<EventInfo> Events { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class EventInfo
{
    public Guid EventId { get; set; }
    public string EventName { get; set; }
    public GameType GameType { get; set; }
    public EventStatus Status { get; set; }  // New enum
    public EventStage Stage { get; set; }    // New enum: GroupStage, PlayoffGroups
    public List<GroupInfo> Groups { get; set; }
    public string? WinnerTeamName { get; set; }
}

public enum EventStatus
{
    Pending,
    InProgress,
    Completed
}

public enum EventStage
{
    GroupStage,
    PlayoffGroups
}

public class GroupInfo
{
    public string GroupName { get; set; }  // "Group A", "Group B", "Playoff Group"
    public List<TeamStandingDto> Standings { get; set; }
    public bool IsActive { get; set; }
}
```

**New DTOs:**
- `EventProgressDto.cs` - Event status for progress bar
- `GroupStandingsDto.cs` - Group standings with cycling info
- `TournamentDashboardDto.cs` - Complete dashboard state

### 1.2 Event System Updates

**New Event Files:**
- `TournamentEngine.Core/Events/EventStartedDto.cs`
- `TournamentEngine.Core/Events/EventCompletedDto.cs`
- `TournamentEngine.Core/Events/EventStageChangedDto.cs`
- `TournamentEngine.Core/Events/GroupStandingsUpdatedDto.cs`

**Updated Events:**
```csharp
public class EventStartedDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; }
    public GameType GameType { get; set; }
    public int EventIndex { get; set; }
    public int TotalEvents { get; set; }
    public List<GroupInfo> Groups { get; set; }
}

public class EventStageChangedDto
{
    public Guid EventId { get; set; }
    public EventStage NewStage { get; set; }  // GroupStage → PlayoffGroups
    public GroupInfo PlayoffGroup { get; set; }
}

public class GroupStandingsUpdatedDto
{
    public Guid EventId { get; set; }
    public string GroupName { get; set; }
    public List<TeamStandingDto> Standings { get; set; }
    public EventStage Stage { get; set; }
}
```

### 1.3 Tournament Manager Updates

**Files to Modify:**
- `TournamentEngine.Core/Tournament/TournamentSeriesManager.cs` → Rename to `TournamentManager.cs`
- `TournamentEngine.Core/Tournament/TournamentManager.cs` → Rename to `EventManager.cs`

**Key Changes:**
1. Rename `RunSeriesAsync()` → `RunTournamentAsync()`
2. Add group stage tracking
3. Publish new events: `EventStarted`, `EventStageChanged`, `GroupStandingsUpdated`
4. Track event progress (Pending/InProgress/Completed)

**New Methods:**
```csharp
// In TournamentManager (formerly TournamentSeriesManager)
public class TournamentManager
{
    public async Task<TournamentInfo> RunTournamentAsync(
        List<IBot> teams, 
        TournamentConfig config)
    {
        // Publish TournamentStarted event
        // For each event in tournament:
        //   - Publish EventStarted
        //   - Run Group Stage
        //   - Publish EventStageChanged (to Playoff)
        //   - Run Playoff Groups
        //   - Publish EventCompleted
        // Publish TournamentCompleted
    }
    
    private async Task RunEventAsync(EventInfo eventInfo, List<IBot> teams)
    {
        // Run group stage
        await RunGroupStageAsync(eventInfo, teams);
        
        // Transition to playoff
        await PublishEventStageChangedAsync(eventInfo, EventStage.PlayoffGroups);
        
        // Run playoff groups
        await RunPlayoffGroupsAsync(eventInfo, topTeams);
    }
}
```

---

## Phase 2: Dashboard Backend Services

### 2.1 New Services

**TournamentDashboardService.cs**
```csharp
public class TournamentDashboardService
{
    public async Task<TournamentDashboardDto> BuildDashboardAsync()
    {
        return new TournamentDashboardDto
        {
            TournamentStatus = GetTournamentStatus(),
            EventProgress = GetEventProgress(),
            EventChampions = GetEventChampions(),
            OverallLeaders = GetOverallLeaders(),
            RecentMatches = GetRecentMatches(10),
            NowRunningEvent = GetNowRunningEvent(),
            GroupStandings = GetCurrentGroupStandings()
        };
    }
}
```

**GroupStandingsService.cs**
```csharp
public class GroupStandingsService
{
    private List<GroupInfo> _allGroups;
    private int _currentGroupIndex = 0;
    
    public GroupInfo GetCurrentGroup()
    {
        // Cycle through groups every 5 seconds
        // Return current group for display
    }
    
    public void UpdateGroupStandings(GroupStandingsUpdatedDto update)
    {
        // Update specific group standings
    }
}
```

**EventProgressService.cs**
```csharp
public class EventProgressService
{
    public List<EventProgressDto> GetEventProgress()
    {
        return _events.Select(e => new EventProgressDto
        {
            EventName = e.EventName,
            Status = e.Status,  // Pending, InProgress, Completed
            IsActive = e.Status == EventStatus.InProgress,
            WinnerTeamName = e.WinnerTeamName
        }).ToList();
    }
}
```

### 2.2 Updated Services

**StateManagerService.cs**
- Add group standings tracking
- Add event progress tracking
- Add tournament status tracking
- Implement bulk match refresh logic (every 10 seconds)

**RecentMatchesService.cs**
```csharp
public class RecentMatchesService
{
    private Queue<MatchCompletedDto> _matchBuffer = new();
    private DateTime _lastBulkRefresh = DateTime.UtcNow;
    
    public List<MatchCompletedDto> GetRecentMatchesForRefresh()
    {
        var now = DateTime.UtcNow;
        var shouldRefresh = _matchBuffer.Count >= 10 || 
                           (now - _lastBulkRefresh).TotalSeconds >= 10;
        
        if (shouldRefresh)
        {
            _lastBulkRefresh = now;
            return _matchBuffer.TakeLast(10).ToList();
        }
        
        return null; // No refresh needed
    }
}
```

### 2.3 API Controller Updates

**TournamentApiController.cs**
```csharp
[HttpGet("dashboard")]
public async Task<ActionResult<TournamentDashboardDto>> GetDashboard()
{
    var dashboard = await _dashboardService.BuildDashboardAsync();
    return Ok(dashboard);
}

[HttpGet("event-progress")]
public ActionResult<List<EventProgressDto>> GetEventProgress()
{
    return Ok(_progressService.GetEventProgress());
}

[HttpGet("group-standings/current")]
public ActionResult<GroupInfo> GetCurrentGroupStandings()
{
    return Ok(_groupStandingsService.GetCurrentGroup());
}

[HttpGet("recent-matches/bulk")]
public ActionResult<List<MatchCompletedDto>> GetRecentMatchesBulk()
{
    var matches = _recentMatchesService.GetRecentMatchesForRefresh();
    return Ok(matches ?? new List<MatchCompletedDto>());
}
```

---

## Phase 3: Frontend Implementation

### 3.1 HTML Structure

**index.html - Complete Redesign**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <title>Productivity with AI Tournament Dashboard</title>
    <!-- Existing head content -->
</head>
<body>
    <!-- Top Bar -->
    <header class="top-bar">
        <h1>Productivity with AI Tournament Dashboard</h1>
        <div class="status-indicators">
            <div class="tournament-status" id="tournamentStatus">
                <span class="status-icon" id="tournamentIcon">⏸️</span>
                <span id="tournamentStatusText">Not Started</span>
            </div>
            <div class="connection-status">
                <span class="status-indicator" id="statusIndicator"></span>
                <span id="statusText">Connecting...</span>
            </div>
        </div>
    </header>

    <!-- Tournament Status Card -->
    <section class="tournament-status-card">
        <div class="progress-section">
            <div class="event-progress-bar" id="eventProgressBar">
                <!-- Dynamic event buttons with progress -->
            </div>
        </div>
        
        <div class="status-grid">
            <!-- Event Champions Card -->
            <article class="event-champions-card">
                <h2>Event Champions</h2>
                <div id="eventChampions">
                    <!-- Dynamic event champion list -->
                </div>
            </article>
            
            <!-- Overall Leaders Card -->
            <article class="overall-leaders-card">
                <h2>Overall Leaders</h2>
                <div class="disclaimer" id="leaderboardDisclaimer">⚠️ Not Final</div>
                <div id="overallLeaders">
                    <!-- Dynamic leaderboard -->
                </div>
            </article>
        </div>
    </section>

    <!-- Events Details Card -->
    <section class="events-details-card">
        <h2>Events Details</h2>
        <div class="events-grid">
            <!-- Recent Matches -->
            <article class="recent-matches-card">
                <h3>Recent Matches</h3>
                <div id="recentMatches">
                    <!-- Last 10 matches -->
                </div>
            </article>
            
            <!-- Now Running Event -->
            <article class="now-running-card">
                <h3>Now Running Event</h3>
                <div class="live-indicator" id="liveIndicator">🔴 LIVE</div>
                <div id="nowRunningEvent">
                    <!-- Current event info -->
                </div>
            </article>
            
            <!-- Group Standings -->
            <article class="group-standings-card">
                <h3 id="groupStandingsTitle">Group Standings</h3>
                <div id="groupStandings">
                    <!-- Auto-cycling group standings -->
                </div>
            </article>
        </div>
    </section>

    <!-- Connection Log (Collapsed) -->
    <details class="connection-log" id="connectionLog">
        <summary>Connection Log</summary>
        <div id="messages"></div>
        <div class="log-controls">
            <button onclick="requestCurrentState()">Get Current State</button>
            <button onclick="clearLog()">Clear Log</button>
            <button onclick="clearAllData()" class="danger-btn">Clear All Data</button>
        </div>
    </details>

    <script src="/signalr/signalr.min.js"></script>
    <script src="/js/dashboard.js"></script>
</body>
</html>
```

### 3.2 CSS Styling

**wwwroot/css/dashboard.css** (New file)

```css
:root {
    --primary-color: #0f766e;
    --success-color: #16a34a;
    --warning-color: #f59e0b;
    --danger-color: #dc2626;
    --bg-primary: #f7f3e9;
    --bg-secondary: #ffffff;
    --text-primary: #1f2a2e;
    --text-muted: #5b6b73;
    --border-color: rgba(31, 42, 46, 0.12);
}

/* Top Bar */
.top-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 20px 30px;
    background: var(--bg-secondary);
    border-bottom: 2px solid var(--border-color);
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.top-bar h1 {
    font-size: 1.8em;
    color: var(--primary-color);
    font-weight: 700;
}

.status-indicators {
    display: flex;
    gap: 20px;
    align-items: center;
}

/* Tournament Status Card */
.tournament-status-card {
    margin: 20px;
    padding: 30px;
    background: var(--bg-secondary);
    border-radius: 16px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.event-progress-bar {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-bottom: 30px;
    padding: 20px;
    background: linear-gradient(90deg, var(--primary-color) 0%, var(--primary-color) var(--progress, 0%), #e5e7eb var(--progress, 0%));
    border-radius: 12px;
    position: relative;
}

.event-button {
    padding: 12px 20px;
    border: none;
    border-radius: 8px;
    background: white;
    cursor: default;
    font-weight: 600;
    transition: all 0.3s;
    position: relative;
}

.event-button.completed {
    background: var(--success-color);
    color: white;
}

.event-button.in-progress {
    background: var(--warning-color);
    color: white;
    animation: pulse 2s infinite;
}

.event-button.pending {
    background: #e5e7eb;
    color: var(--text-muted);
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.7; }
}

/* Status Grid */
.status-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
}

/* Event Champions */
.event-champions-card h2,
.overall-leaders-card h2 {
    margin-bottom: 15px;
    color: var(--primary-color);
    border-bottom: 2px solid var(--border-color);
    padding-bottom: 10px;
}

.event-item {
    padding: 12px;
    margin: 8px 0;
    background: #f9fafb;
    border-radius: 8px;
    border-left: 4px solid var(--primary-color);
}

.event-item.completed {
    border-left-color: var(--success-color);
}

.event-item.in-progress {
    border-left-color: var(--warning-color);
}

.event-item.pending {
    border-left-color: #d1d5db;
}

/* Overall Leaders */
.disclaimer {
    padding: 8px 12px;
    background: #fef3c7;
    color: #92400e;
    border-radius: 6px;
    margin-bottom: 15px;
    font-weight: 600;
    text-align: center;
}

.leader-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px;
    margin: 6px 0;
    background: #f9fafb;
    border-radius: 8px;
}

.leader-item.rank-1 {
    background: #fef3c7;
    font-size: 1.2em;
    font-weight: 700;
    border: 2px solid var(--warning-color);
}

.leader-item.rank-2,
.leader-item.rank-3 {
    font-weight: 600;
}

/* Events Details Card */
.events-details-card {
    margin: 20px;
    padding: 30px;
    background: var(--bg-secondary);
    border-radius: 16px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.events-grid {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    gap: 20px;
    margin-top: 20px;
}

/* Recent Matches */
.match-item {
    padding: 10px;
    margin: 6px 0;
    background: #f9fafb;
    border-radius: 6px;
    font-size: 0.9em;
}

.match-header {
    font-weight: 600;
    color: var(--primary-color);
    margin-bottom: 4px;
}

/* Now Running Event */
.live-indicator {
    display: inline-block;
    padding: 6px 12px;
    background: var(--danger-color);
    color: white;
    border-radius: 20px;
    font-size: 0.85em;
    font-weight: 700;
    animation: pulse 2s infinite;
    margin-bottom: 15px;
}

/* Group Standings */
.group-standings-card {
    position: relative;
}

.standings-item {
    display: flex;
    justify-content: space-between;
    padding: 10px;
    margin: 5px 0;
    background: #f9fafb;
    border-radius: 6px;
}

/* Connection Log */
.connection-log {
    margin: 20px;
    background: var(--bg-secondary);
    border-radius: 12px;
    padding: 15px;
}

.connection-log summary {
    cursor: pointer;
    font-weight: 600;
    color: var(--primary-color);
    padding: 10px;
}

#messages {
    max-height: 200px;
    overflow-y: auto;
    background: #f9fafb;
    padding: 10px;
    border-radius: 6px;
    font-family: monospace;
    font-size: 0.85em;
}

.danger-btn {
    background: var(--danger-color) !important;
}

/* Responsive */
@media (max-width: 1200px) {
    .events-grid {
        grid-template-columns: 1fr;
    }
    .status-grid {
        grid-template-columns: 1fr;
    }
}
```

### 3.3 JavaScript Implementation

**wwwroot/js/dashboard.js** (New file)

```javascript
// SignalR Connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/tournamentHub")
    .withAutomaticReconnect([0, 0, 1000, 3000, 5000])
    .build();

let groupCycleInterval;
let matchRefreshInterval;

// Connection Handlers
connection.on("TournamentStarted", (event) => {
    updateTournamentStatus("In Progress", "🎮");
    logMessage(`Tournament started: ${event.TournamentName}`, 'info');
    refreshDashboard();
});

connection.on("EventStarted", (event) => {
    updateEventProgress(event);
    updateNowRunningEvent(event);
    startGroupCycling(event.Groups);
    logMessage(`Event started: ${event.EventName}`, 'info');
});

connection.on("EventStageChanged", (event) => {
    if (event.NewStage === "PlayoffGroups") {
        stopGroupCycling();
        displayPlayoffGroup(event.PlayoffGroup);
    }
    logMessage(`Event stage changed to ${event.NewStage}`, 'info');
});

connection.on("EventCompleted", (event) => {
    updateEventProgress(event);
    updateEventChampions(event);
    logMessage(`Event completed: ${event.EventName} - Winner: ${event.WinnerTeamName}`, 'success');
});

connection.on("TournamentCompleted", (event) => {
    updateTournamentStatus("Completed", "🏆");
    finalizeOverallLeaders();
    logMessage(`Tournament completed! Champion: ${event.Champion}`, 'success');
});

connection.on("MatchCompleted", (match) => {
    // Buffer match for bulk refresh
    bufferMatch(match);
});

connection.on("GroupStandingsUpdated", (update) => {
    updateGroupStandings(update);
});

connection.on("StandingsUpdated", (standings) => {
    updateOverallLeaders(standings);
});

// Dashboard Update Functions
async function refreshDashboard() {
    try {
        const response = await fetch('/api/tournament/dashboard');
        const dashboard = await response.json();
        
        updateEventProgressBar(dashboard.EventProgress);
        updateEventChampions(dashboard.EventChampions);
        updateOverallLeaders(dashboard.OverallLeaders);
        updateNowRunningEvent(dashboard.NowRunningEvent);
        displayGroupStandings(dashboard.GroupStandings);
    } catch (err) {
        logMessage(`Dashboard refresh error: ${err}`, 'error');
    }
}

function updateEventProgressBar(events) {
    const progressBar = document.getElementById('eventProgressBar');
    const completedCount = events.filter(e => e.Status === 'Completed').length;
    const progress = (completedCount / events.length) * 100;
    
    progressBar.style.setProperty('--progress', `${progress}%`);
    
    progressBar.innerHTML = events.map(event => `
        <button class="event-button ${event.Status.toLowerCase()}">
            ${event.EventName}
        </button>
    `).join('<span class="connector">→</span>');
}

function updateEventChampions(events) {
    const championsDiv = document.getElementById('eventChampions');
    
    championsDiv.innerHTML = events.map(event => {
        let statusIcon, statusText;
        
        if (event.Status === 'Completed') {
            statusIcon = '🏆';
            statusText = `Winner: ${event.WinnerTeamName}`;
        } else if (event.Status === 'InProgress') {
            statusIcon = '⏳';
            statusText = 'In Progress';
        } else {
            statusIcon = '⏸️';
            statusText = 'Pending';
        }
        
        return `
            <div class="event-item ${event.Status.toLowerCase()}">
                <strong>Event: ${event.EventName}</strong>
                <div>${statusIcon} ${statusText}</div>
            </div>
        `;
    }).join('');
}

function updateOverallLeaders(leaders) {
    const leadersDiv = document.getElementById('overallLeaders');
    const disclaimer = document.getElementById('leaderboardDisclaimer');
    
    const isComplete = leaders.IsFinal;
    disclaimer.style.display = isComplete ? 'none' : 'block';
    
    leadersDiv.innerHTML = leaders.TopTeams.slice(0, 5).map((team, index) => {
        const rank = index + 1;
        const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : '';
        const rankClass = `rank-${rank}`;
        const winnerLabel = isComplete && rank === 1 ? ' - TOURNAMENT WINNER' : '';
        
        return `
            <div class="leader-item ${rankClass}">
                <span>${medal} ${rank}. ${team.TeamName}${winnerLabel}</span>
                <span>${team.TotalPoints} pts</span>
            </div>
        `;
    }).join('');
}

// Group Cycling Logic
let currentGroupIndex = 0;
let allGroups = [];

function startGroupCycling(groups) {
    allGroups = groups;
    currentGroupIndex = 0;
    
    if (groupCycleInterval) {
        clearInterval(groupCycleInterval);
    }
    
    displayCurrentGroup();
    groupCycleInterval = setInterval(() => {
        currentGroupIndex = (currentGroupIndex + 1) % allGroups.length;
        displayCurrentGroup();
    }, 5000);
}

function displayCurrentGroup() {
    if (allGroups.length === 0) return;
    
    const group = allGroups[currentGroupIndex];
    const titleEl = document.getElementById('groupStandingsTitle');
    const standingsEl = document.getElementById('groupStandings');
    
    titleEl.textContent = group.GroupName;
    
    standingsEl.innerHTML = group.Standings.map((team, index) => `
        <div class="standings-item">
            <span>${index + 1}. ${team.TeamName}</span>
            <span>${team.Points} pts</span>
        </div>
    `).join('');
}

function stopGroupCycling() {
    if (groupCycleInterval) {
        clearInterval(groupCycleInterval);
        groupCycleInterval = null;
    }
}

function displayPlayoffGroup(playoffGroup) {
    const titleEl = document.getElementById('groupStandingsTitle');
    const standingsEl = document.getElementById('groupStandings');
    
    titleEl.textContent = 'Playoff Group';
    
    standingsEl.innerHTML = playoffGroup.Standings.map((team, index) => `
        <div class="standings-item">
            <span>${index + 1}. ${team.TeamName}</span>
            <span>${team.Points} pts</span>
        </div>
    `).join('');
}

// Recent Matches - Bulk Refresh
let matchBuffer = [];

function bufferMatch(match) {
    matchBuffer.push(match);
}

function startMatchRefresh() {
    matchRefreshInterval = setInterval(async () => {
        try {
            const response = await fetch('/api/tournament/recent-matches/bulk');
            const matches = await response.json();
            
            if (matches && matches.length > 0) {
                displayRecentMatches(matches);
                matchBuffer = [];
            }
        } catch (err) {
            console.error('Match refresh error:', err);
        }
    }, 10000);
}

function displayRecentMatches(matches) {
    const matchesDiv = document.getElementById('recentMatches');
    
    matchesDiv.innerHTML = matches.slice(0, 10).map(match => `
        <div class="match-item">
            <div class="match-header">${match.Bot1Name} vs ${match.Bot2Name}</div>
            <div>${match.EventName}</div>
            <div>Winner: ${match.WinnerName || 'Draw'} (${match.Bot1Score}-${match.Bot2Score})</div>
        </div>
    `).join('');
}

// Utility Functions
function updateTournamentStatus(status, icon) {
    document.getElementById('tournamentIcon').textContent = icon;
    document.getElementById('tournamentStatusText').textContent = status;
}

function logMessage(message, type = 'info') {
    const messagesDiv = document.getElementById('messages');
    const timestamp = new Date().toLocaleTimeString();
    messagesDiv.innerHTML += `<div class="log-${type}">[${timestamp}] ${message}</div>`;
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// Initialize
async function start() {
    try {
        await connection.start();
        document.getElementById('statusIndicator').classList.add('connected');
        document.getElementById('statusText').textContent = 'Connected';
        logMessage('Connected to tournament hub', 'success');
        
        await refreshDashboard();
        startMatchRefresh();
    } catch (err) {
        document.getElementById('statusIndicator').classList.add('disconnected');
        document.getElementById('statusText').textContent = 'Disconnected';
        logMessage(`Connection error: ${err}`, 'error');
        setTimeout(start, 5000);
    }
}

start();
```

---

## Phase 4: Implementation Steps

### Step 1: Backend Core (Week 1)
1. Create new DTOs and enums
2. Rename Series → Tournament, Tournament → Event throughout
3. Add EventStage enum and tracking
4. Update TournamentSeriesManager → TournamentManager
5. Update TournamentManager → EventManager

### Step 2: Event System (Week 1)
1. Create new event DTOs
2. Update event publishers in managers
3. Add group tracking and stage transitions
4. Test event flow with console logging

### Step 3: Dashboard Services (Week 2)
1. Implement TournamentDashboardService
2. Implement GroupStandingsService with cycling
3. Implement EventProgressService
4. Update StateManagerService for bulk matching
5. Update API controller endpoints

### Step 4: Frontend Structure (Week 2)
1. Create new HTML layout
2. Implement CSS styling
3. Remove old dashboard components
4. Add new card structure

### Step 5: Frontend JavaScript (Week 3)
1. Implement event handlers
2. Add group cycling logic
3. Add bulk match refresh (10 second timer)
4. Implement progress bar updates
5. Add medal/winner highlighting

### Step 6: Integration & Testing (Week 3)
1. End-to-end testing with simulator
2. Verify all events firing correctly
3. Test group cycling (5 second intervals)
4. Test match bulk refresh (10 second / full page)
5. Verify medal assignment for top 3
6. Test tournament status transitions

### Step 7: Polish & Documentation (Week 4)
1. Responsive design testing
2. Performance optimization
3. Update all documentation with new terminology
4. Create user guide
5. Update README files

---

## Testing Checklist

### Backend Tests
- [ ] TournamentManager publishes all events correctly
- [ ] EventManager handles group stage → playoff transition
- [ ] Group standings update correctly
- [ ] Event status tracking (Pending/InProgress/Completed)
- [ ] Bulk match refresh returns correct data

### Frontend Tests
- [ ] Progress bar updates with event completion
- [ ] Event champions display correct status icons
- [ ] Overall leaders show "Not Final" until tournament ends
- [ ] Medals (🥇🥈🥉) appear on top 3 when complete
- [ ] Tournament winner highlighted and labeled
- [ ] Recent matches bulk refresh every 10 seconds or full page
- [ ] Group standings cycle every 5 seconds
- [ ] Playoff group displays when stage changes
- [ ] Connection log collapsed by default

### Integration Tests
- [ ] Run full tournament with 3 events
- [ ] Verify real-time updates for all components
- [ ] Test with 10 teams across multiple groups
- [ ] Verify group → playoff transition
- [ ] Clear All Data button resets everything

---

## Migration Notes

### Database Changes
- No database changes required (in-memory state only)

### Breaking Changes
1. API endpoint changes:
   - `/api/tournament/series-view` may need renaming
   - New endpoints added for dashboard, progress, group standings

2. SignalR event names changed:
   - `SeriesStarted` → `TournamentStarted`
   - `SeriesCompleted` → `TournamentCompleted`
   - New events: `EventStarted`, `EventCompleted`, `EventStageChanged`, `GroupStandingsUpdated`

### Backward Compatibility
- Old event names could be kept temporarily with deprecation warnings
- Old API endpoints could redirect to new ones during migration period

---

## Success Criteria

1. ✅ Dashboard displays tournament with updated terminology
2. ✅ Event progress bar shows completion status visually
3. ✅ Event champions list updates in real-time
4. ✅ Overall leaders show top 5 with medals and "Not Final" disclaimer
5. ✅ Recent matches bulk refresh every 10 seconds
6. ✅ Group standings cycle through all groups every 5 seconds
7. ✅ Playoff group displays when event transitions from group stage
8. ✅ Connection log collapsed by default
9. ✅ All real-time events working via SignalR
10. ✅ Responsive design works on various screen sizes

---

## Timeline

- **Week 1**: Backend foundation and event system
- **Week 2**: Dashboard services and API updates
- **Week 3**: Frontend implementation and integration
- **Week 4**: Testing, polish, and documentation

**Total Estimated Time**: 4 weeks

---

## Files Summary

### New Files (25+)
- DTOs: `EventProgressDto`, `GroupStandingsDto`, `TournamentDashboardDto`, `EventInfo`, `GroupInfo`
- Events: `EventStartedDto`, `EventCompletedDto`, `EventStageChangedDto`, `GroupStandingsUpdatedDto`
- Services: `TournamentDashboardService`, `GroupStandingsService`, `EventProgressService`
- Frontend: `dashboard.css`, `dashboard.js`

### Modified Files (30+)
- All files containing "Series" → rename to "Tournament"
- All files containing "Tournament" (when referring to individual games) → rename to "Event"
- `TournamentHub.cs` - add new event handlers
- `TournamentApiController.cs` - new endpoints
- `StateManagerService.cs` - bulk refresh logic
- `index.html` - complete redesign

---

## Next Steps

1. Review and approve this plan
2. Create feature branch: `feature/dashboard-redesign`
3. Begin Phase 1: Backend Core implementation
4. Set up progress tracking in project management tool
