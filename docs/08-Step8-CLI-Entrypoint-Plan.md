# Step 8: Implement CLI Entrypoint - Multi-Service Orchestration

## Overview

Transform the tournament system from isolated console apps into a fully orchestrated, multi-service architecture that coordinates:
1. **Dashboard Service** (ASP.NET Core web server with SignalR hub)
2. **Tournament Runner** (Main CLI application that executes tournaments)
3. **Event Streaming** (Real-time match updates to dashboard)
4. **Configuration Management** (Environment variables + appsettings.json)

This enables a complete tournament experience where users can:
- Configure tournaments via environment or config files
- Run tournaments while viewing real-time updates on a dashboard
- Stream results to connected clients
- Export results to JSON

---

## Architecture

### Service Components

```
┌─────────────────────────────────────────────────────────┐
│          Service Orchestrator (Runner Script/App)       │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │   Configuration Manager                          │  │
│  │   - Load appsettings.json                        │  │
│  │   - Read environment variables                   │  │
│  │   - Validate configuration                       │  │
│  └──┬───────────────────────────────────────────────┘  │
│     │                                                  │
│  ┌──▼──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │  Dashboard      │  │ Tournament   │  │  Bot      │ │
│  │  Service        │  │ Runner       │  │  Loader   │ │
│  │  :5000          │  │ (Console)    │  │           │ │
│  │  SignalR Hub    │  │ GameRunner   │  │  Roslyn   │ │
│  └────────┬────────┘  └─────┬────────┘  └───┬───────┘ │
│           │                 │                │         │
│           └─────────────────┼────────────────┘         │
│                   EventPublisher                       │
│                   (via HubConnection)                  │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
                    Web Browser (http://localhost:5000)
```

---

## Implementation Steps

### Phase 1: Configuration Infrastructure

#### 1.1 Create `appsettings.json`

**Location:** `TournamentEngine.Console/appsettings.json`

```json
{
  "TournamentEngine": {
    "BotsDirectory": "./bots",
    "ResultsDirectory": "./results",
    "DefaultGameTypes": ["RPSLS", "ColonelBlotto", "PenaltyKicks"],
    "BotLoadingTimeout": 30,
    "MoveTimeout": 5,
    "MemoryLimitMB": 512,
    "MaxParallelMatches": 5,
    "MaxRoundsRPSLS": 50,
    "LogLevel": "INFO",
    "EnableDashboard": true,
    "DashboardUrl": "http://localhost:5000/tournamentHub",
    "DashboardPort": 5000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Tournament": {
    "Name": "Tournament Series",
    "BotCount": 20,
    "SeedRandomly": true,
    "ExportResults": true
  }
}
```

#### 1.2 Create `appsettings.{Environment}.json` templates

- `appsettings.Development.json` - For local development
- `appsettings.Production.json` - For deployment

#### 1.3 Environment Variable Overrides

Support overriding via environment variables:
- `TOURNAMENT_BOTS_DIRECTORY`
- `TOURNAMENT_MOVE_TIMEOUT`
- `TOURNAMENT_MEMORY_LIMIT_MB`
- `DASHBOARD_URL`
- `DASHBOARD_PORT`
- `BOT_COUNT`
- `LOG_LEVEL`

---

### Phase 2: Configuration Manager Service

#### 2.1 Create `TournamentConfiguration` class

**Location:** `TournamentEngine.Console/Configuration/TournamentConfiguration.cs`

```csharp
public class TournamentConfiguration
{
    public TournamentEngineSettings TournamentEngine { get; set; }
    public TournamentSettings Tournament { get; set; }
    public LoggingSettings Logging { get; set; }
}

public class TournamentEngineSettings
{
    public string BotsDirectory { get; set; }
    public string ResultsDirectory { get; set; }
    public List<GameType> DefaultGameTypes { get; set; }
    public int BotLoadingTimeout { get; set; }
    public int MoveTimeout { get; set; }
    public int MemoryLimitMB { get; set; }
    public int MaxParallelMatches { get; set; }
    public int MaxRoundsRPSLS { get; set; }
    public string LogLevel { get; set; }
    public bool EnableDashboard { get; set; }
    public string DashboardUrl { get; set; }
    public int DashboardPort { get; set; }
}

public class TournamentSettings
{
    public string Name { get; set; }
    public int BotCount { get; set; }
    public bool SeedRandomly { get; set; }
    public bool ExportResults { get; set; }
}

public class LoggingSettings
{
    public Dictionary<string, string> LogLevel { get; set; }
}
```

