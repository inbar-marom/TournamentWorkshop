# Tournament Bot Workshop Plan: "StrategicMind"

## Bot Name: StrategicMind

---

## Project Coding Standards

**CRITICAL RULES - Must be enforced in all code:**
- Every function must have Unit Tests
- Every statement must end with Two forward slashes(//)
- Only classes and functions may contain documentation
- No inline comments in the rest of the code

---

## 1. Tournament Overview

### Tournament Structure
- **4 Game Events**: RPSLS, Colonel Blotto, Penalty Kicks, Security Game
- **Competition Format**: 
  - Bots grouped into 10 groups
  - Round-robin within each group
  - Winners advance to final stage
- **Scoring**: Each win = 1 point (ties resolved by tiebreaker)
- **Performance Requirements**: 
  - Response time < 0.3 seconds per API call
  - Max file size: 50KB per file
  - Max total size: 500KB
  - Max memory: 450 MB

### Game Formats

#### RPSLS (Rock, Paper, Scissors, Lizard, Spock)
- **Rounds per opponent**: 10
- **Objective**: Win by choosing the superior move
- **Strategy Focus**: Pattern recognition, randomization, adaptive behavior

#### Colonel Blotto
- **Rounds per opponent**: 5
- **Objective**: Allocate 100 troops across 5 battlefields
- **Strategy Focus**: Resource optimization, opponent prediction

#### Penalty Kicks
- **Rounds per opponent**: 10 (actually 9 per game description)
- **Roles**: Shooter (1 point for goal) vs Goalkeeper (2 points for save)
- **Choices**: Left, Center, or Right
- **Strategy Focus**: Mixed strategies, pattern detection

#### Security Game
- **Rounds per opponent**: 5
- **Roles**: Attacker (choose target) vs Defender (allocate 30 units)
- **Targets**: [10, 20, 30] with fixed values
- **Scoring**: 
  - Defense = 0: Attacker gets full value
  - 0 < Defense < Value: Split based on allocation
  - Defense ≥ Value: Defender gets full value
- **Strategy Focus**: Game theory, Nash equilibrium, risk assessment

---

## 2. Architecture & Implementation Steps

### Phase 1: Core Architecture Setup

#### Step 2.1: Bot Base Structure
- **File**: `UserBot.StrategicMind/StrategicMindBot.cs`
- **Implements**: IBot interface
- **Properties**:
  - TeamName: "StrategicMind"
  - GameType: Dynamically determined per game
- **Methods**: All interface methods implemented

#### Step 2.2: Shared Components
- **HistoryTracker.cs**: Track opponent move patterns across all games
  - Store opponent history by game type
  - Calculate frequency distributions
  - Detect patterns (sequences, cycles)
  
- **RandomGenerator.cs**: Cryptographically secure random number generation
  - Ensure unpredictable move selection when needed
  - Support weighted random selection
  
- **StrategySelector.cs**: Meta-strategy for choosing strategies
  - Adaptive strategy switching based on opponent behavior
  - Confidence scoring for strategy effectiveness

#### Step 2.3: Game-Specific Strategy Classes

**RPSLSStrategy.cs**
- Frequency-based counter strategy
- Pattern detection (last N moves)
- Meta-strategy (Markov chains)
- Randomized fallback

**ColonelBlottoStrategy.cs**
- Nash equilibrium allocations
- Aggressive concentration strategy
- Adaptive allocation based on opponent history
- Mixed strategy approach

**PenaltyKicksStrategy.cs**
- Role-aware strategies (Shooter vs Goalkeeper)
- Frequency analysis
- Zone preference detection
- Mixed Nash equilibrium strategy

**SecurityGameStrategy.cs**
- Role-aware strategies (Attacker vs Defender)
- Expected value calculations for attacking
- Optimal defense allocation
- Exploit detection for repeated patterns

---

## 3. Strategy Integration Approach

### 3.1 Overall Strategy Philosophy
**StrategicMind** follows a three-tier approach:
1. **Exploit**: Detect and exploit opponent patterns (highest priority)
2. **Adapt**: Switch strategies based on effectiveness
3. **Equilibrium**: Fall back to Nash equilibrium when no patterns detected

### 3.2 Pattern Detection System
- **Window Size**: Analyze last 5-10 moves
- **Confidence Threshold**: 60% pattern confidence to exploit
- **Pattern Types**:
  - Frequency bias (favorite moves)
  - Sequential patterns (A→B→C)
  - Cyclic patterns (repeating sequences)
  - Reactive patterns (responses to our moves)

### 3.3 Adaptive Learning
- **Per-Game Learning**: Track strategy effectiveness within each match
- **Exploit Duration**: Try exploitation for 3 rounds, then re-evaluate
- **Fallback Mechanism**: If win rate < 40%, revert to Nash equilibrium

### 3.4 Game-Specific Integration

#### RPSLS Integration
```
IF opponent_pattern_detected(confidence > 0.6):
    SELECT counter_move to opponent's predicted move
ELSE IF game_round < 3:
    USE Nash equilibrium (uniform random)
ELSE:
    USE frequency-based counter (choose best against opponent's most common)
```

#### Colonel Blotto Integration
```
IF round == 1:
    USE Nash equilibrium allocation
ELSE IF opponent shows concentration pattern:
    ALLOCATE to avoid opponent's strong battlefields
ELSE IF opponent shows balanced pattern:
    USE aggressive concentration strategy
ELSE:
    USE adaptive mixed strategy
```

#### Penalty Kicks Integration
```
IF role == Shooter:
    IF goalkeeper_pattern_detected:
        SELECT opposite direction
    ELSE:
        USE optimally mixed strategy (weighted by success rate)
ELSE IF role == Goalkeeper:
    IF shooter_pattern_detected:
        PREDICT and match shooter's direction
    ELSE:
        USE optimally mixed defense (weighted by save value)
```

#### Security Game Integration
```
IF role == Attacker:
    CALCULATE expected_value for each target based on defender's history
    SELECT target with highest expected value
    IF no history:
        TARGET high-value (index 2) with 40% probability
ELSE IF role == Defender:
    PREDICT attacker's target based on history
    ALLOCATE defense to minimize expected loss
    IF no pattern:
        USE Nash equilibrium allocation
```

---

## 4. Testing & Validation

### 4.1 Unit Testing Strategy

**Test Coverage Requirements**: Every function must have unit tests

#### Core Component Tests
- **HistoryTracker_Tests.cs**
  - Test move storage and retrieval
  - Test frequency calculation
  - Test pattern detection accuracy
  - Test edge cases (empty history, single move)

- **RandomGenerator_Tests.cs**
  - Test distribution uniformity
  - Test weighted random selection
  - Test seed reproducibility

- **StrategySelector_Tests.cs**
  - Test strategy switching logic
  - Test confidence scoring
  - Test fallback mechanisms

#### Game Strategy Tests

**RPSLSStrategy_Tests.cs**
- Test counter-move logic for all move types
- Test pattern detection with known sequences
- Test frequency-based predictions
- Test randomization when no pattern

**ColonelBlottoStrategy_Tests.cs**
- Test allocation sums to 100
- Test all allocations are non-negative
- Test adaptive allocation logic
- Test Nash equilibrium calculations

**PenaltyKicksStrategy_Tests.cs**
- Test shooter strategy selection
- Test goalkeeper strategy selection
- Test role detection
- Test mixed strategy probabilities

**SecurityGameStrategy_Tests.cs**
- Test attacker target selection
- Test defender allocation sums to 30
- Test expected value calculations
- Test role detection

### 4.2 Integration Testing
- **Bot_IntegrationTests.cs**
  - Test all IBot interface methods
  - Test response times (< 0.3 seconds)
  - Test with simulated opponent data
  - Test strategy switching during games

### 4.3 Performance Validation
- **PerformanceTests.cs**
  - Benchmark each method call
  - Test worst-case scenarios (large history)
  - Memory profiling
  - Ensure < 0.3 second response time

### 4.4 Verification Script

**verificationScript.py**
```python
# Automated validation checks:
1. Code compilation (dotnet build)
2. Double semicolon rule enforcement (scan all .cs files)
3. Required file presence (IBot.cs, all game strategies, etc.)
4. Unit test coverage (run tests, check coverage %)
5. Execution time validation (< 0.3 sec)
6. File size limits (per file < 50KB, total < 500KB)
7. Code standards (no inline comments except documentation)
8. Test execution (all tests pass)
```

---

## 5. Submission Checklist

### 5.1 Code Files
- [ ] **StrategicMindBot.cs** - Main bot implementation
- [ ] **HistoryTracker.cs** - Move history tracking
- [ ] **RandomGenerator.cs** - Secure random number generation
- [ ] **StrategySelector.cs** - Meta-strategy selector
- [ ] **RPSLSStrategy.cs** - RPSLS game logic
- [ ] **ColonelBlottoStrategy.cs** - Colonel Blotto logic
- [ ] **PenaltyKicksStrategy.cs** - Penalty Kicks logic
- [ ] **SecurityGameStrategy.cs** - Security Game logic
- [ ] **GameConstants.cs** - Constants and configurations

### 5.2 Test Files
- [ ] **HistoryTrackerTests.cs**
- [ ] **RandomGeneratorTests.cs**
- [ ] **StrategySelectorTests.cs**
- [ ] **RPSLSStrategyTests.cs**
- [ ] **ColonelBlottoStrategyTests.cs**
- [ ] **PenaltyKicksStrategyTests.cs**
- [ ] **SecurityGameStrategyTests.cs**
- [ ] **BotIntegrationTests.cs**
- [ ] **PerformanceTests.cs**

### 5.3 Documentation Files
- [ ] **plan-workshop.md** - This file
- [ ] **ResearchAgent.md** - Research agent methodology
- [ ] **RPSLS_Skill.md** - RPSLS strategy analysis
- [ ] **colonelBlotto_Skill.md** - Colonel Blotto strategy analysis
- [ ] **penaltyKicks_Skill.md** - Penalty Kicks strategy analysis
- [ ] **securityGame_Skill.md** - Security Game strategy analysis
- [ ] **plan-rpsls.md** - RPSLS implementation plan
- [ ] **plan-colonelBlotto.md** - Colonel Blotto implementation plan
- [ ] **plan-penaltyKicks.md** - Penalty Kicks implementation plan
- [ ] **plan-securityGame.md** - Security Game implementation plan

### 5.4 Validation Files
- [ ] **verificationScript.py** - Automated validation script

### 5.5 Project Files
- [ ] **UserBot.sln** - Solution file
- [ ] **UserBot.StrategicMind.csproj** - Project file with proper references
- [ ] **Directory.Build.props** - Build configuration

### 5.6 Pre-Submission Validation
- [ ] All code follows double semicolon rule (//)
- [ ] All functions have unit tests
- [ ] Only classes and functions have documentation
- [ ] No inline comments in code
- [ ] All tests pass
- [ ] Build succeeds without errors
- [ ] Response time < 0.3 seconds verified
- [ ] File sizes within limits (per file < 50KB)
- [ ] Total size < 500KB
- [ ] Only approved .NET libraries used
- [ ] Target framework: net8.0

### 5.7 MCP Verification & Submission
- [ ] Run MCP verify endpoint (before final submission)
- [ ] Address any verification issues
- [ ] Run MCP submit endpoint
- [ ] Confirm successful submission

---

## 6. Implementation Timeline

### Phase 1: Foundation (Complete Steps 1-5 of workshop)
- Set up MCP server
- Download bot template
- Create instruction files
- Generate this plan

### Phase 2: Research (Step 6)
- Create Research Agent
- Generate skill files for all 4 games

### Phase 3: Game Planning (Step 7)
- Create game-specific plans
- Detail strategies for each game

### Phase 4: Implementation - RPSLS (Steps 8-9, Game 1)
- Implement RPSLSStrategy
- Write unit tests
- Run verification script

### Phase 5: Implementation - Colonel Blotto (Steps 8-9, Game 2)
- Implement ColonelBlottoStrategy
- Write unit tests
- Run verification script

### Phase 6: Implementation - Penalty Kicks (Steps 8-9, Game 3)
- Implement PenaltyKicksStrategy
- Write unit tests
- Run verification script

### Phase 7: Implementation - Security Game (Steps 8-9, Game 4)
- Implement SecurityGameStrategy
- Write unit tests
- Run verification script

### Phase 8: Integration & Testing
- Integrate all strategies into StrategicMindBot
- Run full integration tests
- Performance validation

### Phase 9: Final Validation & Submission (Steps 10-11)
- Package submission
- Run MCP verification
- Submit to tournament

---

## 7. Success Criteria

### Minimum Viable Bot
- Implements all IBot interface methods
- Compiles without errors
- Responds within time limit (< 0.3 sec)
- Passes all unit tests
- Follows all coding standards

### Competitive Bot
- Defeats naive strategies consistently
- Adapts to opponent patterns
- Shows win rate > 50% in testing
- Efficient resource allocation
- Balanced between exploitation and equilibrium

### Championship-Level Bot
- Exploits patterns rapidly (within 2-3 rounds)
- Near-optimal Nash equilibrium fallback
- Robust against counter-strategies
- High performance across all 4 games
- Win rate > 60% against diverse opponents

---

## Notes

- **Bot Name**: StrategicMind
- **Philosophy**: Exploit patterns when detected, adapt strategies based on effectiveness, fall back to Nash equilibrium when uncertain
- **Key Differentiator**: Strong pattern detection combined with game-theory-optimal fallback strategies
- **Development Approach**: Incremental implementation, one game at a time, with continuous validation

---

**Generated**: February 16, 2026  
**Workshop**: Boost Day Tournament Workshop  
**Agent**: GitHub Copilot AI Agent
