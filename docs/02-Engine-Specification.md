# Tournament Engine Specification

## Overview

The Tournament Engine is the core system that manages the AI Productivity Workshop game tournament. It handles bot loading, match execution, bracket management, scoring, and result visualization.

### Key Requirements

- Support for 4 different games (RPSLS, Colonel Blotto, Penalty Kicks, Security Game)
- Group-stage tournament format (two phases: initial groups → final group → tiebreaker)
- Handle 30-120 bots (teams)
- Real-time execution during 2-hour workshop
- Basic match visualization/logging
- Automatic disqualification for invalid bots

---

## Architecture

The engine consists of 5 main components:

1. **Bot Loader** - Loads and validates bot code
2. **Game Runner** - Executes individual matches
3. **Tournament Manager** - Manages bracket and advancement
4. **Scoring System** - Calculates results and rankings
5. **Output/Display** - Shows results to participants

---

## Component 1: Bot Loader

### Purpose

Load bot code safely and validate it meets requirements

### Input

Bot submission directory structure:

```
team_name/
  ├── game1_rpsls_bot.py
  ├── game2_blotto_bot.py
  ├── game3_penalty_bot.py
  ├── game4_security_bot.py (optional)
  ├── strategy.md
  └── copilot_review.txt
```

### Functionality

- Load Python bot file dynamically
- Verify required function exists (make_move, allocate_troops, etc.)
- Validate function signature matches game spec
- Check for prohibited imports (network, file system)
- Sandbox execution (timeout, memory limits)
- Return loaded bot object or error

### Validation Checks

- File exists and is valid Python
- Required function is defined
- **No imports:** requests, socket, os, sys, subprocess
- **Allowed imports:** random, math, collections, itertools, statistics

### Output

- **Success:** Bot object ready for tournament
- **Failure:** Error message + disqualification

### Pseudocode

```python
def load_bot(bot_file_path, game_type):
    try:
        module = import_module(bot_file_path)
        function_name = GAME_FUNCTIONS[game_type]  # e.g., "make_move"
        if not hasattr(module, function_name):
            raise ValidationError(f"Missing {function_name} function")
        bot_function = getattr(module, function_name)
        validate_imports(module)
        return Bot(name=team_name, function=bot_function, game=game_type)
    except Exception as e:
        log_error(team_name, e)
        return None
```

---

## Component 2: Game Runner

### Purpose

Execute a single match between two bots for a specific game

### Input

- Bot A object
- Bot B object
- Game type (rpsls, blotto, penalty, security)
- Match configuration (rounds, timeouts)

### Functionality

- Call bot functions with appropriate game state
- Enforce time limits (50ms for RPSLS, 100ms for Blotto)
- Validate bot output
- Track match history
- Calculate winner
- Handle errors/timeouts gracefully

### Game-Specific Logic

#### Game 1 - RPSLS

- Run 50 rounds
- Each round: call both bots' make_move()
- Compare moves, award points
- Return winner (higher score)

#### Game 2 - Colonel Blotto

- Call allocate_troops() once per bot
- Validate allocation (5 ints, sum=100)
- Compare allocations per battlefield
- Count battlefield wins
- Return winner (more battlefields won)

### Output

**MatchResult object:**

- `winner`: Bot object
- `loser`: Bot object
- `score`: (winner_score, loser_score)
- `match_log`: detailed play-by-play
- `errors`: any bot errors encountered

---

## Component 3: Tournament Manager

### Purpose

Manage group-stage tournament with two phases and tiebreakers

### Input

- List of all valid bots
- Game type

### Functionality

- Divide bots into initial groups (~1/10 of total participants each)
- Run round-robin matches within each group (all vs all)
- Track group standings (wins, losses, points)
- Advance group winners to final group stage
- Run round-robin in final group
- Handle tie-breakers if needed after group stages

### Tournament Structure

**Phase 1: Initial Groups**
- Divide N bots into ~N/10 groups (e.g., 60 bots → 6 groups of 10)
- Each bot plays every other bot in their group (round-robin)
- Winner of each group advances

**Phase 2: Final Group**
- All group winners form a single group
- Round-robin play (all vs all)
- Determine final standings by points/wins

**Phase 3: Tiebreaker (if needed)**
- If multiple bots tied at top of final group:
  - Run single-elimination or sudden-death matches
  - Continue until single winner determined

### Tie-Breaker Logic

If final group ends in tie:

- Run decisive match between tied bots
- Single-elimination format for multiple tied bots
- Sudden death for head-to-head ties
- Max 10 sudden death attempts, then random winner

### Output

- Group assignments and standings
- List of all match results
- Final champion

---

## Component 4: Scoring System

### Purpose

Track and calculate tournament results

### Data Tracked

- **Per bot:** wins, losses, total points scored
- **Per match:** detailed results
- **Tournament progress**

### Functionality

- Record match outcomes
- Update bracket
- Calculate final rankings (1st, 2nd, 3rd, etc.)
- Generate statistics

### Rankings

- **1st place:** Tournament winner
- **2nd place:** Finals loser
- **3rd place:** Semifinals losers (tie)
- Continue for other rounds

---

## Component 5: Output/Display

### Purpose

Show tournament progress and results to participants

### Minimum Viable

Console output showing:

- "Round 1: Bot_A vs Bot_B → Bot_A wins 27-23"
- Bracket progression
- Final champion announcement

### Enhanced (Optional)

- Simple web dashboard
- Real-time bracket visualization
- Match replay viewer
- Statistics dashboard

---

## Technical Recommendations

### Language

**Python 3.9+**

- Easy for participants
- Good sandboxing libraries
- Fast enough for workshop needs

### Key Libraries

- `importlib` - Dynamic module loading
- `multiprocessing` - Timeout enforcement
- `json` - Configuration and results
- `logging` - Error tracking

### Project Structure

```
tournament_engine/
  ├── bot_loader.py       # Component 1
  ├── game_runner.py      # Component 2
  ├── tournament_manager.py  # Component 3
  ├── scoring.py          # Component 4
  ├── display.py          # Component 5
  ├── games/
  │   ├── rpsls.py
  │   ├── blotto.py
  │   ├── penalty.py
  │   └── security.py
  ├── bots/               # Participant submissions
  │   ├── team1/
  │   ├── team2/
  │   └── ...
  ├── tests/
  │   ├── dummy_bots/     # Sample bots for testing
  │   └── test_engine.py
  ├── main.py             # Entry point
  └── config.json         # Tournament configuration
```

---

## Security & Sandboxing

**CRITICAL:** Bot code is UNTRUSTED - must be sandboxed

### Safety Measures

- Timeout enforcement (use multiprocessing with timeout)
- Import restrictions (no os, sys, subprocess, network)
- Memory limits (if possible)
- No file system access
- Run bots in separate processes

### Implementation

- Use `multiprocessing.Process` with timeout
- Catch all exceptions from bot code
- Log errors but don't crash engine
- Auto-disqualify bots that violate rules

---

## Error Handling

### Bot Errors to Handle

- Timeout (>50ms or >100ms)
- Invalid output (wrong type, format)
- Runtime exception in bot code
- Missing function
- Import violations

### Engine Response

- Log error with team name and details
- Award loss to bot with error
- If both bots error: random winner
- Continue tournament despite errors

---

## Performance Considerations

### Expected Load

- 60 bots average
- Game 1 (RPSLS): 50 rounds × 60 bots = ~30 matches × 50 rounds = 1,500 bot calls
- Estimated time: 5-10 minutes per game
- Total tournament: 20-40 minutes for all 4 games

### Optimizations

- Run matches in parallel (if needed)
- Cache bot loading
- Minimize logging during matches
- Pre-validate all bots before tournament starts

---

## Detailed Implementation Specifications

### Data Structures

Complete class definitions with type hints:

```python
class GameState:
    """Immutable state representation"""
    board: Dict[str, Any]
    valid_moves: List[str]
    current_player: int

class BotInterface:
    """Required bot implementation"""
    def get_move(self, game_state: GameState) -> str:
        """Return valid move string"""
        pass
    
    def initialize(self) -> None:
        """Setup called once before tournament"""
        pass
```

### Error Handling

#### Validation Process

**Bot Loading:**

- Import timeout: 5 seconds
- Catch ImportError, SyntaxError, etc.
- Log error with team name

**Move Validation:**

- Timeout per move: 1 second
- Check move format matches game rules
- Verify move is in valid_moves list

**Error Types:**

- Timeout: Bot exceeds time limit
- Invalid Move: Move not in valid list
- Exception: Bot crashes during execution
- Import Error: Bot file cannot be loaded
- Missing Function: Required method not implemented

**Engine Response:**

- Log error with team name and details
- Award loss to bot with error
- If both bots error: random winner
- Continue tournament despite errors