#### 2.2 Create `ConfigurationManager`

**Location:** `TournamentEngine.Console/Configuration/ConfigurationManager.cs`

**Responsibilities:**
- Load `appsettings.json` using `IConfiguration`
- Override with environment variables
- Validate configuration (required fields, valid ranges)
- Create `TournamentConfig` for engine consumption
- Create `TournamentSeriesConfig` for series execution
- Provide logging configuration

---

### Phase 3: Service Orchestrator

#### 3.1 Create Multi-Service Runner

**Location:** `TournamentEngine.Console/Program.cs` (Enhanced)

**Flow:**
1. Load and validate configuration
2. Setup logging
3. Initialize bot loader
4. Start Dashboard service (background process)
5. Wait for Dashboard to be ready
6. Connect to Dashboard hub
7. Load bots from configured directory
8. Start tournament runner
9. Run tournament series (streaming to dashboard)
10. Save results to JSON
11. Display summary
12. Cleanup and shutdown

#### 3.2 Service Management

Create `ServiceManager` class:
- Start Dashboard process with stdio redirection
- Monitor Dashboard health (ping hub)
- Auto-reconnect on connection loss
- Graceful shutdown on error
- Log all service events

---

### Phase 4: Tournament Runner Integration

#### 4.1 Create `TournamentRunner`

**Location:** `TournamentEngine.Console/Tournament/TournamentRunner.cs`

**Responsibilities:**
- Execute tournament series from CLI
- Stream events to dashboard via `SignalRSimulatorEventPublisher`
- Handle cancellation (Ctrl+C)
- Save results after completion
- Display console output with progress

#### 4.2 Results Export

**Location:** `TournamentEngine.Console/Utilities/ResultsExporter.cs`

**Format:** JSON with structure:
```json
{
  "SeriesId": "uuid",
  "StartTime": "ISO8601",
  "EndTime": "ISO8601",
  "TotalMatches": 45,
  "Champion": "Team6",
  "Tournaments": [
    {
      "TournamentId": "uuid",
      "GameType": "RPSLS",
      "Champion": "Team5",
      "Matches": [...],
      "FinalStandings": [...]
    }
  ],
  "OverallStandings": [...]
}
```

---

### Phase 5: Optional Orchestration Scripts

#### 5.1 PowerShell Script (Windows)

**Location:** `./start-tournament.ps1`

```powershell
# Start Dashboard
$dashboardProcess = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList "run --project TournamentEngine.Dashboard" `
    -PassThru

# Wait for dashboard to start
Start-Sleep -Seconds 3

# Start Tournament Runner
dotnet run --project TournamentEngine.Console

# Cleanup
Stop-Process -Id $dashboardProcess.Id
```

#### 5.2 Bash Script (Linux/Mac)

**Location:** `./start-tournament.sh`

```bash
#!/bin/bash

# Start Dashboard in background
dotnet run --project TournamentEngine.Dashboard &
DASHBOARD_PID=$!

# Wait for dashboard
sleep 3

# Start Tournament Runner
dotnet run --project TournamentEngine.Console

