namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;


/// <summary>
/// Executor for Security Game - Attacker vs Defender resource allocation game
/// One bot is attacker for all rounds, other is defender for all rounds
/// </summary>
public class SecurityExecutor : IGameExecutor
{
    private const int TotalRounds = 5;
    private const int MaxRoundsPerMatch = 10;
    private const int NumberOfTargets = 3;
    private static readonly int[] TargetValues = [10, 20, 30]; // Fixed target values
    private const int TotalDefenseUnits = 30;
    private static readonly Random _random = new();

    public GameType GameType => GameType.SecurityGame;

    public async Task<MatchResult> Execute(IBot bot1, IBot bot2, TournamentConfig config, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var matchLog = new List<string>();
        var errors = new List<string>();
        
        int bot1Score = 0;
        int bot2Score = 0;
        int bot1Errors = 0;
        int bot2Errors = 0;
        const int maxRounds = TotalRounds; // Security Game is always exactly 5 rounds
        
        // Randomly assign roles - one is attacker for all rounds, other is defender
        bool bot1IsAttacker = _random.Next(2) == 0;
        var attackerBot = bot1IsAttacker ? bot1 : bot2;
        var defenderBot = bot1IsAttacker ? bot2 : bot1;
        
        matchLog.Add($"=== Security Game Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Attacker: {attackerBot.TeamName}, Defender: {defenderBot.TeamName}");
        matchLog.Add($"Targets: {NumberOfTargets} targets with values [{string.Join(",", TargetValues)}] = {TargetValues.Sum()} points total");
        matchLog.Add($"Defense: {TotalDefenseUnits} units total");
        matchLog.Add($"Max Rounds: {maxRounds}");
        matchLog.Add("");

        // Track round history for bots
        var attackerHistory = new List<RoundHistory>();
        var defenderHistory = new List<RoundHistory>();

        // Execute rounds
        for (int round = 1; round <= maxRounds; round++)
        {
            var attackerState = CreateGameState(round, maxRounds, attackerHistory, "Attacker", TargetValues);
            var defenderState = CreateGameState(round, maxRounds, defenderHistory, "Defender", TargetValues);
            
            var (attackerMove, attackerError) = await GetBotMoveWithTimeout(attackerBot, attackerState, config.MoveTimeout, cancellationToken);
            var (defenderMove, defenderError) = await GetBotMoveWithTimeout(defenderBot, defenderState, config.MoveTimeout, cancellationToken);
            
            // Track errors
            if (attackerError != null)
            {
                if (bot1IsAttacker) bot1Errors++; else bot2Errors++;
                errors.Add($"Round {round}: {attackerBot.TeamName} (Attacker) - {attackerError}");
                matchLog.Add($"Round {round}: {attackerBot.TeamName} (Attacker) ERROR - {attackerError}");
            }
            
            if (defenderError != null)
            {
                if (bot1IsAttacker) bot2Errors++; else bot1Errors++;
                errors.Add($"Round {round}: {defenderBot.TeamName} (Defender) - {defenderError}");
                matchLog.Add($"Round {round}: {defenderBot.TeamName} (Defender) ERROR - {defenderError}");
            }
            
            // Validate moves
            var (attackTargetValid, attackTarget) = ValidateAttackerMove(attackerMove);
            var (defenseValid, defenseAllocation, defenseError) = ValidateDefenderMove(defenderMove);
            
            if (!attackTargetValid && attackerError == null)
            {
                var invalidError = $"Invalid attacker move: '{attackerMove}' (expected target index 0-{NumberOfTargets - 1})";
                if (bot1IsAttacker) bot1Errors++; else bot2Errors++;
                errors.Add($"Round {round}: {attackerBot.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {attackerBot.TeamName} - {invalidError}");
            }
            
            if (!defenseValid && defenderError == null)
            {
                var invalidError = defenseError ?? $"Invalid defender move: '{defenderMove}' (expected comma-separated allocation summing to {TotalDefenseUnits})";
                if (bot1IsAttacker) bot2Errors++; else bot1Errors++;
                errors.Add($"Round {round}: {defenderBot.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {defenderBot.TeamName} - {invalidError}");
            }
            
            // Calculate scores for this round
            int attackerRoundScore = 0;
            int defenderRoundScore = 0;
            
            if (attackTargetValid && defenseValid)
            {
                var targetValue = TargetValues[attackTarget];
                var defense = defenseAllocation[attackTarget];
                
                if (defense == 0)
                {
                    // No defense: attacker gets full value
                    attackerRoundScore = targetValue;
                    defenderRoundScore = 0;
                }
                else if (defense < targetValue)
                {
                    // Partial defense: split the value
                    attackerRoundScore = targetValue - defense;
                    defenderRoundScore = defense;
                }
                else
                {
                    // Full defense: defender gets full value
                    attackerRoundScore = 0;
                    defenderRoundScore = targetValue;
                }
                
                matchLog.Add($"Round {round}: Target values [{string.Join(",", TargetValues)}]");
                matchLog.Add($"  Attack target {attackTarget} (value {targetValue}), Defense {defense}");
                matchLog.Add($"  {attackerBot.TeamName} gets {attackerRoundScore}, {defenderBot.TeamName} gets {defenderRoundScore}");
                matchLog.Add($"  Defense allocation: [{string.Join(",", defenseAllocation)}]");
            }
            else if (attackTargetValid && !defenseValid)
            {
                // Defender error: attacker gets full value of target
                attackerRoundScore = TargetValues[attackTarget];
                defenderRoundScore = 0;
                matchLog.Add($"Round {round}: Target values [{string.Join(",", TargetValues)}]");
                matchLog.Add($"  {attackerBot.TeamName} scores by default (defender error) - {attackerRoundScore} points");
            }
            else if (!attackTargetValid && defenseValid)
            {
                // Attacker error: defender gets sum of all target values
                defenderRoundScore = TargetValues.Sum();
                attackerRoundScore = 0;
                matchLog.Add($"Round {round}: Target values [{string.Join(",", TargetValues)}]");
                matchLog.Add($"  {defenderBot.TeamName} scores by default (attacker error) - {defenderRoundScore} points");
            }
            else
            {
                // Both invalid: no points
                matchLog.Add($"Round {round}: Target values [{string.Join(",", TargetValues)}]");
                matchLog.Add($"  No score (both errors)");
            }
            
            // Add to cumulative scores
            if (bot1IsAttacker)
            {
                bot1Score += attackerRoundScore;
                bot2Score += defenderRoundScore;
            }
            else
            {
                bot2Score += attackerRoundScore;
                bot1Score += defenderRoundScore;
            }
            
            matchLog.Add($"  Score after round {round}: {bot1.TeamName} {bot1Score} - {bot2Score} {bot2.TeamName}");
            matchLog.Add("");
            
            // Record round history
            attackerHistory.Add(new RoundHistory
            {
                Round = round,
                MyMove = attackerMove ?? "ERROR",
                OpponentMove = defenderMove ?? "ERROR",
                Result = attackerRoundScore > defenderRoundScore ? "Win" : 
                         attackerRoundScore < defenderRoundScore ? "Loss" : "Draw",
                Role = "Attacker"
            });
            
            defenderHistory.Add(new RoundHistory
            {
                Round = round,
                MyMove = defenderMove ?? "ERROR",
                OpponentMove = attackerMove ?? "ERROR",
                Result = defenderRoundScore > attackerRoundScore ? "Win" : 
                         defenderRoundScore < attackerRoundScore ? "Loss" : "Draw",
                Role = "Defender"
            });
        }
        
        var outcome = DetermineOutcome(bot1Score, bot2Score, bot1Errors, bot2Errors);
        var winnerName = outcome == MatchOutcome.Player1Wins ? bot1.TeamName :
                        outcome == MatchOutcome.Player2Wins ? bot2.TeamName : null;
        
        var endTime = DateTime.Now;
        matchLog.Add($"=== Final Result ===");
        matchLog.Add($"Winner: {winnerName ?? "Draw"}");
        matchLog.Add($"Final Score: {bot1.TeamName} {bot1Score} - {bot2Score} {bot2.TeamName}");
        matchLog.Add($"Duration: {endTime - startTime}");
        
        return new MatchResult
        {
            Bot1Name = bot1.TeamName,
            Bot2Name = bot2.TeamName,
            GameType = GameType.SecurityGame,
            Outcome = outcome,
            WinnerName = winnerName,
            Bot1Score = bot1Score,
            Bot2Score = bot2Score,
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime,
            MatchLog = matchLog,
            Errors = errors
        };
    }

