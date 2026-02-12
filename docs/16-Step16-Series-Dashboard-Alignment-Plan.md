# Step 16 - Series Dashboard Alignment Plan

## Overview
Align the Dashboard to present a tournament series as a structured sequence with a single-screen layout. The UI must show the series step, completed winners, and upcoming steps at a glance. Less important data moves into a compact, collapsible details drawer.

## Goals
- Make series progress obvious at a glance.
- Show the current step index and game type.
- Show winners for completed steps in the series.
- Show upcoming steps in a compact, readable format.
- Keep the main view to one screen (no scrolling) on typical 1080p.
- Preserve existing tournament badges and match feed behavior.

## Non-Goals
- Full analytics dashboards or historical drill-down across many series.
- Replacing the existing match feed or standings logic.
- Multi-screen navigation or new routing.

## UX Layout (One-Screen Default)
### Top Row (Series Control Bar)
- **Series Title + Status**: "Series: Local Tournament" + Running/Completed badge.
- **Step Readout**: "Step 2 of 3 - Colonel Blotto".
- **Step Track**: 3-8 dots with current step highlighted.

### Middle Row (Three Compact Panels)
- **Now Running**: Current game type, tournament name, start time.
- **Winners Row**: Compact list of completed steps (Step, Game, Winner).
- **Up Next**: Two-line list or chips for upcoming steps.

### Lower Row (Details Drawer, Collapsed)
- **Details Drawer (collapsed by default)**
  - Recent Matches (existing)
  - Full Standings (existing)
  - Event Log (existing)

## UI Behavior
- Details drawer is collapsed by default; user can expand via a small "Details" toggle.
- Auto-collapse details when a new series starts.
- Keep tournament badges in match feed and recent matches.

## Suggested Additions (Beyond User Request)
- **Series Timeline Tooltips**: Step name + winner on hover.
- **"Next Up" Chip**: Highlight the immediate next step.
- **Compact Winner Icon**: Small trophy next to each winner.
- **Soft Highlight**: Brief highlight of the Winners Row on step completion.

## Data Model Changes
### New DTOs
- `SeriesStateDto`
  - `SeriesId` (string)
  - `SeriesName` (string)
  - `TotalSteps` (int)
  - `CurrentStepIndex` (int)
  - `Steps` (List<SeriesStepDto>)
  - `Status` (enum: NotStarted, InProgress, Completed)

- `SeriesStepDto`
  - `StepIndex` (int)
  - `GameType` (string)
  - `Status` (enum: Pending, Running, Completed)
  - `WinnerName` (string, optional)
  - `TournamentId` (string, optional)
  - `TournamentName` (string, optional)

### New Events
- `SeriesStartedDto`
- `SeriesProgressUpdatedDto`
- `SeriesStepCompletedDto`
- `SeriesCompletedDto`

## Backend Changes
- **TournamentSeriesManager**: Emit series-level events on start, step change, and completion.
- **ConsoleEventPublisher**: Forward series events to Dashboard Hub.
- **TournamentHub**: Add event handlers and methods for series state.
- **StateManagerService**: Store `SeriesStateDto` and update it on events.

## Frontend Changes (Dashboard)
- Add the **Series Control Bar** above current status.
- Add **Now Running**, **Winners Row**, and **Up Next** panels.
- Add a **Details Drawer** section to hide less important data by default.
- Update SignalR handlers to process series events and update the UI.

## Implementation Steps
1. ✅ **DTOs + Events**: Add `SeriesStateDto`, `SeriesStepDto`, and event DTOs.
2. ✅ **Series Manager Publishing**: Emit series events with step index and winner.
3. ✅ **Dashboard Hub + State Manager**: Store and serve series state.
4. ✅ **UI Layout**: Implement Series Control Bar + three panels.
5. ✅ **Details Drawer**: Add toggle and collapsed layout + persistent state + auto-collapse cue.
6. ✅ **Styling**: Compact panels, chips, progress dots, winner badges, highlights.
7. ✅ **Testing**: Unit tests for services + API tests + Playwright UI tests.

## Testing
- Unit tests for series DTO updates in `StateManagerService`.
- Dashboard integration tests for series endpoints and events.
- Manual smoke test with simulator to verify compact layout.

## Acceptance Criteria
✅ Single-screen view at load (no scrolling required).
✅ Current step, completed winners, and upcoming steps visible.
✅ Details drawer collapses/expands without breaking layout.
✅ Existing tournament badges and match feed remain intact.
✅ Details drawer persists open/closed state across page reloads.
✅ Auto-collapse on series start with visual cue (pulse animation).
✅ Winners Row highlights briefly on step completion.
✅ Winner items show compact "W" badge.
✅ Step track dots show running/completed/pending status.
✅ Comprehensive test coverage (unit + UI).

## Open Questions
- How many steps should be supported before compressing into a dropdown?
- Should series name be user-configurable or inferred from game types?