# Cleanup
kill $DASHBOARD_PID 2>/dev/null
```

---

### Phase 6: Environment Variable Configuration

#### 6.1 .env File Support

**Location:** `.env` (in workspace root)

```env
TOURNAMENT_BOTS_DIRECTORY=./bots
TOURNAMENT_MOVE_TIMEOUT=5
TOURNAMENT_MEMORY_LIMIT_MB=512
DASHBOARD_URL=http://localhost:5000/tournamentHub
DASHBOARD_PORT=5000
BOT_COUNT=20
LOG_LEVEL=Information
```

#### 6.2 Configuration Priority

1. Environment Variables (highest priority)
2. `appsettings.{Environment}.json`
3. `appsettings.json` (default)

---

## Implementation Breakdown

### Task 1: Configuration Infrastructure ✅ COMPLETED
- [x] Create `appsettings.json`
- [x] Create `TournamentConfiguration` POCO classes
- [x] Create `ConfigurationManager` with validation
- [x] Add Microsoft.Extensions.Configuration NuGet

**Completed:** 2026-02-11
**Actual Time:** 1.5 hours
**Status:** All configuration infrastructure in place and tested

### Task 2: Dashboard Service Management ✅ COMPLETED
- [x] Create `ServiceManager` for process management
- [x] Implement dashboard health check
- [x] Add service startup/shutdown logic
- [x] Error handling and logging

**Completed:** 2026-02-11
**Actual Time:** 1 hour
**Status:** Full lifecycle management implemented with health monitoring

### Task 3: Results Export
- [ ] Create `ResultsExporter` with JSON serialization
- [ ] Add results directory creation
- [ ] Test export format
- [ ] Add timestamp/ID generation

**Depends on:** Task 1
**Complexity:** Low
**Estimated Time:** 1-2 hours

### Task 4: Enhanced Program.cs
- [ ] Integrate configuration manager
- [ ] Orchestrate service startup
- [ ] Implement tournament runner flow
- [ ] Add console UI for progress
- [ ] Handle graceful shutdown

**Depends on:** Tasks 1, 2, 3
**Complexity:** High
**Estimated Time:** 3-4 hours

### Task 5: Documentation & Scripts
- [ ] Create orchestration scripts (PowerShell/Bash)
- [ ] Document configuration options
- [ ] Add usage examples
- [ ] Create troubleshooting guide

**Depends on:** Task 4
**Complexity:** Low
**Estimated Time:** 1-2 hours

---

## Success Criteria

### Configuration Management ✅
- [ ] `appsettings.json` loaded correctly
- [ ] Environment variables override values
- [ ] Configuration validation catches missing/invalid values
- [ ] Different environments supported (Dev/Prod)

### Service Orchestration ✅
- [ ] Dashboard starts automatically
- [ ] Dashboard health check works
- [ ] Tournament runner connects to dashboard
- [ ] Services shutdown gracefully
- [ ] Error messages are clear and actionable

### Tournament Execution ✅
- [ ] Bots loaded from configured directory
- [ ] Tournament series runs with configured games
- [ ] Real-time events stream to dashboard
- [ ] Results exported to JSON
- [ ] Console shows progress

### User Experience ✅
- [ ] Single command to start everything: `dotnet run --project TournamentEngine.Console`
- [ ] Clear console output with progress
- [ ] Dashboard accessible at configured URL
- [ ] Results saved to configured directory
- [ ] Easy troubleshooting (logs are clear)

---

## Integration Points

### With Existing Components

1. **TournamentManager** - Use existing `RunTournamentAsync()` method
2. **TournamentSeriesManager** - Orchestrate multi-game series
3. **BotLoader** - Load bots from `BotsDirectory`
4. **ScoringSystem** - Generate rankings and statistics
5. **SignalRSimulatorEventPublisher** - Stream events to dashboard
6. **Dashboard Service** - Receive and display real-time updates

### With Future Components

1. **Step 7 (Output/Display)** - Console display for progress
2. **Step 13 (Bot API)** - Load bots from API instead of directory
3. **Step 15 (Dashboard)** - Integrate existing dashboard

---

## Error Handling

### Configuration Errors
- Missing required fields → Show helpful error with defaults
- Invalid values → Suggest valid ranges
- Inaccessible directories → Create them or show permission error

### Service Errors
- Dashboard failed to start → Offer to continue without dashboard
- Bot loading failed → Skip invalid bots, continue with valid ones
- Connection lost to dashboard → Auto-reconnect with exponential backoff

### Tournament Errors
- Bot timeout → LogError, record as loss
- Bot crash → Mark bot as invalid, continue
- Match error → Retry or skip with logging

---

## Configuration Examples

### Development Setup
```json
{
  "TournamentEngine": {
    "BotsDirectory": "./test_bots",
    "DefaultGameTypes": ["RPSLS"],
    "MaxParallelMatches": 2,
    "LogLevel": "DEBUG",
    "EnableDashboard": true
  }
}
```

### Production Setup
```json
{
  "TournamentEngine": {
    "BotsDirectory": "/var/tournament/bots",
    "ResultsDirectory": "/var/tournament/results",
    "MaxParallelMatches": 8,
    "LogLevel": "WARNING",
    "EnableDashboard": false
  }
}
```

---

## Next Steps After Completion

1. **Step 7:** Implement console display (formatted output during tournament)
2. **Step 13:** Add bot submission API for remote bot registration
3. **Step 15:** Integrate existing dashboard with this orchestrator
4. **Deployment:** Create Docker container for complete system

