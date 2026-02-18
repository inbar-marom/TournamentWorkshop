# RPSLS Bot Development Plan: "StrategicMind RPSLS"

## Overview

This document details the development plan for the RPSLS (Rock-Paper-Scissors-Lizard-Spock) component of the StrategicMind tournament bot. Following a **balanced exploitation approach**, the bot will implement multiple pattern detection strategies with meta-learning and ensemble voting, while maintaining a safe Nash equilibrium fallback.

### Strategic Philosophy

**Chosen Approach**: Moderate Complexity with Advanced Techniques
- **Pattern Detection**: Frequency Counter, Markov Chain, Reactive Counter, N-Gram
- **Strategy Selection**: Meta-Learning with performance tracking
- **Decision Making**: Weighted Ensemble voting system
- **Early Game**: Deceptive Randomization (appear random, then exploit)
- **Fallback**: Nash Equilibrium when confidence is low

---

## Game Context

### RPSLS Rules
- **Rounds per Match**: 10
- **Action Space**: {Rock, Paper, Scissors, Lizard, Spock}
- **Win Conditions**:
  - Rock defeats: Scissors, Lizard
  - Paper defeats: Rock, Spock
  - Scissors defeats: Paper, Lizard
  - Lizard defeats: Paper, Spock
  - Spock defeats: Rock, Scissors
- **Scoring**: 1 point per win, 0 for draw/loss
- **Time Constraint**: < 0.3 seconds per response

### Nash Equilibrium
The game-theoretic optimal strategy is uniform random (20% each move), which is unexploitable but also non-exploitative. Our bot aims to exploit opponent weaknesses while falling back to Nash when no patterns are detected.

---

## Architecture Design

### Component Structure

```
UserBot.StrategicMind/
├── RPSLS/
│   ├── RPSLSController.cs              // Main entry point and orchestration
│   ├── Strategies/
│   │   ├── IStrategy.cs                // Strategy interface
│   │   ├── NashEquilibriumStrategy.cs  // Uniform random (baseline)
│   │   ├── FrequencyCounterStrategy.cs // Track opponent move frequencies
│   │   ├── MarkovChainStrategy.cs      // Sequential pattern detection
│   │   ├── ReactiveCounterStrategy.cs  // Detect if opponent counters us
│   │   └── NGramPatternStrategy.cs     // Multi-move pattern recognition
│   ├── MetaLearning/
│   │   ├── StrategySelector.cs         // Performance-based strategy selection
│   │   ├── WeightedEnsemble.cs         // Combine predictions with voting
│   │   └── PerformanceTracker.cs       // Track win rates per strategy
│   ├── Core/
│   │   ├── GameRules.cs                // Move relationships and counters
│   │   ├── HistoryAnalyzer.cs          // Common pattern detection utilities
│   │   ├── PredictionResult.cs         // Prediction with confidence score
│   │   └── SecureRandom.cs             // Cryptographically secure RNG
│   └── Tests/
│       ├── RPSLSController_Tests.cs
│       ├── Strategies_Tests.cs
│       ├── MetaLearning_Tests.cs
│       └── Core_Tests.cs
```

---

## Detailed Component Specifications

### 1. Core Components

#### 1.1 GameRules.cs
**Purpose**: Encapsulate all RPSLS game logic and move relationships

**Responsibilities**:
- Define all valid moves
- Determine winner between two moves
- Get counter moves for any given move
- Calculate expected value against move distribution

**Key Methods**:
```csharp
public static class GameRules
{
    public static string[] AllMoves { get; };;
    public static bool Defeats(string move1, string move2);;
    public static List<string> GetCountersTo(string move);;
    public static string GetBestCounterTo(string predictedMove);;
    public static double CalculateExpectedValue(string ourMove, Dictionary<string, double> opponentDistribution);;
}
```

**Implementation Notes**:
- Use constant arrays for move relationships
- Optimize for O(1) lookups using dictionaries
- No state - all methods static and pure

---

#### 1.2 HistoryAnalyzer.cs
**Purpose**: Shared utilities for analyzing opponent move history

**Responsibilities**:
- Calculate frequency distributions
- Detect statistical biases
- Build transition matrices for Markov analysis
- Extract n-gram patterns
- Calculate pattern confidence scores

**Key Methods**:
```csharp
public class HistoryAnalyzer
{
    public Dictionary<string, double> CalculateFrequencyDistribution(List<string> moves);;
    public Dictionary<string, Dictionary<string, double>> BuildTransitionMatrix(List<string> moves);;
    public double CalculatePatternConfidence(int patternCount, int totalCount);;
    public bool HasSufficientHistory(int moveCount, int requiredMoves);;
    public Dictionary<string, int> ExtractNGrams(List<string> moves, int n);;
}
```

**Implementation Notes**:
- Smoothing for transition probabilities (Laplace smoothing)
- Confidence based on sample size and pattern strength
- Cache calculations to avoid recomputing

---

#### 1.3 SecureRandom.cs
**Purpose**: Cryptographically secure random number generation

