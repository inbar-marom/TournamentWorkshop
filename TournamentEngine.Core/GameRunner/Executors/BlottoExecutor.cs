namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;

/// <summary>
/// Executor for Colonel Blotto game
/// </summary>
public class BlottoExecutor : IGameExecutor
{
    private const int BattlefieldCount = 5;
    private const int TotalTroops = 100;

    public GameType GameType => GameType.ColonelBlotto;

    public async Task<MatchResult> Execute(IBot bot1, IBot bot2, TournamentConfig config, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var matchLog = new List<string>();
        var errors = new List<string>();
        
        matchLog.Add($"=== Colonel Blotto Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Battlefields: {BattlefieldCount}, Total Troops: {TotalTroops}");
        matchLog.Add("");

        var gameState = CreateGameState();
        
        // Get troop allocations from both bots with timeout
        var (allocation1, error1) = await GetBotAllocationWithTimeout(bot1, gameState, config.MoveTimeout, cancellationToken);
        var (allocation2, error2) = await GetBotAllocationWithTimeout(bot2, gameState, config.MoveTimeout, cancellationToken);
        
        int bot1Errors = 0;
        int bot2Errors = 0;
        
        // Track errors
        if (error1 != null)
        {
            bot1Errors++;
            errors.Add($"{bot1.TeamName} - {error1}");
            matchLog.Add($"{bot1.TeamName} ERROR - {error1}");
        }
        
        if (error2 != null)
        {
            bot2Errors++;
            errors.Add($"{bot2.TeamName} - {error2}");
            matchLog.Add($"{bot2.TeamName} ERROR - {error2}");
        }
        
        // Validate allocations
        bool valid1 = allocation1 != null && IsValidAllocation(allocation1);
        bool valid2 = allocation2 != null && IsValidAllocation(allocation2);
        
        if (!valid1 && error1 == null)
        {
            var invalidError = allocation1 == null 
                ? "Null allocation" 
                : $"Invalid allocation: [{string.Join(", ", allocation1)}] (must be 5 ints, sum=100, all =0)";
            bot1Errors++;
            errors.Add($"{bot1.TeamName} - {invalidError}");
            matchLog.Add($"{bot1.TeamName} - {invalidError}");
        }
        
        if (!valid2 && error2 == null)
        {
            var invalidError = allocation2 == null 
                ? "Null allocation" 
                : $"Invalid allocation: [{string.Join(", ", allocation2)}] (must be 5 ints, sum=100, all =0)";
            bot2Errors++;
            errors.Add($"{bot2.TeamName} - {invalidError}");
            matchLog.Add($"{bot2.TeamName} - {invalidError}");
        }
        
        // Compute battlefield results
        int bot1BattlefieldsWon = 0;
        int bot2BattlefieldsWon = 0;
        
        if (valid1 && valid2)
        {
            matchLog.Add($"{bot1.TeamName} allocation: [{string.Join(", ", allocation1!)}]");
            matchLog.Add($"{bot2.TeamName} allocation: [{string.Join(", ", allocation2!)}]");
            matchLog.Add("");
            
            for (int i = 0; i < BattlefieldCount; i++)
            {
                var troops1 = allocation1![i];
                var troops2 = allocation2![i];
                
                if (troops1 > troops2)
                {
                    bot1BattlefieldsWon++;
                    matchLog.Add($"Battlefield {i + 1}: {bot1.TeamName} wins ({troops1} vs {troops2})");
                }
                else if (troops2 > troops1)
                {
                    bot2BattlefieldsWon++;
                    matchLog.Add($"Battlefield {i + 1}: {bot2.TeamName} wins ({troops2} vs {troops1})");
                }
                else
                {
                    matchLog.Add($"Battlefield {i + 1}: Draw ({troops1} vs {troops2})");
                }
            }
        }
        else if (valid1 && !valid2)
        {
            bot1BattlefieldsWon = BattlefieldCount;
            matchLog.Add($"{bot1.TeamName} wins all battlefields by default (opponent error)");
        }
        else if (!valid1 && valid2)
        {
            bot2BattlefieldsWon = BattlefieldCount;
            matchLog.Add($"{bot2.TeamName} wins all battlefields by default (opponent error)");
        }
        else
        {
            // Both invalid - deterministic random winner
            var randomWinner = (bot1.TeamName.GetHashCode() % 2 == 0) ? 1 : 2;
            if (randomWinner == 1)
            {
                bot1BattlefieldsWon = BattlefieldCount;
                matchLog.Add($"Both errors - {bot1.TeamName} wins (deterministic random)");
            }
            else
            {
                bot2BattlefieldsWon = BattlefieldCount;
                matchLog.Add($"Both errors - {bot2.TeamName} wins (deterministic random)");
            }
        }
        
        // Determine final outcome
        var outcome = DetermineOutcome(bot1BattlefieldsWon, bot2BattlefieldsWon, bot1Errors, bot2Errors);
        var winnerName = outcome == MatchOutcome.Player1Wins ? bot1.TeamName :
                        outcome == MatchOutcome.Player2Wins ? bot2.TeamName : null;
        
        var endTime = DateTime.Now;
        matchLog.Add("");
        matchLog.Add($"=== Final Result ===");
        matchLog.Add($"Winner: {winnerName ?? "Draw"}");
        matchLog.Add($"Battlefields Won: {bot1.TeamName} {bot1BattlefieldsWon} - {bot2BattlefieldsWon} {bot2.TeamName}");
        matchLog.Add($"Duration: {endTime - startTime}");
        
        return new MatchResult
        {
            Bot1Name = bot1.TeamName,
            Bot2Name = bot2.TeamName,
            GameType = GameType.ColonelBlotto,
            Outcome = outcome,
            WinnerName = winnerName,
            Bot1Score = bot1BattlefieldsWon,
            Bot2Score = bot2BattlefieldsWon,
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime,
            MatchLog = matchLog,
            Errors = errors
        };
    }

