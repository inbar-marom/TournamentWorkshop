# Step 7: Live Tournament Dashboard - Implementation Plan

## Overview

Build a real-time web dashboard service that provides live tournament visualization and status updates. The service runs locally with tournament execution but exposes a web UI accessible remotely via browser, enabling spectators to watch tournament progress in real-time.

---

## Architecture

### Service Model: **Dual-Process Architecture**

```
┌─────────────────────────────────────────┐
│   Tournament Engine (Console/Core)     │
│   - Runs tournaments                    │
│   - Publishes events via WebSocket      │
│   - Updates shared state store          │
└──────────────┬──────────────────────────┘
               │ WebSocket / SignalR
               │ In-Memory State
               ▼
┌─────────────────────────────────────────┐
│   Dashboard Web Service (ASP.NET Core)  │
│   - Hosts web UI (Blazor/React)        │
│   - Serves static files                 │
│   - Broadcasts to connected clients     │
│   - Exposes REST API for queries        │
└──────────────┬──────────────────────────┘
               │ HTTP/WebSocket
               ▼
┌─────────────────────────────────────────┐
│   Browser Clients (Remote)              │
│   - Real-time dashboard UI              │
│   - Auto-updates on state changes       │
│   - Responsive visualization            │
└─────────────────────────────────────────┘
```

---

## Components

### 1. **Tournament Event Publisher** (TournamentEngine.Core)

**Location:** `TournamentEngine.Core/Events/`

**Responsibilities:**
- Publish tournament state changes as events
- Notify connected dashboard service of updates
- Maintain lightweight state snapshot

**Events to Publish:**
- `TournamentStarted` - Series begins
- `TournamentGameChanged` - New game type in series
- `RoundStarted` - New round begins with match pairings
- `MatchCompleted` - Individual match finishes with result
- `GroupStageCompleted` - Group stage done, moving to finals
- `StandingsUpdated` - Rankings/scores change
- `TournamentCompleted` - Entire tournament/series finishes

**Implementation Options:**

**SignalR Hub** (Recommended)
```csharp
public class TournamentHubPublisher : ITournamentEventPublisher
{
    private readonly IHubContext<TournamentHub> _hubContext;
    
    public async Task PublishMatchCompleted(MatchCompletedEvent evt)
    {
        await _hubContext.Clients.All.SendAsync("MatchCompleted", evt);
    }
    
    public async Task PublishStandingsUpdated(StandingsUpdatedEvent evt)
    {
        await _hubContext.Clients.All.SendAsync("StandingsUpdated", evt);
    }
}

---

### 2. **Dashboard Web Service** (New Project)

**Project:** `TournamentEngine.Dashboard`

**Stack:** ASP.NET Core 8.0 Web API + Static File Hosting

**NuGet Packages:**
- `Microsoft.AspNetCore.SignalR` - Real-time communication
- `Microsoft.AspNetCore.SpaServices` - SPA integration (if using React)
- `Swashbuckle.AspNetCore` - API documentation (optional)

**Structure:**
```
TournamentEngine.Dashboard/
├── Program.cs                    # Web host setup
├── Hubs/
│   └── TournamentHub.cs          # SignalR hub for real-time updates
├── Controllers/
│   └── TournamentApiController.cs # REST endpoints for state queries
├── Models/
│   ├── TournamentStateDto.cs     # Dashboard data models
│   ├── MatchDto.cs
│   ├── StandingDto.cs
│   └── SeriesProgressDto.cs
├── Services/
│   ├── StateManagerService.cs    # Manages current tournament state
│   └── HistoryService.cs         # Stores past events/matches
└── wwwroot/                      # Static files for UI
    ├── index.html
    ├── css/
    ├── js/
    └── assets/