    private static GameState CreateGameState(int currentRound, int maxRounds, List<RoundHistory> roundHistory, string role, int[] targetValues)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.SecurityGame,
                ["Role"] = role,
                ["NumberOfTargets"] = NumberOfTargets,
                ["TargetValues"] = targetValues,
                ["TotalDefenseUnits"] = TotalDefenseUnits
            },
            MoveHistory = roundHistory.Select(r => r.MyMove ?? "").Where(m => !string.IsNullOrEmpty(m)).ToList(),
            CurrentRound = currentRound,
            MaxRounds = maxRounds,
            IsGameOver = false,
            Winner = null,
            RoundHistory = new List<RoundHistory>(roundHistory)
        };
    }

    private static async Task<(string? move, string? error)> GetBotMoveWithTimeout(
        IBot bot, 
        GameState gameState, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            var move = await bot.MakeSecurityMove(gameState, cts.Token);
            return (move, null);
        }
        catch (OperationCanceledException)
        {
            return (null, $"Timeout exceeded ({timeout.TotalMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            return (null, $"Exception: {ex.Message}");
        }
    }

    private static (bool valid, int targetIndex) ValidateAttackerMove(string? move)
    {
        if (string.IsNullOrWhiteSpace(move))
            return (false, -1);
        
        if (int.TryParse(move.Trim(), out int target) && target >= 0 && target < NumberOfTargets)
            return (true, target);
        
        return (false, -1);
    }

    private static (bool valid, int[] allocation, string? error) ValidateDefenderMove(string? move)
    {
        if (string.IsNullOrWhiteSpace(move))
            return (false, Array.Empty<int>(), null);
        
        try
        {
            var parts = move.Split(',');
            if (parts.Length != NumberOfTargets)
                return (false, Array.Empty<int>(), $"Expected {NumberOfTargets} values, got {parts.Length}");
            
            var allocation = new int[NumberOfTargets];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out int value) || value < 0)
                    return (false, Array.Empty<int>(), $"Invalid value at position {i}: '{parts[i]}'");
                
                allocation[i] = value;
            }
            
            // Verify sum equals total defense units
            if (allocation.Sum() != TotalDefenseUnits)
                return (false, Array.Empty<int>(), $"Total allocation {allocation.Sum()} must equal {TotalDefenseUnits}");
            
            return (true, allocation, null);
        }
        catch
        {
            return (false, Array.Empty<int>(), "Parse error");
        }
    }

    private static MatchOutcome DetermineOutcome(int bot1Score, int bot2Score, int bot1Errors, int bot2Errors)
    {
        if (bot1Errors > 0 && bot2Errors > 0)
            return bot1Score > bot2Score ? MatchOutcome.Player1Wins : 
                   bot2Score > bot1Score ? MatchOutcome.Player2Wins : MatchOutcome.BothError;
        
        if (bot1Errors > 0 && bot2Errors == 0)
            return MatchOutcome.Player2Wins;
        
        if (bot2Errors > 0 && bot1Errors == 0)
            return MatchOutcome.Player1Wins;
        
        if (bot1Score > bot2Score)
            return MatchOutcome.Player1Wins;
        else if (bot2Score > bot1Score)
            return MatchOutcome.Player2Wins;
        else
        {
            // Random tiebreaker to reduce draws (deterministic based on both names)
            var seed = bot1Score.GetHashCode() ^ bot2Score.GetHashCode();
            return (seed % 2) == 0 ? MatchOutcome.Player1Wins : MatchOutcome.Player2Wins;
        }
    }
}

