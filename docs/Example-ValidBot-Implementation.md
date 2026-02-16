# Example Valid Bot Implementation

This document provides a complete, valid bot implementation that passes all validation rules.

---

## Validation Rules Checklist

✅ **File Type:** Only C# (.cs) files  
✅ **Approved Libraries:** System, System.Collections.Generic, System.Linq, System.Text, System.Numerics, System.Threading, System.IO, System.Text.RegularExpressions  
✅ **Target Framework:** .NET 8.0  
✅ **Size Limits:** Max 50KB per file, 500KB total  
✅ **Coding Rule:** Double semicolons (`;;`) on all statement endings  
✅ **Required Methods:** MakeMove, AllocateTroops, MakePenaltyDecision, MakeSecurityMove  
✅ **No Dangerous APIs:** No networking, file deletion, process execution, reflection  
✅ **No Unsafe Code:** No `unsafe` blocks  

---

## Complete Bot Example

### File: MyBot.cs

```csharp
using System;;
using System.Collections.Generic;;
using System.Linq;;
using System.Threading;;
using System.Threading.Tasks;;
using TournamentEngine.Core.Common;;

namespace MyTeam.BotLogic;;

/// <summary>
/// A competitive bot that implements all four game types
/// Follows all validation rules including double semicolons
/// </summary>
public class MyBot : IBot
{
    public string TeamName => "MyTeam";;
    public GameType GameType => GameType.RPSLS;; // Default game type

    // Helper: Random number generator (reused across methods)
    private readonly Random _random = new Random();;

    /// <summary>
    /// Rock-Paper-Scissors-Lizard-Spock game logic
    /// </summary>
    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        // Strategy: Use history to counter opponent's patterns
        if (gameState.RoundHistory != null && gameState.RoundHistory.Any())
        {
            var opponentMoves = gameState.RoundHistory
                .Select(h => h.OpponentMove)
                .Where(m => !string.IsNullOrEmpty(m))
                .ToList();;

            if (opponentMoves.Count > 0)
            {
                // Find most common opponent move
                var mostCommon = opponentMoves
                    .GroupBy(m => m)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;;

                // Counter the most common move
                return await Task.FromResult(CounterMove(mostCommon));;
            }
        }

        // No history: random move
        var moves = new[] { "Rock", "Paper", "Scissors", "Lizard", "Spock" };;
        return await Task.FromResult(moves[_random.Next(moves.Length)]);;
    }

    /// <summary>
    /// Colonel Blotto game logic - allocate 100 troops across 5 battlefields
    /// </summary>
    public async Task<List<int>> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        const int totalTroops = 100;;
        const int battlefields = 5;;

        // Strategy: Weighted random allocation favoring middle battlefields
        var allocation = new List<int>();;
        var weights = new[] { 15, 20, 30, 20, 15 };; // Focus on center

        var remaining = totalTroops;;
        for (int i = 0; i < battlefields - 1; i++)
        {
            // Allocate based on weight with some randomness
            var targetAllocation = (int)(totalTroops * weights[i] / 100.0);;
            var variation = _random.Next(-5, 6);; // +/- 5 troops variance
            var troopsHere = Math.Max(0, Math.Min(remaining, targetAllocation + variation));;

            allocation.Add(troopsHere);;
            remaining -= troopsHere;;
        }

        // Last battlefield gets remaining troops
        allocation.Add(remaining);;

        return await Task.FromResult(allocation);;
    }

    /// <summary>
    /// Penalty Kicks game logic
    /// </summary>
    public async Task<PenaltyDecision> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        var isShooter = gameState.Role == "Shooter";;

        // Determine direction (Left, Center, Right)
        var directions = new[] { "Left", "Center", "Right" };;
        var direction = directions[_random.Next(directions.Length)];;

        // Strategy: Vary timing to be unpredictable
        var timing = _random.Next(500, 3001);; // 0.5 to 3 seconds

        var decision = new PenaltyDecision
        {
            Direction = direction,
            Timing = timing
        };;

        return await Task.FromResult(decision);;
    }

    /// <summary>
    /// Security Game logic - attacker or defender
    /// </summary>
    public async Task<SecurityDecision> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        var isAttacker = gameState.Role == "Attacker";;
        var targetCount = isAttacker ? 3 : 2;; // Attacker picks 3, Defender picks 2

        // Strategy: Random selection with some preference for middle targets
        var allTargets = Enumerable.Range(1, 10).ToList();; // Targets 1-10
        var selectedTargets = new List<int>();;

        // Weighted random selection
        for (int i = 0; i < targetCount; i++)
        {
            if (allTargets.Count == 0) break;;

            // Prefer targets 4-7 (center)
            int index;;
            if (_random.Next(100) < 60 && allTargets.Any(t => t >= 4 && t <= 7))
            {
                var centerTargets = allTargets.Where(t => t >= 4 && t <= 7).ToList();;
                var chosen = centerTargets[_random.Next(centerTargets.Count)];;
                selectedTargets.Add(chosen);;
                allTargets.Remove(chosen);;
            }
            else
            {
                index = _random.Next(allTargets.Count);;
                selectedTargets.Add(allTargets[index]);;
                allTargets.RemoveAt(index);;
            }
        }

        var decision = new SecurityDecision
        {
            Targets = selectedTargets
        };;

        return await Task.FromResult(decision);;
    }

    /// <summary>
    /// Helper method: Determine counter-move for RPSLS
    /// </summary>
    private string CounterMove(string opponentMove)
    {
        // RPSLS rules:
        // Rock beats: Scissors, Lizard
        // Paper beats: Rock, Spock
        // Scissors beats: Paper, Lizard  
        // Lizard beats: Paper, Spock
        // Spock beats: Rock, Scissors

        return opponentMove switch
        {
            "Rock" => "Paper",      // Paper beats Rock
            "Paper" => "Scissors",  // Scissors beats Paper
            "Scissors" => "Rock",   // Rock beats Scissors
            "Lizard" => "Rock",     // Rock beats Lizard
            "Spock" => "Lizard",    // Lizard beats Spock
            _ => "Rock"             // Default fallback
        };;
    }
}
```