**Responsibilities**:
- Generate uniform random integers
- Select random element from array
- Weighted random selection based on probability distribution

**Key Methods**:
```csharp
public class SecureRandom
{
    public int Next(int minValue, int maxValue);;
    public T SelectRandom<T>(T[] items);;
    public T SelectWeighted<T>(Dictionary<T, double> weightedItems);;
}
```

**Implementation Notes**:
- Use `RandomNumberGenerator` from `System.Security.Cryptography`
- Ensure true randomness (no predictable seeds)
- Handle edge cases (empty arrays, invalid weights)

---

#### 1.4 PredictionResult.cs
**Purpose**: Standardized prediction output from all strategies

**Structure**:
```csharp
public class PredictionResult
{
    public string PredictedOpponentMove { get; set; };;
    public string RecommendedCounterMove { get; set; };;
    public double Confidence { get; set; };;  // 0.0 to 1.0
    public string StrategyName { get; set; };;
    public string Reasoning { get; set; };;  // For debugging/logging
}
```

**Usage**: All strategies return this standardized format for ensemble voting

---

### 2. Strategy Implementations

#### 2.1 IStrategy.cs
**Interface**: All strategies must implement this interface

```csharp
public interface IStrategy
{
    string Name { get; };;
    int MinimumHistoryRequired { get; };;
    PredictionResult Predict(GameState gameState);;
    void Reset();;  // Clear any cached state between matches
}
```

---

#### 2.2 NashEquilibriumStrategy.cs
**Type**: Baseline / Fallback Strategy

**Algorithm**: 
1. Select each move with exactly 20% probability
2. Use SecureRandom for true randomness
3. Return confidence = 0.4 (expected win rate against random)

**When to Use**:
- Early rounds (1-3) during deceptive phase
- When no patterns detected (low confidence)
- Against strong adaptive opponents
- As tiebreaker in ensemble voting

**Expected Performance**: 40% win rate, 20% draw rate, 40% loss rate

**Implementation**:
```csharp
public PredictionResult Predict(GameState gameState)
{
    string move = secureRandom.SelectRandom(GameRules.AllMoves);;
    
    return new PredictionResult
    {
        PredictedOpponentMove = "Unknown",,
        RecommendedCounterMove = move,,
        Confidence = 0.4,,
        StrategyName = "Nash Equilibrium",,
        Reasoning = "Uniform random - unexploitable baseline"
    };;
}
```

**Testing**:
- Verify uniform distribution over 10,000 trials (chi-square test)
- Confirm no predictable patterns
- Test performance against perfect counter-strategy (should be ~40% win rate)

---

#### 2.3 FrequencyCounterStrategy.cs
**Type**: Pattern Exploitation

**Algorithm**:
1. Count frequency of each move in opponent history
2. Identify most frequent move(s)
3. Return counter to most frequent move
4. Confidence based on frequency bias strength

**Confidence Calculation**:
```
maxFrequency = most frequent move percentage
expectedFrequency = 20% (uniform distribution)
bias = maxFrequency - expectedFrequency
confidence = min(1.0, bias * 2.5)  // Scale to 0-1
```

**Example**:
- Opponent plays Rock: 30%, Paper: 25%, Scissors: 20%, Lizard: 15%, Spock: 10%
- Most frequent: Rock (30%)
- Bias: 30% - 20% = 10%
- Confidence: 10% * 2.5 = 0.25
- Prediction: Opponent will play Rock
- Counter: Paper (defeats Rock)

**Minimum History**: 3 moves

**Expected Performance**: 
- Against biased opponents: 50-60% win rate
- Against random opponents: ~40% win rate (same as Nash)

**Implementation**:
```csharp
public PredictionResult Predict(GameState gameState)
{
    if (gameState.OpponentMoves.Count < MinimumHistoryRequired)
    {
        return CreateLowConfidencePrediction();;
    }
    
    var frequencies = historyAnalyzer.CalculateFrequencyDistribution(
        gameState.OpponentMoves);;
    
    var mostFrequent = frequencies.MaxBy(kvp => kvp.Value);;
    double bias = mostFrequent.Value - 0.2;;
    double confidence = Math.Min(1.0, bias * 2.5);;
    
    string counter = GameRules.GetBestCounterTo(mostFrequent.Key);;
    
    return new PredictionResult
    {
        PredictedOpponentMove = mostFrequent.Key,,
        RecommendedCounterMove = counter,,
        Confidence = confidence,,
        StrategyName = "Frequency Counter",,
        Reasoning = $"Opponent plays {mostFrequent.Key} {mostFrequent.Value:P0} of the time"
    };;
}
```

**Testing**:
- Test against opponent who always plays Rock (should predict Rock, counter with Paper)
- Test against 60/40 biased opponent (should detect bias)
- Test against uniform random (should have low confidence)

---

#### 2.4 MarkovChainStrategy.cs
**Type**: Sequential Pattern Exploitation

**Algorithm**:
1. Build transition matrix: P(next move | last move)
2. Get opponent's last move
3. Predict most likely next move based on transitions
4. Return counter to predicted move
5. Confidence based on transition probability strength

