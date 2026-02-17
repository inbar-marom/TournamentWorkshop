# Event Tabs - Visual Verification Guide

## How to Test the New Event Tabs Feature

### Starting the Dashboard

```powershell
# Start the dashboard
dotnet run --project TournamentEngine.Dashboard --urls "http://localhost:5000"
```

Then open your browser to: http://localhost:5000

### What You Should See

#### Before Tournament Starts
- **Event Tabs Section**: Empty or showing "No events configured"
- **Recent Matches**: "No matches yet"
- **Group Standings**: "No group data yet"

#### After Tournament Starts

1. **Event Tabs Row**
   - Tabs appear below "Events Details" heading
   - Each event shows as a button (e.g., "RPSLS", "Colonel Blotto", "Penalty Kicks")
   - One tab is highlighted (active) with accent color background
   - Currently running event has:
     - ⧖ symbol before the name
     - Dashed border (if not selected)
     - Gradient glow effect (if selected)

2. **Recent Matches Panel**
   - Shows ONLY matches from the currently selected event
   - Game type in match badges matches selected event
   - If no matches for this event: "No matches yet for this event"

3. **Group Standings Panel**
   - Shows groups for the selected event
   - Auto-cycles through groups as before
   - If no groups for this event: "No group data yet for this event"

### Interactive Testing

#### Test 1: Click Different Event Tabs
1. Click on a different event tab
2. **Expected**: 
   - Tab becomes highlighted
   - Recent Matches updates to show only that event's matches
   - Group Standings updates for that event
   - Browser doesn't reload (instant update)

#### Test 2: Running Event Indicator
1. Note which event is currently running (has ⧖ symbol)
2. Click on that running event tab
3. **Expected**:
   - Tab shows gradient background with glow
   - ⧖ symbol is visible
   - Tab has "running" and "active" styling

#### Test 3: No Running Event Panel
1. Look for "Now Running Event" panel
2. **Expected**:
   - Panel does NOT exist anymore
   - Only 2 columns: "Recent Matches" and "Group Standings"

### Visual Styling Checks

#### Event Tab (Normal)
- Light gray background (#f7f7f4)
- Gray text
- 1px solid border
- Rounded corners

#### Event Tab (Hover)
- Soft green background (accent-soft)
- Teal text (accent-strong)
- Accent border

#### Event Tab (Active/Selected)
- Teal background (accent color)
- White text
- Bold font weight
- Darker teal border

#### Event Tab (Running, not selected)
- 2px dashed teal border
- ⧖ symbol prefix
- Normal background

#### Event Tab (Running + Active)
- Gradient teal background
- White text
- Glow/shadow effect
- ⧖ symbol prefix
- Bold text

### Common Issues & Solutions

#### Issue: Tabs don't appear
**Solution**: Ensure tournament has started and has configured events

#### Issue: Running indicator (⧖) not showing
**Solution**: Make sure an event is actually in "InProgress" status

#### Issue: Data not filtering
**Solution**: Check browser console for JavaScript errors

#### Issue: Layout broken on mobile
**Solution**: Tabs should wrap on smaller screens (flex-wrap)

### Browser Console Testing

Open browser DevTools (F12) and check:

```javascript
// Check state variables
console.log('Selected Event Index:', selectedEventIndex);
console.log('Tournament Steps:', tournamentSteps);
console.log('Recent Matches:', recentMatches);

// Manually select an event
selectEventTab(0); // Select first event
selectEventTab(1); // Select second event
```

### Screenshot Checklist

Take screenshots showing:
- [ ] Event tabs with multiple events
- [ ] Active event tab styling
- [ ] Running event indicator (⧖)
- [ ] Recent Matches filtered by event
- [ ] Group Standings for selected event
- [ ] Hover state on tab
- [ ] Mobile responsive layout

## Expected Behavior Summary

✅ Event tabs render dynamically from tournament configuration
✅ Clicking tabs switches the selected event
✅ Recent Matches filter by selected event's GameType
✅ Group Standings update for selected event
✅ Running event has visual indicator (⧖ + styling)
✅ No "Now Running Event" panel exists
✅ Layout is responsive
✅ State persists during tournament (doesn't reset on update)

## Rollback Instructions

If issues occur, revert these changes:

1. Restore `TournamentEngine.Dashboard/wwwroot/index.html` from git
2. Restart the dashboard

```powershell
git checkout HEAD -- TournamentEngine.Dashboard/wwwroot/index.html
```

Then restart the dashboard.