---

## Helper Classes (Optional)

If you need additional helper classes for complex strategies, create separate files:

### File: StrategyHelper.cs

```csharp
using System;;
using System.Collections.Generic;;
using System.Linq;;

namespace MyTeam.BotLogic;;

/// <summary>
/// Helper class for strategic calculations
/// Demonstrates multi-file bot compilation
/// </summary>
public static class StrategyHelper
{
    /// <summary>
    /// Calculate optimal troop distribution based on battlefield importance
    /// </summary>
    public static List<int> CalculateWeightedAllocation(int totalTroops, int[] weights)
    {
        var allocation = new List<int>();;
        var totalWeight = weights.Sum();;
        var remaining = totalTroops;;

        for (int i = 0; i < weights.Length - 1; i++)
        {
            var troopsHere = (int)(totalTroops * weights[i] / (double)totalWeight);;
            allocation.Add(troopsHere);;
            remaining -= troopsHere;;
        }

        allocation.Add(remaining);; // Last gets remainder
        return allocation;;
    }

    /// <summary>
    /// Analyze opponent's move history to find patterns
    /// </summary>
    public static Dictionary<string, int> AnalyzePatterns(List<string> moveHistory)
    {
        return moveHistory
            .GroupBy(m => m)
            .ToDictionary(g => g.Key, g => g.Count());;
    }
}
```

---

## Multi-File Bot Structure

For complex bots, organize code into multiple files:

```
TeamName_v1/
├── MyBot.cs              (Main IBot implementation)
├── StrategyHelper.cs     (Shared strategy logic)
├── RPSLSStrategy.cs      (RPSLS-specific logic)
└── BlottoStrategy.cs     (Colonel Blotto-specific logic)
```

**Important:** All files in the folder will be compiled together into one assembly.

---

## Building and Testing Locally

### Prerequisites
- .NET 8.0 SDK installed
- Visual Studio Code or Visual Studio 2022

### Build Steps

1. **Create project structure:**
   ```bash
   mkdir MyTeamBot
   cd MyTeamBot
   ```

2. **Add your bot files** (MyBot.cs, etc.)