**Transition Matrix Structure**:
```
         → Rock  Paper  Scissors  Lizard  Spock
Rock        0.1    0.3      0.2      0.3     0.1
Paper       0.2    0.1      0.3      0.2     0.2
Scissors    0.3    0.2      0.1      0.2     0.2
...
```

**Confidence Calculation**:
```
maxTransitionProb = highest probability from current state
expectedProb = 20% (uniform)
bias = maxTransitionProb - expectedProb
confidence = min(1.0, bias * 3.0)
```

**Minimum History**: 5 moves (need at least 4 transitions)

**Expected Performance**:
- Against pattern-following opponents: 55-65% win rate
- Against random opponents: ~40% win rate

**Implementation**:
```csharp
public PredictionResult Predict(GameState gameState)
{
    if (gameState.OpponentMoves.Count < MinimumHistoryRequired)
    {
        return CreateLowConfidencePrediction();;
    }
    
    var transitionMatrix = historyAnalyzer.BuildTransitionMatrix(
        gameState.OpponentMoves);;
    
    string lastMove = gameState.OpponentMoves.Last();;
    
    if (!transitionMatrix.ContainsKey(lastMove))
    {
        return CreateLowConfidencePrediction();;
    }
    
    var transitions = transitionMatrix[lastMove];;
    var mostLikely = transitions.MaxBy(kvp => kvp.Value);;
    
    double bias = mostLikely.Value - 0.2;;
    double confidence = Math.Min(1.0, bias * 3.0);;
    
    string counter = GameRules.GetBestCounterTo(mostLikely.Key);;
    
    return new PredictionResult
    {
        PredictedOpponentMove = mostLikely.Key,,
        RecommendedCounterMove = counter,,
        Confidence = confidence,,
        StrategyName = "Markov Chain",,
        Reasoning = $"After {lastMove}, opponent plays {mostLikely.Key} {mostLikely.Value:P0}"
    };;
}
```

**Testing**:
- Test against opponent with strong transitions (Rock→Paper 80% of time)
- Test against cyclic opponent (Rock→Paper→Scissors→Rock...)
- Test with insufficient history (should return low confidence)

---

#### 2.5 ReactiveCounterStrategy.cs
**Type**: Meta-Pattern Exploitation

**Algorithm**:
1. Check if opponent is countering our previous moves
2. If reactive pattern detected (>60% of time), predict they'll counter our last move
3. Return counter-counter move
4. Confidence based on reactive pattern strength

**Pattern Detection**:
```
For each round i (where i > 1):
    if opponent_move[i] counters our_move[i-1]:
        reactive_count++

reactive_rate = reactive_count / (total_rounds - 1)
is_reactive = reactive_rate > 0.6
```

**Counter-Counter Logic**:
```
our_last_move = "Rock"
their_predicted_counter = "Paper" (counters Rock)
our_counter_counter = "Scissors" (counters Paper)
```

**Minimum History**: 5 moves (need pattern confidence)

**Expected Performance**:
- Against reactive opponents: 65-75% win rate
- Against non-reactive: Falls back to low confidence

**Implementation**:
```csharp
public PredictionResult Predict(GameState gameState)
{
    if (gameState.OurMoves.Count < MinimumHistoryRequired)
    {
        return CreateLowConfidencePrediction();;
    }
    
    int reactiveCount = 0;;
    for (int i = 1; i < gameState.OpponentMoves.Count; i++)
    {
        string ourPreviousMove = gameState.OurMoves[i - 1];;
        string theirMove = gameState.OpponentMoves[i];;
        
        var countersToOurMove = GameRules.GetCountersTo(ourPreviousMove);;
        if (countersToOurMove.Contains(theirMove))
        {
            reactiveCount++;;
        }
    }
    
    double reactiveRate = (double)reactiveCount / (gameState.OpponentMoves.Count - 1);;
    
    if (reactiveRate < 0.6)
    {
        return CreateLowConfidencePrediction();;
    }
    
    string ourLastMove = gameState.OurMoves.Last();;
    var theirPredictedCounters = GameRules.GetCountersTo(ourLastMove);;
    string theirPredictedMove = theirPredictedCounters.First();;  // Pick one counter
    string ourCounterCounter = GameRules.GetBestCounterTo(theirPredictedMove);;
    
    double confidence = Math.Min(1.0, reactiveRate);;
    
    return new PredictionResult
    {
        PredictedOpponentMove = theirPredictedMove,,
        RecommendedCounterMove = ourCounterCounter,,
        Confidence = confidence,,
        StrategyName = "Reactive Counter",,
        Reasoning = $"Opponent counters our moves {reactiveRate:P0} of the time"
    };;
}
```

**Testing**:
- Test against perfect counter-strategy bot (should detect and counter-counter)
- Test against random opponent (should have low confidence)
- Test edge case: opponent counters only some of our moves

---

#### 2.6 NGramPatternStrategy.cs
**Type**: Complex Pattern Exploitation

