# Step 17 - Tournament Management Controls Plan

## Goal
Add a management screen that lets operators start, pause, resume, stop, clear submissions, and rerun the last tournament after bots are submitted.

## Placement
- **New management screen** at `/manage` (separate from `/` and `/Bots`).
- Keep operational controls isolated from bot intake and live tournament view.

## Scope
### Controls
- **Start Tournament** (enabled only when bots are ready)
- **Pause Tournament**
- **Resume Tournament**
- **Stop Tournament**
- **Clear Submissions**
- **Re-run Last Tournament**

### Readiness Checks
- Minimum bot count (>= 2 valid bots)
- No validation in progress
- Latest submission status loaded

## Architecture
### Backend
- New `TournamentManagementService` to orchestrate run/pause/resume/stop
- New `TournamentManagementEndpoints` for control actions
- State tracking: `ManagementStateDto` with status and timestamps
- Hook into existing `TournamentSeriesManager` and `StateManagerService`

### Frontend
- Razor page `/Manage` with status banner and action buttons
- Button states reflect management state (disabled when invalid)
- Confirmation prompts for destructive actions (clear, stop)
- Live status updates via SignalR

## API Endpoints (proposed)
- `POST /api/manage/start`
- `POST /api/manage/pause`
- `POST /api/manage/resume`
- `POST /api/manage/stop`
- `POST /api/manage/clear`
- `POST /api/manage/rerun`
- `GET /api/manage/status`

## TDD Plan (per sub-step)
Each sub-step uses Red/Green/Refactor. After **every** sub-step:
1) Run all tests in the solution.
2) Fix any failures immediately.

### Sub-steps
1. **Contracts & DTOs**
   - Add `ManagementStateDto`, enums for run state.
   - Tests: DTO serialization + default values.

2. **Service Layer**
   - Implement `TournamentManagementService` with state machine.
   - Tests: start/pause/resume/stop, invalid transitions, rerun.

3. **Endpoints**
   - Add minimal API endpoints under `/api/manage`.
   - Tests: HTTP status codes, payload validation, state updates.

4. **SignalR Integration**
   - Broadcast management state changes.
   - Tests: hub events and payloads.

5. **UI Page**
   - New `/Manage` Razor page with controls and status panel.
   - Tests: UI structure, control presence, button states.

6. **Integration Wiring**
   - Connect to bot readiness checks and tournament engine.
   - Tests: end-to-end management flow with mock bots.

## Testing Requirements
- **Before and after each sub-step:** run all tests in the solution.
- **After completing all sub-steps:** run full simulator that combines:
  - Bot submission (Step 13)
  - Tournament engine execution (Steps 5-11)
  - Management controls (Step 17)

## Simulator Validation
Create or extend a simulator to:
1. Submit bots.
2. Validate bots.
3. Start tournament from management screen.
4. Pause and resume mid-run.
5. Stop tournament and rerun.
6. Confirm UI updates and final results.

## Success Criteria
- All controls function end-to-end with correct state transitions.
- Buttons enable/disable correctly based on readiness.
- No regressions in existing dashboards or API.
- Full integration simulator passes end-to-end.
- All tests pass after every sub-step.
