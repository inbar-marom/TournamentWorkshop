# StrategicMind RPSLS Bot - Usage Guide

## Quick Start

### Basic Usage

```csharp
using UserBot.StrategicMind;
using UserBot.Core;

// Create the bot
var bot = new StrategicMindBot();

// Create game state for round 1
var gameState = new GameState
{
    GameType = GameType.RPSLS,
    RoundNumber = 1,
    TotalRounds = 10,
    OpponentMoveHistory = new List<string>(),
    MyMoveHistory = new List<string>()
};

// Get the bot's move
string myMove = await bot.MakeMove(gameState, CancellationToken.None);
// Returns: One of "Rock", "Paper", "Scissors", "Lizard", "Spock"
```

### Full Game Example

```csharp
var bot = new StrategicMindBot();
var opponentMoves = new List<string>();
var myMoves = new List<string>();

for (int round = 1; round <= 10; round++)
{
    // Create game state
    var gameState = new GameState
    {
        GameType = GameType.RPSLS,
        RoundNumber = round,
        TotalRounds = 10,
        OpponentMoveHistory = opponentMoves,
        MyMoveHistory = myMoves
    };
    
    // Get bot's move
    string myMove = await bot.MakeMove(gameState, CancellationToken.None);
    myMoves.Add(myMove);
    
    // Simulate opponent move (replace with actual opponent)
    string opponentMove = GetOpponentMove(); // Your opponent logic
    opponentMoves.Add(opponentMove);
    
    // Determine winner
    if (GameRules.Defeats(myMove, opponentMove))
    {
        Console.WriteLine($"Round {round}: Won! ({myMove} beats {opponentMove})");
    }
    else if (GameRules.Defeats(opponentMove, myMove))
    {
        Console.WriteLine($"Round {round}: Lost! ({opponentMove} beats {myMove})");
    }
    else
    {
        Console.WriteLine($"Round {round}: Draw! (both played {myMove})");
    }
}
```

## Strategy Behavior

### Rounds 1-2: Deceptive Phase
The bot uses Nash Equilibrium (pure random) to avoid revealing patterns:
```csharp
// Round 1-2: Appears completely random
// Prevents opponents from detecting our early game strategy
```

### Rounds 3+: Adaptive Phase
After gathering sufficient history, the bot:
1. Runs all applicable strategies (based on minimum history requirements)
2. Combines predictions using weighted ensemble voting
3. Learns which strategies work best against this opponent
4. Adapts strategy selection based on performance

## Understanding Strategy Selection

```
Round 1-2: Nash Equilibrium only
Round 3-4: Nash + Frequency Counter
Round 5+:   Nash + Frequency + Markov + Reactive + N-Gram

The bot automatically:
- Filters strategies by confidence
- Weights predictions by confidence scores
- Tracks which strategies succeed
- Favors successful strategies (90% exploitation, 10% exploration)
```

## Testing Against Different Opponents

### 1. Always-Rock Opponent
```csharp
// Frequency Counter will detect the bias quickly
// Expected: 60-80% win rate after round 3
```

### 2. Cyclic Opponent (Rock → Paper → Scissors → repeat)
```csharp
// Markov Chain and N-Gram will detect the pattern
// Expected: 55-70% win rate after round 5-6
```

### 3. Reactive Opponent (counters our previous move)
```csharp
// Reactive Counter strategy will detect and exploit
// Expected: 65-75% win rate after round 4-5
```

### 4. Random Opponent
```csharp
// Falls back to Nash Equilibrium
// Expected: ~40% win rate (theoretical baseline)
```

## Advanced: Accessing Individual Strategies

```csharp
using UserBot.StrategicMind.Strategies;
using UserBot.StrategicMind.Core;

// Create individual strategies
var nashStrategy = new NashEquilibriumStrategy();
var freqStrategy = new FrequencyCounterStrategy();
var markovStrategy = new MarkovChainStrategy();

// Get predictions from each
var nashPrediction = nashStrategy.Predict(gameState);
var freqPrediction = freqStrategy.Predict(gameState);
var markovPrediction = markovStrategy.Predict(gameState);

// Examine confidence scores
Console.WriteLine($"Nash confidence: {nashPrediction.Confidence:P0}");
Console.WriteLine($"Frequency confidence: {freqPrediction.Confidence:P0}");
Console.WriteLine($"Markov confidence: {markovPrediction.Confidence:P0}");
```

## Performance Characteristics

- **Response Time**: < 5ms (well within 300ms requirement)
- **Memory Usage**: Minimal (only stores move history)
- **CPU Usage**: Low (simple pattern matching algorithms)
- **Accuracy**: Adapts to opponent within 3-5 rounds

## Bot Properties

```csharp
var bot = new StrategicMindBot();

Console.WriteLine(bot.TeamName); // "StrategicMind"
Console.WriteLine(bot.GameType); // GameType.RPSLS

// Note: Other game methods throw NotImplementedException
// This bot is specialized for RPSLS only
```

## Troubleshooting

### Bot Always Returns Same Move
- Check that you're updating `OpponentMoveHistory` and `MyMoveHistory` after each round
- Ensure `RoundNumber` is incrementing

### Poor Performance Against Random Opponent
- This is expected! Random opponents have no exploitable patterns
- Bot will achieve ~40% win rate (theoretical baseline)

### Bot Takes Too Long
- Our implementation is < 5ms per move
- If experiencing delays, check your opponent move retrieval logic

## Integration with Tournament System

The bot implements the `IBot` interface and can be directly used with the tournament system:

```csharp
// The bot is ready for submission to the tournament!
// It will automatically adapt to any RPSLS opponent
```

## Further Reading

- [Plan RPSLS](plan-rpsls.md): Detailed strategic planning document
- [RPSLS Skill](.github/skills/rpsls/SKILL.md): Complete strategic analysis
- [Implementation Summary](RPSLS-IMPLEMENTATION-SUMMARY.md): Architecture and testing overview
