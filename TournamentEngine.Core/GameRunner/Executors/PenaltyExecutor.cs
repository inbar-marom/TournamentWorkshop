namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;


/// <summary>
/// Executor for Penalty Kicks game (minimal implementation)
/// </summary>
public class PenaltyExecutor : IGameExecutor
{
    private static readonly string[] ValidDecisions = { "Left", "Right" };
    private const int MaxRounds = 10;

    public GameType GameType => GameType.PenaltyKicks;

    public async Task<MatchResult> Execute(IBot bot1, IBot bot2, TournamentConfig config, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var matchLog = new List<string>();
        var errors = new List<string>();
        
        int bot1Score = 0;
        int bot2Score = 0;
        int bot1Errors = 0;
        int bot2Errors = 0;
        
        matchLog.Add($"=== Penalty Kicks Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Max Rounds: {MaxRounds}");
        matchLog.Add("");

        // Execute rounds (simplified: both bots make decisions, score if they differ)
        for (int round = 1; round <= MaxRounds; round++)
        {
            var gameState = CreateGameState(round, MaxRounds);
            
            var (decision1, error1) = await GetBotDecisionWithTimeout(bot1, gameState, config.MoveTimeout, cancellationToken);
            var (decision2, error2) = await GetBotDecisionWithTimeout(bot2, gameState, config.MoveTimeout, cancellationToken);
            
            if (error1 != null)
            {
                bot1Errors++;
                errors.Add($"Round {round}: {bot1.TeamName} - {error1}");
                matchLog.Add($"Round {round}: {bot1.TeamName} ERROR - {error1}");
            }
            
            if (error2 != null)
            {
                bot2Errors++;
                errors.Add($"Round {round}: {bot2.TeamName} - {error2}");
                matchLog.Add($"Round {round}: {bot2.TeamName} ERROR - {error2}");
            }
            
            bool valid1 = decision1 != null && IsValidDecision(decision1);
            bool valid2 = decision2 != null && IsValidDecision(decision2);
            
            if (!valid1 && error1 == null)
            {
                var invalidError = $"Invalid decision: '{decision1}'";
                bot1Errors++;
                errors.Add($"Round {round}: {bot1.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {bot1.TeamName} - {invalidError}");
            }
            
            if (!valid2 && error2 == null)
            {
                var invalidError = $"Invalid decision: '{decision2}'";
                bot2Errors++;
                errors.Add($"Round {round}: {bot2.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {bot2.TeamName} - {invalidError}");
            }
            
            // Simplified scoring: if both valid and different, bot1 scores
            if (valid1 && valid2)
            {
                if (decision1 != decision2)
                {
                    bot1Score++;
                    matchLog.Add($"Round {round}: {bot1.TeamName} scores ({decision1} != {decision2}) - Score: {bot1Score}-{bot2Score}");
                }
                else
                {
                    matchLog.Add($"Round {round}: No score ({decision1} == {decision2}) - Score: {bot1Score}-{bot2Score}");
                }
            }
            else if (valid1 && !valid2)
            {
                bot1Score++;
                matchLog.Add($"Round {round}: {bot1.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
            }
            else if (!valid1 && valid2)
            {
                bot2Score++;
                matchLog.Add($"Round {round}: {bot2.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
            }
        }
        
        var outcome = DetermineOutcome(bot1Score, bot2Score, bot1Errors, bot2Errors);
        var winnerName = outcome == MatchOutcome.Player1Wins ? bot1.TeamName :
                        outcome == MatchOutcome.Player2Wins ? bot2.TeamName : null;
        
        var endTime = DateTime.Now;
        matchLog.Add("");
        matchLog.Add($"=== Final Result ===");
        matchLog.Add($"Winner: {winnerName ?? "Draw"}");
        matchLog.Add($"Final Score: {bot1.TeamName} {bot1Score} - {bot2Score} {bot2.TeamName}");
        matchLog.Add($"Duration: {endTime - startTime}");
        
        return new MatchResult
        {
            Bot1Name = bot1.TeamName,
            Bot2Name = bot2.TeamName,
            GameType = GameType.PenaltyKicks,
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

    private static GameState CreateGameState(int currentRound = 1, int maxRounds = MaxRounds)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.PenaltyKicks
            },
            MoveHistory = new List<string>(),
            CurrentRound = currentRound,
            MaxRounds = maxRounds,
            IsGameOver = false,
            Winner = null
        };
    }

    private static async Task<(string? decision, string? error)> GetBotDecisionWithTimeout(
        IBot bot, 
        GameState gameState, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            var decision = await bot.MakePenaltyDecision(gameState, cts.Token);
            return (decision, null);
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

    private static bool IsValidDecision(string decision)
    {
        return ValidDecisions.Contains(decision);
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
            return MatchOutcome.Draw;
    }
}