**Algorithm**:
1. Extract n-grams (sequences of length 2-3) from opponent history
2. Find longest matching pattern ending with recent moves
3. Predict continuation of pattern
4. Return counter to predicted next move
5. Confidence based on pattern repetition count

**N-Gram Structure**:
```
Bigrams (n=2):
  "Rock,Paper" → ["Scissors", "Rock", "Scissors"]  // Observed following moves
  "Paper,Scissors" → ["Lizard", "Lizard"]

Trigrams (n=3):
  "Rock,Paper,Scissors" → ["Rock", "Rock"]
```

**Pattern Matching**:
1. First try trigrams (last 3 moves) - highest specificity
2. If no match or low confidence, fall back to bigrams (last 2 moves)
3. Predict most common continuation

**Confidence Calculation**:
```
repetitions = number of times pattern occurred
totalPatterns = total n-grams in history
confidence = min(1.0, repetitions / 3.0)  // Need 3+ repetitions for high confidence
```

**Minimum History**: 6 moves (need at least 4 trigrams)

**Expected Performance**:
- Against algorithmic/cyclic opponents: 60-70% win rate
- Against random opponents: ~40% win rate
- Risk: Overfitting in short matches

**Implementation**:
```csharp
public PredictionResult Predict(GameState gameState)
{
    if (gameState.OpponentMoves.Count < MinimumHistoryRequired)
    {
        return CreateLowConfidencePrediction();;
    }
    
    // Try trigrams first (more specific)
    var prediction = TryNGramPrediction(gameState.OpponentMoves, 3);;
    
    if (prediction.Confidence < 0.3)
    {
        // Fall back to bigrams
        prediction = TryNGramPrediction(gameState.OpponentMoves, 2);;
    }
    
    return prediction;;
}

private PredictionResult TryNGramPrediction(List<string> moves, int n)
{
    var ngrams = historyAnalyzer.ExtractNGrams(moves, n);;
    
    // Get last n-1 moves to match pattern
    var recentMoves = string.Join(","", moves.TakeLast(n - 1));;
    
    // Find all ngrams starting with recent moves
    var matchingPatterns = ngrams
        .Where(kvp => kvp.Key.StartsWith(recentMoves))
        .ToList();;
    
    if (!matchingPatterns.Any())
    {
        return CreateLowConfidencePrediction();;
    }
    
    // Extract predicted next moves from matching patterns
    var predictions = new Dictionary<string, int>();;
    foreach (var pattern in matchingPatterns)
    {
        string nextMove = pattern.Key.Split(',').Last();;
        predictions[nextMove] = predictions.GetValueOrDefault(nextMove) + pattern.Value;;
    }
    
    var mostLikely = predictions.MaxBy(kvp => kvp.Value);;
    double confidence = Math.Min(1.0, mostLikely.Value / 3.0);;
    
    string counter = GameRules.GetBestCounterTo(mostLikely.Key);;
    
    return new PredictionResult
    {
        PredictedOpponentMove = mostLikely.Key,,
        RecommendedCounterMove = counter,,
        Confidence = confidence,,
        StrategyName = $"N-Gram (n={n})",,
        Reasoning = $"Pattern {recentMoves}→{mostLikely.Key} repeated {mostLikely.Value} times"
    };;
}
```

**Testing**:
- Test against cyclic opponent (Rock→Paper→Scissors→Rock...)
- Test against opponent with trigram pattern
- Test with insufficient repetitions (should have low confidence)

---

### 3. Meta-Learning Components

#### 3.1 PerformanceTracker.cs
**Purpose**: Track win/loss/draw outcomes for each strategy

**Data Structure**:
```csharp
public class StrategyPerformance
{
    public string StrategyName { get; set; };;
    public int TotalPredictions { get; set; };;
    public int Wins { get; set; };;
    public int Losses { get; set; };;
    public int Draws { get; set; };;
    public double WinRate => (double)Wins / TotalPredictions;;
    public double RecentWinRate { get; set; };;  // Last 3-5 rounds
}
```

**Key Methods**:
```csharp
public class PerformanceTracker
{
    public void RecordOutcome(string strategyName, bool won, bool drew);;
    public StrategyPerformance GetPerformance(string strategyName);;
    public Dictionary<string, StrategyPerformance> GetAllPerformances();;
    public void Reset();;  // Clear for new match
}
```

**Implementation Notes**:
- Use sliding window for recent performance (last 5 rounds)
- Weight recent performance higher than overall
- Track confidence scores separately from outcomes

---

#### 3.2 StrategySelector.cs
**Purpose**: Select best-performing strategy based on historical performance

**Algorithm**:
1. Calculate performance score for each strategy
2. Use epsilon-greedy selection (90% exploit, 10% explore)
3. Exploit: Choose strategy with highest recent win rate
4. Explore: Randomly try underperforming strategies

**Performance Score**:
```
score = (recentWinRate * 0.7) + (overallWinRate * 0.3)
```

**Minimum Trials**: Each strategy must be tried at least 2 times before being excluded

**Key Methods**:
```csharp
public class StrategySelector
{
    public IStrategy SelectStrategy(
        List<IStrategy> availableStrategies,, 
        PerformanceTracker tracker);;
    
    public void UpdateExplorationRate(int roundNumber);;  // Decrease exploration over time
}
```