    private static GameState CreateGameState()
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.ColonelBlotto,
                ["BattlefieldCount"] = BattlefieldCount,
                ["TotalTroops"] = TotalTroops
            },
            MoveHistory = new List<string>(),
            CurrentRound = 1,
            MaxRounds = 1,
            IsGameOver = false,
            Winner = null
        };
    }

    private static async Task<(int[]? allocation, string? error)> GetBotAllocationWithTimeout(
        IBot bot, 
        GameState gameState, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            var allocation = await bot.AllocateTroops(gameState, cts.Token);
            return (allocation, null);
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

    private static bool IsValidAllocation(int[] allocation)
    {
        if (allocation == null || allocation.Length != BattlefieldCount)
            return false;
        
        if (allocation.Any(x => x < 0))
            return false;
        
        return allocation.Sum() == TotalTroops;
    }

    private static MatchOutcome DetermineOutcome(int bot1Battlefields, int bot2Battlefields, int bot1Errors, int bot2Errors)
    {
        // Check for error-based outcomes
        if (bot1Errors > 0 && bot2Errors > 0)
            return bot1Battlefields > bot2Battlefields ? MatchOutcome.Player1Wins : 
                   bot2Battlefields > bot1Battlefields ? MatchOutcome.Player2Wins : MatchOutcome.BothError;
        
        if (bot1Errors > 0 && bot2Errors == 0)
            return MatchOutcome.Player2Wins;
        
        if (bot2Errors > 0 && bot1Errors == 0)
            return MatchOutcome.Player1Wins;
        
        // Normal scoring outcome
        if (bot1Battlefields > bot2Battlefields)
            return MatchOutcome.Player1Wins;
        else if (bot2Battlefields > bot1Battlefields)
            return MatchOutcome.Player2Wins;
        else
            return MatchOutcome.Draw;
    }
}
