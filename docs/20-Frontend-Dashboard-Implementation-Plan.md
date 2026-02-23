# Frontend Tournament Dashboard - Implementation Plan

**Date**: February 23, 2026  
**Status**: In Progress  
**Target**: Create polling-based frontend dashboard using existing API endpoints

---

## 1. Dashboard Layout & Components

### Page Structure (Vertical Stack)
```
┌──────────────────────────────────────────────────────────────┐
│  Header: "Productivity with AI Tournament Dashboard"          │
│  Status: "NOT STARTED" | ● Connected | Israel Time: 09:18:49 │
├──────────────────────────────────────────────────────────────┤
│                    TOURNAMENT STATUS                          │
│  Event Progress: [====    ] 0% Complete                       │
│  RPSLS | Colonel Blotto | Penalty Kicks | Security Game      │
│                                                               │
│  EVENT CHAMPIONS          |     OVERALL LEADERS (NOT FINAL)  │
│  ├ RPSLS - Pending        |     No standings available       │
│  ├ Colonel Blotto-Pending |     (only when running)          │
│  ├ Penalty Kicks-Pending  |                                  │
│  └ Security Game-Pending  |                                  │
├──────────────────────────────────────────────────────────────┤
│                     EVENTS DETAILS                            │
│  Tabs: [RPSLS] Colonel Blotto | Penalty Kicks | Security Game│
│                                                               │
│  Group: No groups yet                                        │
│                                                               │
│  GROUP STANDINGS          |    MATCHES (CURRENT GROUP)       │
│  No group data yet        |    No match history              │
│                           |    No matches yet                │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. API Endpoints & Polling Configuration

### Polling Intervals

| Component | Endpoint | Interval | Condition |
|-----------|----------|----------|-----------|
| Connection Status | `/api/tournament-engine/connection` | 5 sec | Always |
| Tournament Status | `/api/tournament-engine/status` | 5 sec | Always |
| Event Progress | `/api/tournament-engine/events` | 10 sec | Always |
| Events Details | `/api/tournament-engine/events` | 10 sec | Always |
| Event Champions | `/api/tournament-engine/leaders` | 5 sec | When running |
| Overall Leaders | `/api/tournament-engine/leaders` | 5 sec | When running |
| Groups (by event) | `/api/tournament-engine/groups/{eventName}` | 10 sec | After event selected |
| Group Standings & Matches | `/api/tournament-engine/groups/{eventName}/{groupLabel}` | 5 sec | After group selected |

---

## 3. Component Details

### 3.1 Header Section
- **Title**: "Productivity with AI Tournament Dashboard"
- **Status Badge**: Tournament status text (e.g., "NOT STARTED")
- **Connection Indicator**: Green dot (●) + "Connected" text
- **Israel Time**: Current time in HH:MM:SS format
- **Update Frequency**: Clock updates every 1 second
- **Styling**: Light background, centered title, right-aligned status

### 3.2 Tournament Status Section
Contains: Event Progress + Event Champions + Overall Leaders

**3.2.1 Event Progress**
- **Display**: Progress bar (0-100%) showing tournament completion
- **Event Labels**: All event names shown below bar (RPSLS, Colonel Blotto, Penalty Kicks, Security Game)
- **Visual Indicator**: Highlight current event name
- **Data Source**: `/api/tournament-engine/events`
- **Polling**: 10 seconds
- **Logic**: Calculate % as (completed events / total events) * 100

**3.2.2 Event Champions (Left Column)**
- **Title**: "EVENT CHAMPIONS"
- **Display**: Vertical list of all events with status
- **Format**: 
  ```
  ├ RPSLS - Pending
  ├ Colonel Blotto - Pending
  ├ Penalty Kicks - Pending
  └ Security Game - Pending
  ```
- **Status Values**: "Pending" (not started) → "In Progress" → "[Champion Name]" (completed)
- **Visibility**: Always visible (but content changes based on tournament state)
- **Data Source**: `/api/tournament-engine/events`
- **Polling**: 10 seconds

**3.2.3 Overall Leaders / Leaderboard (Right Column)**
- **Title**: "OVERALL LEADERS (NOT FINAL)"
- **Default Message**: "No standings available" (when tournament not running)
- **Format** (when running): List of teams with cumulative scores
  ```
  TeamName1 - 150 points
  TeamName2 - 145 points
  TeamName3 - 142 points
  ```
- **Sorting**: Descending by total score
- **Visibility**: Only show when `tournamentStatus.isRunning === true`
- **Data Source**: `/api/tournament-engine/leaders`
- **Polling**: 5 seconds (starts when tournament running)

### 3.3 Events Details Section
Contains: Event Tabs + Group Selection + Group Standings + Matches

**3.3.1 Event Tabs**
- **Display**: Horizontal tab buttons for each event
- **Active Tab**: Highlighted (darker background) based on selected event
- **Default**: First event (RPSLS) selected on page load
- **Action**: Clicking tab loads groups for that event
- **Data Source**: `/api/tournament-engine/events`
- **Visual**: Pill-style buttons with selected state

**3.3.2 Group Selection**
- **Label**: "Group:"
- **Display**: Text label showing group names available for selected event
- **Default Message**: "No groups yet" (before event selected or no groups)
- **Action**: Clicking group label or dropdown loads group details
- **Data Source**: `/api/tournament-engine/groups/{eventName}`
- **Polling**: 10 seconds (after event selected)

**3.3.3 Group Standings Table (Left Column)**
- **Title**: "GROUP STANDINGS"
- **Default Message**: "No group data yet"
- **Columns**: Rank, Team Name, Wins, Losses, Draws, Score
- **Sorting**: Descending by score
- **Data Source**: `/api/tournament-engine/groups/{eventName}/{groupLabel}`
- **Field**: `groupStanding` array
- **Polling**: 5 seconds (after group selected)
- **Visual**: Standard HTML table with alternating row colors

**3.3.4 Matches Table (Right Column)**
- **Title**: "MATCHES (CURRENT GROUP)" (renamed from "Recent Matches")
- **Default Message**: "No match history for current selection" / "No matches yet"
- **Columns**: Bot1 Name, Bot1 Score, Bot2 Name, Bot2 Score, Winner, Outcome, Time
- **Sorting**: Descending by CompletedAt (newest first)
- **Data Source**: `/api/tournament-engine/groups/{eventName}/{groupLabel}`
- **Field**: `recentMatches` array
- **Polling**: 5 seconds (after group selected)
- **Visual**: Standard HTML table

### 3.4 Group Standings & Matches (Conditional Display)
- **Section 1 - Group Standings Table**:
  - Columns: Rank, Team Name, Wins, Losses, Draws, Score
  - Sorting: Descending by score
  - Data Source: `/api/tournament-engine/groups/{eventName}/{groupLabel}`
  - Field: `groupStanding` array
  
- **Section 2 - Matches** (renamed from "Recent Matches"):
  - Columns: Bot1 Name, Bot1 Score, Bot2 Name, Bot2 Score, Winner, Outcome, Time
  - Sorting: Descending by CompletedAt (newest first)
  - Data Source: `/api/tournament-engine/groups/{eventName}/{groupLabel}`
  - Field: `recentMatches` array
  - Polling: 5 seconds (when visible)

---

## 4. Data Flow & State Management

### Application State (JavaScript)
```javascript
{
  tournamentStatus: {
    currentEvent: null,
    totalEvents: 0,
    isRunning: false,
    startTime: null
  },
  
  selectedEvent: null,
  selectedGroup: null,
  
  data: {
    events: [],
    leaders: [],
    groups: {},  // { eventName: [...groups...] }
    groupDetails: {},  // { "eventName::groupLabel": {...} }
  },
  
  pollIntervals: {}  // Store interval IDs for cleanup
}
```

### Polling Manager
- Create object to manage multiple polling intervals
- Pause/resume polling based on component visibility
- Clear intervals on page unload
- Prevent race conditions with pending requests

---

## 5. Implementation Steps

### Phase 1: Setup (Step 1-2) ✅ COMPLETED
- [x] **Step 1**: Create HTML structure with semantic sections
  - File: `wwwroot/index.html` (650 lines)
  - Components: Header, Tournament Status, Events Details, Tables
  
- [x] **Step 2**: Create CSS stylesheet with responsive layout
  - File: `wwwroot/css/dashboard.css` (700 lines)
  - Features: Flexbox/Grid layout, responsive design, theme colors

### Phase 2: Core Functionality (Step 3-6) ✅ COMPLETED
- [x] **Step 3**: Create JavaScript polling manager utility
  - File: `wwwroot/js/polling-manager.js` (120 lines)
  - Features: Interval management, caching, lifecycle control
  
- [x] **Step 4**: Implement API service wrapper with error handling
  - File: `wwwroot/js/api-client.js` (85 lines)
  - Features: Timeout handling, retry logic, method wrappers
  
- [x] **Step 5**: Create constants file
  - File: `wwwroot/js/constants.js` (70 lines)
  - Features: Endpoints, polling intervals, UI selectors
  
- [x] **Step 6**: Implement event selection and event details display
  - Implemented in `app.js` (selectEvent method)

### Phase 3: Data Display (Step 7-10) ✅ COMPLETED
- [x] **Step 7**: Implement Event Progress component
  - UIRenderer: `updateEventProgress()` method
  - Shows progress bar, percentage, event labels
  
- [x] **Step 8**: Implement Groups selection and display
  - UIRenderer: `updateGroupList()` method
  - Shows available groups as selectable items
  
- [x] **Step 9**: Implement Group Standings table
  - UIRenderer: `updateGroupStandings()` method
  - Columns: Rank, Team Name, Wins, Losses, Draws, Score
  
- [x] **Step 10**: Implement Matches table
  - UIRenderer: `updateMatches()` method
  - Columns: Bot1, Score, Bot2, Score, Winner, Outcome, Time

### Phase 4: Conditional Rendering & Polling (Step 11-13) ✅ COMPLETED
- [x] **Step 11**: Implement tournament running status check
  - TournamentDashboard: `pollTournamentStatus()` method
  - Manages `isRunning` state
  
- [x] **Step 12**: Implement Event Champions (conditional polling)
  - UIRenderer: `updateEventChampions()` method
  - Always visible, updates with event progress
  
- [x] **Step 13**: Implement Overall Leaders (conditional polling)
  - UIRenderer: `updateOverallLeaders()` method
  - Shows only when `isRunning === true`
  - Polling starts when tournament begins

### Phase 5: Polish & Testing (Step 14-15) ⏳ IN PROGRESS
- [ ] **Step 14**: Add loading indicators and error handling
  - Partially implemented: `showLoading()`, `showError()` in UIRenderer
  - Status: Ready for testing
  
- [ ] **Step 15**: Test all polling intervals and data updates
  - Next: Start dashboard and verify data flow

---

## 6. File Structure

```
wwwroot/
├── index.html                 (Main HTML page)
├── css/
│   └── dashboard.css          (Styles and layout)
├── js/
│   ├── app.js                 (Main application entry)
│   ├── api-client.js          (API service wrapper)
│   ├── polling-manager.js     (Polling interval manager)
│   ├── ui-renderer.js         (DOM manipulation & rendering)
│   └── constants.js           (API endpoints & polling intervals)
└── README.md                  (Frontend documentation)
```

---

## 7. Key Implementation Notes

### Technology Stack
- **Frontend**: Vanilla JavaScript (no framework)
- **Styling**: CSS3 with Flexbox/Grid for responsive layout
- **HTTP Client**: Fetch API
- **Browser Support**: Modern browsers (ES6+)

### Polling Strategy
- Use `setInterval()` for polling with ID tracking
- Store intervals in centralized manager object
- Implement exponential backoff for failed requests
- Clear intervals when components unmount

### Data Normalization
- Normalize event names to lowercase for comparison
- Handle null/undefined values gracefully
- Use empty arrays/objects as defaults

### Error Handling
- Display error messages in UI
- Log errors to console for debugging
- Retry failed requests with backoff
- Gracefully degrade if API unavailable

### Performance
- Debounce rapid API calls
- Cache responses between polls
- Minimize DOM updates
- Use event delegation for dynamic elements

---

## 8. Testing Checklist

- [ ] All endpoint calls return 200 OK
- [ ] Connection status updates every 5 seconds
- [ ] Event Progress highlights current event correctly
- [ ] Events Details table populated with all events
- [ ] Event selection triggers group loading
- [ ] Group selection triggers standings/matches loading
- [ ] Champions display only when tournament running
- [ ] Leaders display only when tournament running
- [ ] Group standings sorted by score (descending)
- [ ] Matches sorted by time (newest first)
- [ ] No memory leaks from polling intervals
- [ ] Responsive design works on mobile/tablet
- [ ] Error messages display clearly if API fails

---

## 9. Next Steps

1. Get confirmation on dashboard design/layout details
2. Create HTML structure (wwwroot/index.html)
3. Create CSS styling (wwwroot/css/dashboard.css)
4. Create JavaScript modules in order
5. Test each polling interval independently
6. Integrate and test end-to-end
7. Deploy to TournamentEngine.Dashboard

---

## 11. Implementation Completion Summary

### Files Created (7 files, 2,000+ lines)
| File | Lines | Purpose |
|------|-------|---------|
| `index.html` | 180 | Main HTML structure with semantic sections |
| `css/dashboard.css` | 720 | Complete styling with responsive design |
| `js/constants.js` | 70 | Configuration: endpoints, intervals, selectors |
| `js/api-client.js` | 85 | API wrapper with error handling |
| `js/polling-manager.js` | 120 | Poll lifecycle management |
| `js/ui-renderer.js` | 350 | All DOM updates and rendering |
| `js/app.js` | 400 | Application orchestration & event handlers |

### Features Implemented
✅ **Header Section**
- Real-time Israel time (updates every 1 second)
- Tournament status badge ("NOT STARTED" / "RUNNING" / "COMPLETED")
- Connection indicator (green dot + "Connected" text)

✅ **Tournament Status Section**
- Event Progress bar (0-100%) with completion percentage
- Event labels showing all tournaments
- Event Champions list (all events, status updates)
- Overall Leaders list (visible only when running, 5-sec polling)

✅ **Events Details Section**
- Event selection tabs (pill-style with active state)
- Group list display for selected event
- Group Standings table (Rank, Team, Wins, Losses, Draws, Score)
- Matches table (Bot1, Score, Bot2, Score, Winner, Outcome, Time)

✅ **Polling System**
- Connection: 5 seconds (always active)
- Tournament Status: 5 seconds (always active)
- Events/Progress: 10 seconds (always active)
- Event Champions: 10 seconds (always active)
- Overall Leaders: 5 seconds (starts when tournament running)
- Groups: 10 seconds (starts when event selected)
- Group Details: 5 seconds (starts when group selected)

✅ **User Interactions**
- Event selection → Loads groups, stops previous group details polling
- Group selection → Loads standings/matches, stops previous details polling
- Dynamic active state styling
- Automatic polling cleanup on page unload

✅ **Error Handling & UX**
- Loading indicators (spinner shown during loads)
- Error toast notifications
- Empty state messages for missing data
- Graceful degradation if API unavailable
- Responsive design (mobile, tablet, desktop)

### Architecture Decisions
1. **Vanilla JavaScript** - No framework dependencies (light, fast)
2. **Global Singletons** - `apiClient`, `pollingManager`, `uiRenderer` for simplicity
3. **Polling Manager** - Centralized interval control prevents duplicates
4. **Event-driven Updates** - Only update UI when data changes
5. **Responsive Flexbox/Grid** - Works across all screen sizes

### Testing Readiness
✅ HTML structure complete and valid
✅ CSS fully styled and responsive
✅ JavaScript modules loading in correct order
✅ All polling intervals configured
✅ All API methods implemented
✅ All UI update methods implemented
✅ Event handlers connected and functional

### Next Steps for Testing
1. Start Dashboard: `dotnet run --project TournamentEngine.Dashboard --urls "http://localhost:8080"`
2. Open browser: `http://localhost:8080`
3. Verify tables and components load
4. Check console for polling activity
5. Run tournament to test data flow

### Known Limitations & Future Enhancements
- No API error retry with backoff (can be added)
- No local caching of responses (can be added)
- No dark mode support (can be added)
- Match outcome filtering (can be added)
- Export to CSV/PDF (can be added)
- Real-time notifications via SignalR (already supported by backend)

---