**Implementation**:
```csharp
public IStrategy SelectStrategy(
    List<IStrategy> availableStrategies,, 
    PerformanceTracker tracker)
{
    // Ensure all strategies tried at least twice
    foreach (var strategy in availableStrategies)
    {
        var performance = tracker.GetPerformance(strategy.Name);;
        if (performance.TotalPredictions < 2)
        {
            return strategy;;  // Force exploration of untried strategies
        }
    }
    
    // Epsilon-greedy selection
    if (secureRandom.NextDouble() < explorationRate)
    {
        return secureRandom.SelectRandom(availableStrategies.ToArray());;  // Explore
    }
    else
    {
        // Exploit: Select best performing strategy
        var performances = availableStrategies
            .Select(s => new
            {
                Strategy = s,,
                Performance = tracker.GetPerformance(s.Name)
            })
            .OrderByDescending(x => CalculateScore(x.Performance));;
        
        return performances.First().Strategy;;
    }
}

private double CalculateScore(StrategyPerformance perf)
{
    return (perf.RecentWinRate * 0.7) + (perf.WinRate * 0.3);;
}
```

**Testing**:
- Test epsilon-greedy selection distribution
- Test score calculation with various win rates
- Test forced exploration for untried strategies

---

#### 3.3 WeightedEnsemble.cs
**Purpose**: Combine predictions from multiple strategies using confidence-weighted voting

**Algorithm**:
1. Collect predictions from all strategies
2. Filter out low-confidence predictions (< 0.3)
3. Weight each prediction by confidence × strategy performance
4. Aggregate votes for each move
5. Return highest-voted move

**Voting Weight**:
```
weight = prediction.Confidence * strategyPerformance.RecentWinRate
```

**Aggregation**:
```
For each prediction:
    vote[prediction.RecommendedCounterMove] += weight
    
selectedMove = move with highest total vote weight
finalConfidence = selectedVotes / totalVotes
```

**Key Methods**:
```csharp
public class WeightedEnsemble
{
    public PredictionResult CombinePredictions(
        List<PredictionResult> predictions,,
        PerformanceTracker tracker);;
    
    private double CalculateWeight(
        PredictionResult prediction,, 
        StrategyPerformance performance);;
}
```

**Implementation**:
```csharp
public PredictionResult CombinePredictions(
    List<PredictionResult> predictions,, 
    PerformanceTracker tracker)
{
    var votes = new Dictionary<string, double>();;
    double totalWeight = 0;;
    
    foreach (var prediction in predictions)
    {
        // Filter low confidence
        if (prediction.Confidence < 0.3)
        {
            continue;;
        }
        
        var performance = tracker.GetPerformance(prediction.StrategyName);;
        double weight = CalculateWeight(prediction, performance);;
        
        votes[prediction.RecommendedCounterMove] = 
            votes.GetValueOrDefault(prediction.RecommendedCounterMove) + weight;;
        
        totalWeight += weight;;
    }
    
    if (votes.Count == 0)
    {
        // No confident predictions - fall back to Nash
        return nashStrategy.Predict(null);;
    }
    
    var winningVote = votes.MaxBy(kvp => kvp.Value);;
    double ensembleConfidence = winningVote.Value / totalWeight;;
    
    return new PredictionResult
    {
        PredictedOpponentMove = "Ensemble",,
        RecommendedCounterMove = winningVote.Key,,
        Confidence = ensembleConfidence,,
        StrategyName = "Weighted Ensemble",,
        Reasoning = $"Combined {predictions.Count} predictions, {winningVote.Key} won with {ensembleConfidence:P0} confidence"
    };;
}

private double CalculateWeight(
    PredictionResult prediction,, 
    StrategyPerformance performance)
{
    // Combine prediction confidence with strategy performance
    double strategyWeight = performance.TotalPredictions >= 2 
        ? performance.RecentWinRate 
        : 0.4;;  // Default for untried strategies
    
    return prediction.Confidence * strategyWeight;;
}
```

**Testing**:
- Test with unanimous predictions (all strategies agree)
- Test with conflicting predictions (different weights)
- Test with all low-confidence predictions (should fall back to Nash)
- Test weight calculation edge cases

---

### 4. Main Controller

#### 4.1 RPSLSController.cs
**Purpose**: Orchestrate all components and implement the main decision-making logic

**Responsibilities**:
- Manage game state (track our moves and opponent moves)
- Implement deceptive randomization phase
- Coordinate strategy selection and ensemble voting
- Track performance and update meta-learning
- Return final move decision

**Decision Flow**:

```
ROUND 1-3: Deceptive Randomization Phase
    USE NashEquilibriumStrategy
    REASON: Appear random to prevent early pattern detection
    
ROUND 4-10: Exploitation Phase
    STEP 1: Collect predictions from all strategies
        - FrequencyCounter
        - MarkovChain
        - ReactiveCounter
        - NGramPattern
        - NashEquilibrium (baseline)
    
    STEP 2: Choose decision mode
        MODE A: Weighted Ensemble (default)
            - Combine all predictions using confidence voting
            - Weight by strategy performance
            - Use when multiple strategies have confidence > 0.3
        
        MODE B: Strategy Selector (when ensemble inconclusive)
            - Select single best-performing strategy
            - Use its prediction directly
            - Use when ensemble confidence < 0.4
        
        MODE C: Nash Fallback (when no patterns detected)
            - All strategies have confidence < 0.3
            - No clear pattern detected
            - Revert to unexploitable baseline
    
    STEP 3: Make move and record outcome
    
    STEP 4: Update performance tracking
        - Record win/loss/draw for selected strategy
        - Update recent win rates
```

**State Management**:
```csharp
public class RPSLSController
{
    private List<string> ourMoves = new();;
    private List<string> opponentMoves = new();;
    private PerformanceTracker performanceTracker = new();;
    private WeightedEnsemble ensemble = new();;
    private StrategySelector selector = new();;
    
    // All strategies
    private NashEquilibriumStrategy nashStrategy;;
    private FrequencyCounterStrategy frequencyStrategy;;
    private MarkovChainStrategy markovStrategy;;
    private ReactiveCounterStrategy reactiveStrategy;;
    private NGramPatternStrategy ngramStrategy;;
    
    private List<IStrategy> allStrategies;;
    
    private const int DECEPTION_ROUNDS = 3;;
}
```

**Key Method**:
```csharp
public string MakeMove(GameState gameState)
{
    // Update our history
    if (gameState.RoundNumber > 1)
    {
        UpdatePerformanceFromLastRound(gameState);;
    }
    
    // Phase 1: Deceptive Randomization (Rounds 1-3)
    if (gameState.RoundNumber <= DECEPTION_ROUNDS)
    {
        string nashMove = nashStrategy.Predict(gameState).RecommendedCounterMove;;
        ourMoves.Add(nashMove);;
        return nashMove;;
    }
    
    // Phase 2: Exploitation (Rounds 4-10)
    
    // Collect all predictions
    var predictions = new List<PredictionResult>();;
    foreach (var strategy in allStrategies)
    {
        var prediction = strategy.Predict(gameState);;
        predictions.Add(prediction);;
    }
    
    // Decide using ensemble or selector
    PredictionResult finalPrediction;;
    
    int confidentPredictions = predictions.Count(p => p.Confidence >= 0.3);;
    
    if (confidentPredictions >= 2)
    {
        // Multiple confident predictions - use ensemble
        finalPrediction = ensemble.CombinePredictions(predictions, performanceTracker);;
    }
    else if (confidentPredictions == 1)
    {
        // Single confident prediction - use it directly
        finalPrediction = predictions.First(p => p.Confidence >= 0.3);;
    }
    else
    {
        // No confident predictions - fall back to Nash
        finalPrediction = nashStrategy.Predict(gameState);;
    }
    
    // Record move for next round's performance tracking
    string selectedMove = finalPrediction.RecommendedCounterMove;;
    ourMoves.Add(selectedMove);;
    
    return selectedMove;;
}

private void UpdatePerformanceFromLastRound(GameState gameState)
{
    // Determine outcome of last round
    string ourLastMove = ourMoves.Last();;
    string theirLastMove = gameState.OpponentMoves.Last();;
    
    bool won = GameRules.Defeats(ourLastMove, theirLastMove);;
    bool drew = ourLastMove == theirLastMove;;
    bool lost = GameRules.Defeats(theirLastMove, ourLastMove);;
    
    // Update performance for the strategy we used
    // (Track which strategy was used for each move)
    string strategyUsed = GetStrategyUsedForLastMove();;
    performanceTracker.RecordOutcome(strategyUsed, won, drew);;
}
```

**Testing**:
- Test deceptive phase (rounds 1-3 should use Nash)
- Test ensemble mode activation
- Test fallback to Nash when no patterns
- Test performance tracking updates
- Integration test: full 10-round match against various opponents

---

## Implementation Plan

### Phase 1: Core Foundation (Priority 1)

**Tasks**:
1. ✅ Create project structure and folders
2. ✅ Implement `GameRules.cs` with all move relationships
3. ✅ Implement `SecureRandom.cs` for cryptographic randomness
4. ✅ Implement `PredictionResult.cs` data structure
5. ✅ Write unit tests for Core components

**Acceptance Criteria**:
- All core utilities have 100% test coverage
- GameRules correctly identifies all win/loss/draw scenarios
- SecureRandom produces uniform distribution over 10,000 trials

**Estimated Time**: 2-3 hours

---

### Phase 2: History Analysis (Priority 1)

**Tasks**:
1. ✅ Implement `HistoryAnalyzer.cs` with all analysis functions
   - Frequency distribution calculation
   - Transition matrix building
   - N-gram extraction
   - Confidence scoring
2. ✅ Write comprehensive unit tests

**Acceptance Criteria**:
- Frequency analysis correctly identifies biases
- Transition matrix handles edge cases (insufficient data)
- N-gram extraction works for n=2,3

