# Event Tabs Implementation Plan

## Overview
Add multi-tab event selection to the Events Details section in the main tournament dashboard, allowing users to select specific events (games) and view event-specific data for Recent Matches and Group Standings.

## Current State
- Events Details section shows:
  - Recent Matches (all events combined)
  - Now Running Event (separate panel)
  - Group Standings (cycling through all groups)
- No event-specific filtering
- No visual indication of current running event in the events list

## Goals
1. Add tabbed interface for event selection
2. Filter Recent Matches by selected event
3. Filter Group Standings by selected event
4. Mark currently running event tab with visual indicator
5. Remove "Now Running Event" panel (redundant with tab marking)

## Implementation Steps

### Step 1: TDD - UI Structure Tests
**File**: `TournamentEngine.Tests/TournamentDashboardUITests.cs` (new)
- Test: Event tabs container exists
- Test: Event tabs are generated from tournament steps
- Test: Selected event tab has active styling
- Test: Running event tab has running indicator
- Test: Recent Matches section exists
- Test: Group Standings section exists
- Test: Now Running Event panel does NOT exist

### Step 2: Implement Event Tabs HTML Structure
**File**: `TournamentEngine.Dashboard/wwwroot/index.html`
- Add `<div id="eventTabs">` container before details-grid
- Generate tab buttons from tournament steps
- Add click handlers for tab selection
- Add CSS classes for active and running states

### Step 3: TDD - Data Filtering Tests
**File**: `TournamentEngine.Tests/TournamentDashboardUITests.cs`
- Test: Recent matches filtered by selected event/gameType
- Test: Group standings filtered by selected event
- Test: Switching tabs updates displayed data
- Test: Default tab shows first event or current running event

### Step 4: Implement Event-Specific Data Filtering
**File**: `TournamentEngine.Dashboard/wwwroot/index.html`
- Add `selectedEventIndex` state variable
- Add `currentEventId` tracking
- Filter `recentMatches` by `GameType` matching selected event
- Filter `GroupStandings` by event ID or game type
- Update render functions to use filtered data

### Step 5: TDD - Active Event Marking Tests
**File**: `TournamentEngine.Tests/TournamentDashboardUITests.cs`
- Test: Running event tab has "running" CSS class
- Test: Running event tab shows visual indicator (⧖ or similar)
- Test: Clicking running event tab still works
- Test: Multiple running states handled correctly

### Step 6: Implement Active Event Visual Marking
**File**: `TournamentEngine.Dashboard/wwwroot/index.html`
- Add CSS class `tab-running` for active events
- Add icon/indicator in tab label for running events
- Ensure running event is visually distinct

### Step 7: Update CSS Styling
**File**: `TournamentEngine.Dashboard/wwwroot/index.html` (style section)
- Add `.event-tabs` container styles
- Add `.event-tab` button styles
- Add `.event-tab.active` styles
- Add `.event-tab.running` styles with accent color/animation
- Ensure responsive layout

### Step 8: Remove Now Running Event Panel
**File**: `TournamentEngine.Dashboard/wwwroot/index.html`
- Remove `<div id="nowRunningEvent">` panel
- Remove related CSS for 3-column grid
- Update `details-grid` to 2-column layout
- Remove `updateNowRunning()` function calls

### Step 9: Data Structure Enhancements (if needed)
**Files**: 
- `TournamentEngine.Core/Common/Dashboard/TournamentStateDtos.cs`
- Add `EventId` to `RecentMatchDto` if missing
- Add `EventId` to `GroupDto` if missing
- Ensure proper mapping in backend services

### Step 10: Integration Testing
- Run full test suite
- Visual verification in browser
- Test event switching
- Test with running tournament
- Test with completed tournament

## Key Design Decisions

1. **Tab Selection Default**: Show currently running event by default, or first event if none running
2. **Tab Persistence**: Don't persist tab selection across refreshes (always default to current/first)
3. **Visual Indicator**: Use ⧖ symbol and accent color for running event
4. **Data Filtering**: Filter by `GameType` from event steps, matching against `RecentMatchDto.GameType`
5. **Group Standings**: Show only groups for selected event (filter by GameType or EventId)

## Testing Strategy

### Unit Tests
- UI element presence tests
- Data filtering logic tests
- Event selection state tests

### Integration Tests
- Visual verification with live data
- Event switching functionality
- Running event indicator updates
- Responsive layout

## Success Criteria
- [ ] Event tabs render correctly
- [ ] Clicking tab changes selected event
- [ ] Recent Matches filtered by selected event
- [ ] Group Standings filtered by selected event
- [ ] Running event visually marked
- [ ] Now Running Event panel removed
- [ ] All existing tests pass
- [ ] New TDD tests pass
- [ ] Visual layout looks good

## Rollback Plan
If issues arise:
1. Revert HTML changes
2. Restore Now Running Event panel
3. Revert CSS changes
4. Keep new tests for future implementation