---

## Bot Isolation

### Sandboxing Requirements

**Process Isolation:**

- Run each bot in separate subprocess
- Kill process after match completion
- Prevent access to other bots' code

**Resource Limits:**

- CPU time per move: 1 second
- Memory limit: 512 MB per bot
- No file system write access
- No network access

**Import Restrictions:**

- **Allow:** random, math, copy, typing
- **Block:** os, subprocess, sys, socket
- **Block:** file operations (open, read, write)

---

## Logging System

### Log Levels

**Match Events:**

- Match start/end with team names
- Each move with player and action
- Match outcome with score

**Error Events:**

- Bot errors with full traceback
- Validation failures
- Timeout incidents

**Tournament Events:**

- Round start/end
- Bracket progression
- Final standings

### Output Format

```
[TIMESTAMP] [LEVEL] [TEAM_NAME] Message
```

### Storage

- `tournament_log.txt` in root directory
- Separate log per game type

---

## Scoring System

### Match Outcomes

- **Win:** 3 points (or 1 point, configurable)
- **Draw:** 1 point each (if game allows draws)
- **Loss:** 0 points
- **Both Error:** 0 points each
- **One Error:** Other bot gets win points

### Tournament Scoring

- Group stage: Points accumulated per match
- Group winners: Bot(s) with highest points in each group
- Tiebreakers within groups: Head-to-head record, then goal differential, then sudden-death
- Track total games won, points scored, and win percentage for statistics
- Record match history for review

---

## Game Interface

### Standard Game API

Each game implements:

```python
class Game:
    def __init__(self):
        """Initialize game state"""
    
    def get_initial_state(self) -> GameState:
        """Return starting state"""
    
    def apply_move(self, state: GameState, move: str) -> GameState:
        """Return new state after move"""
    
    def get_valid_moves(self, state: GameState) -> List[str]:
        """Return list of legal moves"""
    
    def is_terminal(self, state: GameState) -> bool:
        """Check if game is over"""
    
    def get_winner(self, state: GameState) -> Optional[int]:
        """Return winner (0, 1) or None"""
```

---

## Tournament Manager Core Responsibilities

- Load all bots from bots/ directory
- Create group assignments for initial stage
- Run round-robin matches within groups
- Track group standings and determine winners
- Handle errors and disqualifications
- Advance group winners to final group
- Run final group round-robin
- Execute tiebreakers if needed
- Determine tournament champion

### Group Structure

**Example: 60 bots**
- Phase 1: 6 groups of 10 bots each
- Each group: 45 matches (10 bots round-robin)
- 6 group winners advance
- Phase 2: 1 group of 6 bots
- Final group: 15 matches (6 bots round-robin)
- Tiebreaker: As needed based on final standings

**Group Assignment:** Random distribution into groups

---

## Configuration

### config.json structure

```json
{
  "tournament": {
    "format": "single_elimination",
    "rounds_per_match": 50,
    "games": ["rpsls", "blotto", "penalty", "security"]
  },
  "timeouts": {
    "import": 5,
    "move": 1
  },
  "resources": {
    "memory_limit_mb": 512
  },
  "logging": {
    "level": "INFO",
    "file": "tournament_log.txt"
  }
}
```

---

## Implementation Checklist

### Core Engine Components

- ☐ bot_loader.py - Load and validate bot submissions
- ☐ game_runner.py - Execute single match between two bots
- ☐ tournament_manager.py - Manage bracket and progression
- ☐ scoring.py - Track results and determine winners
- ☐ display.py - Output results to console/file

### Game Implementations

- ☐ games/rpsls.py - Rock Paper Scissors Lizard Spock
- ☐ games/blotto.py - Colonel Blotto
- ☐ games/penalty.py - Penalty Kicks
- ☐ games/security.py - Security vs Hacker

### Testing

- ☐ tests/test_engine.py - Engine validation
- ☐ tests/dummy_bots/ - Sample bots for testing

### Entry Point

- ☐ main.py - CLI to start tournament

### Configuration

- ☐ config.json - Tournament settings

---

## Usage Instructions

### Running Tournament

```bash
python main.py --game rpsls --rounds 50
```

### Expected Output

- **Console:** Match progress and results
- **File:** tournament_log.txt with details
- **File:** results.json with final standings

### Bot Requirements for Participants

- Implement BotInterface class
- Place in bots/team_name/ directory
- Include __init__.py
- No external dependencies beyond allowed list