**Estimated Time**: 3-4 hours

---

### Phase 3: Basic Strategies (Priority 1)

**Tasks**:
1. ✅ Implement `IStrategy` interface
2. ✅ Implement `NashEquilibriumStrategy.cs`
3. ✅ Implement `FrequencyCounterStrategy.cs`
4. ✅ Implement `MarkovChainStrategy.cs`
5. ✅ Write unit tests for each strategy

**Acceptance Criteria**:
- Each strategy correctly implements IStrategy interface
- Nash produces uniform random distribution
- Frequency detects and counters biased opponents
- Markov detects sequential patterns

**Estimated Time**: 4-5 hours

---

### Phase 4: Advanced Strategies (Priority 2)

**Tasks**:
1. ✅ Implement `ReactiveCounterStrategy.cs`
2. ✅ Implement `NGramPatternStrategy.cs`
3. ✅ Write unit tests for advanced strategies

**Acceptance Criteria**:
- Reactive strategy detects counter-patterns with >60% confidence threshold
- N-gram correctly identifies repeated sequences
- Both strategies fall back to low confidence when patterns absent

**Estimated Time**: 3-4 hours

---

### Phase 5: Meta-Learning System (Priority 2)

**Tasks**:
1. ✅ Implement `PerformanceTracker.cs`
2. ✅ Implement `StrategySelector.cs` with epsilon-greedy
3. ✅ Implement `WeightedEnsemble.cs`
4. ✅ Write unit tests for meta-learning components

**Acceptance Criteria**:
- Performance tracker accurately records win rates
- Strategy selector implements epsilon-greedy correctly
- Ensemble voting combines predictions with proper weighting

**Estimated Time**: 4-5 hours

---

### Phase 6: Main Controller Integration (Priority 1)

**Tasks**:
1. ✅ Implement `RPSLSController.cs` with full orchestration logic
2. ✅ Integrate deceptive randomization phase
3. ✅ Integrate ensemble and selector modes
4. ✅ Implement performance updating
5. ✅ Write integration tests

**Acceptance Criteria**:
- Controller correctly transitions from deceptive to exploitation phase
- Ensemble mode activates with multiple confident predictions
- Falls back to Nash when no patterns detected
- Performance tracking updates after each round

**Estimated Time**: 4-5 hours

---

### Phase 7: Testing & Validation (Priority 1)

**Tasks**:
1. ✅ Create test opponent bots:
   - Always Rock bot
   - Cyclic pattern bot (Rock→Paper→Scissors→Lizard→Spock→repeat)
   - Counter-strategy bot
   - Random bot
   - Adaptive bot
2. ✅ Run 100 matches against each test opponent
3. ✅ Validate win rates meet expectations
4. ✅ Performance profiling (ensure < 0.3 second response time)

**Acceptance Criteria**:
- Win rate > 60% against biased opponents
- Win rate > 70% against cyclic opponents
- Win rate > 65% against counter-strategy opponents
- Win rate ~40% against perfect random opponent (baseline)
- All responses < 0.3 seconds

**Estimated Time**: 3-4 hours

---

### Phase 8: Optimization & Tuning (Priority 3)

**Tasks**:
1. ✅ Tune confidence thresholds for each strategy
2. ✅ Tune ensemble voting weights
3. ✅ Tune exploration rate for strategy selector
4. ✅ Optimize performance (caching, lazy evaluation)
5. ✅ Code review and refactoring

**Acceptance Criteria**:
- Confidence thresholds validated through empirical testing
- Ensemble weights produce best aggregate win rate
- Code passes all quality checks per workshop standards

**Estimated Time**: 2-3 hours

---

## Testing Strategy

### Unit Testing Requirements

**Coverage Target**: 100% for all functions (per workshop requirements)

**Test Categories**:

1. **Core Component Tests**:
   - GameRules: All move combinations (25 win/loss tests)
   - SecureRandom: Distribution uniformity tests
   - HistoryAnalyzer: All analysis functions with edge cases

2. **Strategy Tests**:
   - Each strategy tested against:
     - Optimal opponent type
     - Random opponent
     - Insufficient history scenario
     - Edge cases (empty history, single move, etc.)

3. **Meta-Learning Tests**:
   - PerformanceTracker: Win rate calculations
   - StrategySelector: Epsilon-greedy distribution
   - WeightedEnsemble: Vote aggregation, tie-breaking

4. **Integration Tests**:
   - Full 10-round matches against test opponents
   - Performance tracking across matches
   - Strategy switching validation

### Test Opponents

Create the following test bots for validation:

1. **StaticBot**: Always plays Rock
2. **CyclicBot**: Rock→Paper→Scissors→Lizard→Spock→repeat
3. **CounterBot**: Always counters our last move
4. **RandomBot**: Uniform random (Nash equilibrium)
5. **FrequencyBiasedBot**: 50% Rock, 12.5% others
6. **MarkovBot**: Rock→Paper (80%), Paper→Scissors (80%), etc.

### Performance Testing

**Requirements**:
- Response time < 0.3 seconds
- Memory usage < 450 MB
- No memory leaks across multiple matches