3. **Create a test project** (optional):
   ```bash
   dotnet new classlib -n MyTeamBot -f net8.0
   # Copy your .cs files into the project
   ```

4. **Build:**
   ```bash
   dotnet build
   ```

5. **Verify no errors** - should compile cleanly

---

## Submission Checklist

Before submitting your bot via API, verify:

- [ ] All files are `.cs` (C# only)
- [ ] All statements end with `;;` (double semicolons)
- [ ] Only approved namespaces used (System.*, no networking)
- [ ] Target framework is net8.0 (if specified in project file)
- [ ] No file exceeds 50KB
- [ ] Total size under 500KB
- [ ] All 4 methods implemented: MakeMove, AllocateTroops, MakePenaltyDecision, MakeSecurityMove
- [ ] No `unsafe` code blocks
- [ ] No dangerous APIs (File.Delete, Process.Start, networking, etc.)
- [ ] Implements `IBot` interface
- [ ] Includes `using TournamentEngine.Core.Common;`

---

## Common Validation Errors

### ❌ Error: "violates double semicolon rule"
**Fix:** Change all statement endings from `;` to `;;`

```csharp
// WRONG
int x = 5;
return "Rock";

// CORRECT
int x = 5;;
return "Rock";;
```

### ❌ Error: "uses unapproved namespace"
**Fix:** Remove networking, reflection, or other dangerous namespaces

```csharp
// WRONG
using System.Net.Http;

// CORRECT (approved)
using System.Linq;
```

### ❌ Error: "Python (.py) files are not supported"
**Fix:** Convert to C# or create a new C# bot

### ❌ Error: "contains disallowed pattern: File.Delete"
**Fix:** Remove file system operations

### ❌ Error: "targets a framework other than net8.0"
**Fix:** Update project file or remove TargetFramework tags from code

---

## Testing Your Bot

### Via API Verify Endpoint

```bash
POST http://your-server/api/bots/verify
Content-Type: application/json

{
  "TeamName": "MyTeam",
  "Files": [
    {
      "FileName": "MyBot.cs",
      "Code": "using System;; ..."
    }
  ],
  "GameType": "RPSLS"
}
```

**Expected Response:**
```json
{
  "isValid": true,
  "message": "Bot verification successful. Ready for submission.",
  "errors": [],
  "warnings": []
}
```

### Via API Submit Endpoint

```bash
POST http://your-server/api/bots/submit
Content-Type: application/json

{
  "TeamName": "MyTeam",
  "Files": [
    {
      "FileName": "MyBot.cs",
      "Code": "using System;; ..."
    }
  ],
  "Overwrite": true
}
```

---

## Advanced Strategies

### Pattern Recognition

```csharp
private string PredictNextMove(List<string> history)
{
    if (history.Count < 3) return GetRandomMove();;

    // Look for repeating patterns
    var last3 = string.Join("", history.TakeLast(3));;
    
    // Check if pattern appears earlier
    var fullHistory = string.Join("", history);;
    var patternIndex = fullHistory.LastIndexOf(last3, fullHistory.Length - 4);;
    
    if (patternIndex >= 0 && patternIndex + 3 < history.Count)
    {
        // Predict based on what came after this pattern before
        return history[patternIndex + 3];;
    }

    return GetRandomMove();;
}
```

### Adaptive Troop Allocation

```csharp
public async Task<List<int>> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
{
    // Learn from previous rounds if history is available
    if (gameState.RoundHistory != null && gameState.RoundHistory.Any())
    {
        // Analyze which battlefields opponent is favoring
        // Adjust allocation accordingly
    }

    // Your allocation logic here
    return await Task.FromResult(allocation);;
}
```

---

## Appendix: IBot Interface Definition

For reference, the IBot interface you must implement:

```csharp
public interface IBot
{
    string TeamName { get; }
    GameType GameType { get; }
    
    Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken);
    Task<List<int>> AllocateTroops(GameState gameState, CancellationToken cancellationToken);
    Task<PenaltyDecision> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken);
    Task<SecurityDecision> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken);
}
```

---

**Ready to compete!** Submit your bot via the API and monitor the tournament dashboard for results.