```

**Key Endpoints:**

**REST API:**
- `GET /api/tournament/current` - Current tournament state snapshot
- `GET /api/tournament/series-progress` - Series progress overview
- `GET /api/tournament/standings` - Current standings
- `GET /api/tournament/groups` - Group assignments and standings
- `GET /api/tournament/matches/recent` - Recent match results
- `GET /api/tournament/matches/upcoming` - Upcoming matches (if known)
- `GET /api/tournament/history` - Past tournaments in series

**SignalR Hub Methods:**
- `SubscribeToTournament` - Client connects
- `GetCurrentState` - Request immediate state snapshot
- Server pushes: `MatchCompleted`, `StandingsUpdated`, `RoundStarted`, etc.

---

### 3. **Web UI** (Browser Client)

**Technology Options:**

**Option A: Blazor Server** (Recommended for .NET ecosystem)
- Native SignalR integration
- C# on client and server
- Component-based architecture

**Option B: React + TypeScript**
- Modern SPA framework
- Rich ecosystem
- Better for complex visualizations

**Option C: Plain HTML/JS + SignalR Client**
- Simplest to deploy
- No build process
- Lightweight

**UI Structure:**

```
Dashboard Layout:
┌─────────────────────────────────────────────────────┐
│ Tournament Dashboard              [Live●] [Series 1]│
├─────────────────────────────────────────────────────┤
│                                                      │
│  Series Progress:                                    │
│  [Game 1: RPSLS ✓] [Game 2: Blotto ⏳] [Game 3: ... ]│
│                                                      │
│  Current Tournament: Colonel Blotto (Round 3/5)     │
│  ────────────────────────────────────────────        │
│                                                      │
│  🏆 Overall Leaders:                                 │
│  1. TeamAlpha        245 pts  (2 tournament wins)    │
│  2. TeamBeta         230 pts  (1 tournament win)     │
│  3. TeamGamma        210 pts  (0 tournament wins)    │
│                                                      │
│  📊 Group Standings:                                 │
│  ┌─ Group A ───────┐  ┌─ Group B ───────┐          │
│  │ 1. TeamAlpha    ││  │ 1. TeamDelta    ││         │
│  │ 2. TeamBeta     ││  │ 2. TeamEpsilon  ││         │
│  │ 3. TeamGamma    ││  │ 3. TeamZeta     ││         │
│  └─────────────────┘  └─────────────────┘          │
│                                                      │
│  🎮 Recent Matches:                                  │
│  TeamAlpha vs TeamBeta  →  Alpha wins 8-7           │
│  TeamGamma vs TeamDelta →  Draw 5-5                  │
│                                                      │
│  ⏭️  Next Up:                                        │
│  TeamAlpha vs TeamGamma  (Starting in 2s...)         │
│                                                      │
└─────────────────────────────────────────────────────┘
```

---

## UI Components & Features

### 1. **Series Progress Tracker**

**Display:**
- List of all tournaments in series
- Visual progress indicator (completed ✓ / in-progress ⏳ / pending ○)
- Game type for each tournament
- Click to view detailed tournament results

**Data:**
```csharp
public class SeriesProgressDto
{
    public string SeriesId { get; set; }
    public List<TournamentInSeriesDto> Tournaments { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public int CurrentTournamentIndex { get; set; }
}

public class TournamentInSeriesDto
{
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public TournamentStatus Status { get; set; } // Pending, InProgress, Completed
    public string? Champion { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
```

---

### 2. **Current Tournament Status**

**Display:**
- Tournament number in series (e.g., "Tournament 2 of 4")
- Game type (RPSLS, Blotto, etc.)
- Current stage (Group Stage Round 3, Finals Round 1, etc.)
- Progress bar: Matches completed vs total matches
- Estimated time remaining (optional)

**Data:**
```csharp
public class CurrentTournamentDto
{
    public int TournamentNumber { get; set; }
    public GameType GameType { get; set; }
    public TournamentStage Stage { get; set; } // GroupStage, Finals
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public int MatchesCompleted { get; set; }
    public int TotalMatches { get; set; }
    public double ProgressPercentage { get; set; }
}
```

---

### 3. **Overall Series Leaderboard**

**Display:**
- Top 10 bots by total series score
- Rank, team name, total points, tournament wins
- Highlight changes (↑ moved up, ↓ moved down)
- Color-coded top 3 (gold, silver, bronze)

**Features:**
- Auto-scroll to show more teams
- Click team to see detailed stats
- Filter by group (optional)

**Data:**
```csharp
public class SeriesLeaderboardDto
{
    public List<TeamStandingDto> Standings { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class TeamStandingDto
{
    public int Rank { get; set; }
    public string TeamName { get; set; }
    public int TotalPoints { get; set; }
    public int TournamentWins { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int RankChange { get; set; } // +N or -N since last update
}
```

---

### 4. **Group Standings View**

**Display:**
- Grid layout with all groups side-by-side
- Each group shows standings table
- Highlight group leaders (advance to finals)
- Show wins, losses, points per group

**Features:**
- Groups update in real-time as matches complete
- Visual indicator for teams advancing to finals
- Click group to expand detailed match history

**Data:**
```csharp
public class GroupStandingsDto
{
    public List<GroupDto> Groups { get; set; }
}

public class GroupDto
{
    public string GroupName { get; set; } // "Group A", "Group B"
    public List<BotRankingDto> Rankings { get; set; }
}

public class BotRankingDto
{
    public int Rank { get; set; }
    public string TeamName { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Points { get; set; }
}
```

---

### 5. **Live Match Feed**

**Display:**
- Scrolling list of recent matches (last 10-20)
- Show: Team A vs Team B → Result
- Timestamp per match
- Color-coded results (win/loss/draw)
- Auto-update as new matches complete

**Features:**
- "Match in progress" indicator with animation
- Click match to see detailed breakdown
- Filter by team name

**Data:**
```csharp
public class RecentMatchDto
{
    public string MatchId { get; set; }
    public string Bot1Name { get; set; }
    public string Bot2Name { get; set; }
    public MatchOutcome Outcome { get; set; }
    public string? WinnerName { get; set; }
    public int Bot1Score { get; set; }
    public int Bot2Score { get; set; }
    public DateTime CompletedAt { get; set; }
    public GameType GameType { get; set; }
}
```

---

### 6. **Next Match Preview**

**Display:**
- Show upcoming match pairing
- Countdown timer until match starts
- Team statistics comparison
- Head-to-head history (if available)

**Optional:**
- Predicted winner based on past performance
- Live match view (if watching individual match execution)

---

### 7. **Statistics Dashboard**

**Display:**
- Total matches played in series
- Matches by game type (pie chart or bar chart)
- Average match duration
- Most wins, highest score, most draws
- Win rate by game type

**Visualizations:**
- Charts using Chart.js or similar library
- Real-time updates

---

## Real-Time Communication Flow

### 1. **Tournament Engine → Dashboard Service**

**When match completes:**
```csharp
// In TournamentManager.cs
var result = await _gameRunner.ExecuteMatch(bot1, bot2, gameType, ct);
await _eventPublisher.PublishMatchCompleted(new MatchCompletedEvent
{
    MatchId = Guid.NewGuid().ToString(),
    Bot1Name = bot1.TeamName,
    Bot2Name = bot2.TeamName,
    Result = result,
    Timestamp = DateTime.UtcNow
});
```

**When standings update:**
```csharp
// After scoring system calculates standings
var standings = _scoringSystem.GetCurrentRankings(tournamentInfo);
await _eventPublisher.PublishStandingsUpdated(new StandingsUpdatedEvent
{
    Standings = standings,
    Timestamp = DateTime.UtcNow
});
```

---

### 2. **Dashboard Service → Browser Clients**

**SignalR Hub:**
```csharp
public class TournamentHub : Hub
{
    public async Task SubscribeToUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "TournamentViewers");
    }
    
    public async Task GetCurrentState()
    {
        var state = await _stateManager.GetCurrentStateAsync();
        await Clients.Caller.SendAsync("CurrentState", state);
    }
}

// Broadcasting from service
public class StateManagerService
{
    private readonly IHubContext<TournamentHub> _hubContext;
    
    public async Task BroadcastMatchCompleted(MatchCompletedEvent evt)
    {
        await _hubContext.Clients.Group("TournamentViewers")
            .SendAsync("MatchCompleted", evt);
    }
}
```

---

### 3. **Browser Client (JavaScript/TypeScript)**

**SignalR Client Connection:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/tournamentHub")
    .withAutomaticReconnect()
    .build();

connection.on("MatchCompleted", (match) => {
    updateMatchFeed(match);
    playMatchAnimation(match);
});

connection.on("StandingsUpdated", (standings) => {
    updateLeaderboard(standings);
    highlightChanges(standings);
});

connection.on("RoundStarted", (roundInfo) => {
    updateTournamentStatus(roundInfo);
    showNextMatches(roundInfo.upcomingMatches);
});

await connection.start();
await connection.invoke("SubscribeToUpdates");
```

---

## Configuration

### Dashboard Service Settings

**appsettings.json:**
```json
{
  "Dashboard": {
    "Port": 5000,
    "EnableCors": true,
    "AllowedOrigins": ["http://localhost:3000"],
    "MaxConcurrentConnections": 1000,
    "StateUpdateIntervalMs": 500,
    "EnableHistory": true,
    "MaxHistoryItems": 1000
  },
  "SignalR": {
    "EnableMessagePack": true,
    "KeepAliveInterval": "00:00:15",
    "ClientTimeoutInterval": "00:00:30"
  }
}
```

---

## Deployment & Access

### Running Locally with Remote Access

**Option 1: Direct Network Access**
```bash
# Start dashboard service on all network interfaces
dotnet run --urls "http://0.0.0.0:5000"

# Access from remote browser:
http://<your-ip-address>:5000
```

**Option 2: Ngrok Tunnel** (for internet access)
```bash
# Start dashboard service
dotnet run --urls "http://localhost:5000"

# Create public tunnel
ngrok http 5000

# Access from anywhere:
https://<ngrok-subdomain>.ngrok.io
```

**Option 3: Azure/Cloud Deployment**
- Deploy dashboard service to Azure App Service
- Tournament engine publishes events to Azure SignalR Service
- Fully scalable, public access

---

## Implementation Phases

### Phase 1: Core Infrastructure ✅
- [x] Create `TournamentEngine.Dashboard` project
- [x] Set up ASP.NET Core Web API
- [x] Add SignalR hub
- [x] Create event publisher interface in Core

### Phase 2: Event System
- [ ] Implement `ITournamentEventPublisher` interface
- [ ] Add event publishing to `TournamentManager`
- [ ] Add event publishing to `TournamentSeriesManager`
- [ ] Test event flow with console logging

### Phase 3: State Management
- [ ] Create `StateManagerService` in dashboard
- [ ] Implement state snapshot DTOs
- [ ] Build REST API endpoints
- [ ] Add in-memory state caching

### Phase 4: Basic UI
- [ ] Create HTML/CSS dashboard layout
- [ ] Add SignalR client connection
- [ ] Implement leaderboard component
- [ ] Implement match feed component

### Phase 5: Advanced Visualizations
- [ ] Add group standings grid
- [ ] Add series progress tracker
- [ ] Add real-time charts
- [ ] Add match details popup

### Phase 6: Polish & Features
- [ ] Add animations for updates
- [ ] Add sound effects (optional)
- [ ] Add dark mode toggle
- [ ] Add responsive mobile design
- [ ] Add export/share functionality

---

## Testing Strategy

### Unit Tests
- Event publisher correctly formats events
- State manager updates state correctly
- Hub broadcasts to connected clients

### Integration Tests
- Tournament engine publishes events during actual tournament
- Dashboard receives and processes events
- Browser client receives SignalR messages

### Load Tests
- 100+ concurrent browser connections
- Rapid match completion events (stress test)
- State consistency under high load

---

## Additional Ideas & Enhancements

### 1. **Match Replay**
- Store all match results in history
- "Replay Tournament" feature to watch past tournaments
- Timeline scrubber to jump to specific rounds

### 2. **Bot Profile Pages**
- Click bot name to see detailed stats
- Win/loss graph over time
- Performance by game type
- Code snippet preview (if enabled)

### 3. **Tournament Brackets Visualization**
- Visual bracket tree for finals
- Show advancement paths
- Click nodes to see match details

### 4. **Prediction System**
- AI predictions for upcoming matches
- Community voting on winners
- Accuracy tracking

### 5. **Chat/Commentary**
- Live chat for spectators
- Admin commentary on interesting matches
- Highlight reel of best moments

### 6. **Mobile App**
- Native iOS/Android apps
- Push notifications for match results
- Swipe gestures for navigation

### 7. **Multi-Tournament History**
- View past tournament series
- Compare performance across tournaments
- Hall of Fame for best bots

### 8. **Admin Controls**
- Pause/resume tournament
- Skip to next round
- Disqualify bot
- Restart tournament

### 9. **Spectator Modes**
- "Theater mode" - fullscreen visualizations
- "Minimal mode" - just scores
- "Detailed mode" - full match logs

### 10. **Export & Sharing**
- Download tournament results as JSON/CSV
- Share live dashboard link
- Embed widget in external website
- Screenshot/video recording

---

## Success Criteria

### Functional Requirements ✅
- [ ] Dashboard service runs independently from tournament engine
- [ ] Real-time updates arrive within 500ms of event
- [ ] Browser clients auto-reconnect on disconnect
- [ ] UI displays all required information accurately
- [ ] Service handles 50+ concurrent connections
- [ ] Works on desktop and mobile browsers

### Non-Functional Requirements ✅
- [ ] Clean, professional UI design
- [ ] Smooth animations and transitions
- [ ] Accessible remotely via IP/URL
- [ ] Low latency (< 1s from event to display)
- [ ] Graceful degradation if tournament pauses
- [ ] Clear error messages if connection lost

---

## Dependencies

### NuGet Packages
- `Microsoft.AspNetCore.App` (ASP.NET Core runtime)
- `Microsoft.AspNetCore.SignalR` (real-time communication)
- `System.Text.Json` (JSON serialization)

### Frontend Libraries
- SignalR JavaScript Client
- Chart.js or similar (for visualizations)
- CSS framework (Bootstrap/Tailwind) for styling

### Infrastructure
- .NET 8 SDK
- Modern browser (Chrome, Firefox, Edge)
- Network access for remote viewing

---

## Next Steps

1. ✅ Create `TournamentEngine.Dashboard` project
2. ✅ Set up SignalR Hub
3. ✅ Define event DTOs
4. ✅ Implement event publisher in Core
5. ✅ Build basic HTML UI
6. ✅ Test end-to-end event flow
7. ✅ Deploy and test remote access

---

## Notes

- Keep state snapshots lightweight for fast broadcasting
- Use MessagePack for SignalR if performance becomes an issue
- Consider Redis for shared state if running multiple dashboard instances
- Add rate limiting to prevent event flooding
- Log all events for debugging and replay purposes