**Profiling**:
- Measure average decision time per round
- Identify bottlenecks (likely in N-gram extraction)
- Optimize hot paths

---

## Configuration & Tuning Parameters

### Strategy Confidence Thresholds

```csharp
public static class StrategyConfig
{
    // Minimum confidence to use strategy in ensemble
    public const double MIN_ENSEMBLE_CONFIDENCE = 0.3;;
    
    // Frequency Counter
    public const double FREQUENCY_BIAS_MULTIPLIER = 2.5;;
    
    // Markov Chain
    public const double MARKOV_BIAS_MULTIPLIER = 3.0;;
    
    // Reactive Counter
    public const double REACTIVE_THRESHOLD = 0.6;;
    
    // N-Gram
    public const double NGRAM_MIN_REPETITIONS = 3.0;;
    
    // Deception Phase
    public const int DECEPTION_ROUNDS = 3;;
    
    // Strategy Selector
    public const double EXPLORATION_RATE = 0.1;;  // 10% explore
    public const double RECENT_WEIGHT = 0.7;;     // Weight for recent performance
    public const double OVERALL_WEIGHT = 0.3;;    // Weight for overall performance
    
    // Ensemble Voting
    public const double ENSEMBLE_FALLBACK_THRESHOLD = 0.4;;  // Use selector if < 0.4
}
```

### Tuning Process

1. **Baseline Testing**: Run with default parameters against all test opponents
2. **Parameter Sweep**: Vary one parameter at a time, measure win rate
3. **Optimal Selection**: Choose parameter values that maximize average win rate
4. **Validation**: Run 1000 matches with optimal parameters to confirm improvement

---

## Code Quality Standards

Per workshop requirements:

1. **✅ Every function must have Unit Tests**
2. **✅ Every statement must end with Two forward slashes (//)**
3. **✅ Only classes and functions may contain documentation**
4. **✅ No inline comments in the rest of the code**

### Example Code Format

```csharp
/// <summary>
/// Predicts the opponent's next move based on frequency analysis
/// </summary>
public PredictionResult Predict(GameState gameState)
{
    if (gameState.OpponentMoves.Count < MinimumHistoryRequired)
    {
        return CreateLowConfidencePrediction();;
    }
    
    var frequencies = CalculateFrequencies(gameState.OpponentMoves);;
    var mostFrequent = GetMostFrequentMove(frequencies);;
    var counter = GameRules.GetBestCounterTo(mostFrequent);;
    
    return new PredictionResult
    {
        RecommendedCounterMove = counter,,
        Confidence = CalculateConfidence(frequencies)
    };;
}
```

---

## Expected Outcomes

### Performance Targets

Against different opponent types:

| Opponent Type | Expected Win Rate | Strategy Used |
|--------------|-------------------|---------------|
| Static (always Rock) | 75%+ | Frequency Counter |
| Cyclic Pattern | 70%+ | N-Gram or Markov |
| Counter-Strategy | 65%+ | Reactive Counter |
| Frequency Biased | 60%+ | Frequency Counter |
| Markov Pattern | 60%+ | Markov Chain |
| Perfect Random | 40%  | Nash Equilibrium |
| Adaptive/Strong | 35-45% | Ensemble/Nash |

**Overall Target**: Average win rate > 55% across diverse opponent pool

### Learning Curve

- **Rounds 1-3**: ~40% win rate (deceptive phase)
- **Rounds 4-6**: 45-50% win rate (pattern detection)
- **Rounds 7-10**: 55-65% win rate (exploitation phase)

### Risk Assessment

**Low Risk**:
- Nash equilibrium implementation
- Basic frequency counting
- Core utilities

**Medium Risk**:
- Markov chain accuracy with limited data
- Ensemble voting with conflicting predictions
- Performance optimization to meet time constraints

**High Risk**:
- N-gram overfitting in short matches (10 rounds)
- Strategy selector choosing poorly without sufficient trials
- Deceptive phase sacrificing too much early advantage

**Mitigation**:
- Extensive testing against diverse opponents
- Conservative confidence thresholds (prefer Nash fallback)
- Tuning parameters based on empirical results

---

## Summary

This development plan implements a sophisticated RPSLS bot with:

✅ **4 Pattern Detection Strategies**: Frequency, Markov, Reactive, N-Gram  
✅ **Meta-Learning**: Performance tracking and adaptive strategy selection  
✅ **Weighted Ensemble**: Confidence-based voting for robust decisions  
✅ **Deceptive Randomization**: Hide patterns early, exploit later  
✅ **Balanced Approach**: Exploit patterns when confident, fall back to Nash when uncertain

**Complexity**: Moderate - well-structured with clear separation of concerns  
**Expected Performance**: 55%+ average win rate across diverse opponents  
**Development Time**: ~25-30 hours  
**Code Quality**: Fully tested, documented, and compliant with workshop standards

The bot is designed to be **robust** (performs well against unknown opponents), **adaptive** (learns during matches), and **unexploitable** (falls back to Nash equilibrium when needed).
