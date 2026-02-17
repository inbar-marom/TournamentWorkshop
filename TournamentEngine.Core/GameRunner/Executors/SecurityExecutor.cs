namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;


/// <summary>
/// Executor for Security vs Hacker game
/// Roles are randomly assigned at the start of each match.
/// </summary>
public class SecurityExecutor : IGameExecutor
{
    private static readonly string[] ValidMoves = { "Attack", "Defend" };
    private const int MaxRounds = 10;
    private static readonly Random _random = new Random();

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
        
        // Randomly decide initial attacking/defending preference (for variety)
        bool bot1Aggressive = _random.Next(2) == 0;
        
        matchLog.Add($"=== Security Game Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Max Rounds: {MaxRounds}");
        matchLog.Add("");

        // Track round history for bots
        var roundHistory = new List<RoundHistory>();

        // Execute rounds (simplified scoring)
        for (int round = 1; round <= MaxRounds; round++)
        {
            var gameState = CreateGameState(round, MaxRounds, roundHistory);
            
            var (move1, error1) = await GetBotMoveWithTimeout(bot1, gameState, config.MoveTimeout, cancellationToken);
            var (move2, error2) = await GetBotMoveWithTimeout(bot2, gameState, config.MoveTimeout, cancellationToken);
            
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
            
            bool valid1 = move1 != null && IsValidMove(move1);
            bool valid2 = move2 != null && IsValidMove(move2);
            
            if (!valid1 && error1 == null)
            {
                var invalidError = $"Invalid move: '{move1}'";
                bot1Errors++;
                errors.Add($"Round {round}: {bot1.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {bot1.TeamName} - {invalidError}");
            }
            
            if (!valid2 && error2 == null)
            {
                var invalidError = $"Invalid move: '{move2}'";
                bot2Errors++;
                errors.Add($"Round {round}: {bot2.TeamName} - {invalidError}");
                matchLog.Add($"Round {round}: {bot2.TeamName} - {invalidError}");
            }
            
            // Simplified scoring: Attack beats Defend
            string roundResult;
            if (valid1 && valid2)
            {
                if (move1 == "Attack" && move2 == "Defend")
                {
                    bot1Score++;
                    matchLog.Add($"Round {round}: {bot1.TeamName} (Attack) beats {bot2.TeamName} (Defend) - Score: {bot1Score}-{bot2Score}");
                    roundResult = "Win";
                }
                else if (move2 == "Attack" && move1 == "Defend")
                {
                    bot2Score++;
                    matchLog.Add($"Round {round}: {bot2.TeamName} (Attack) beats {bot1.TeamName} (Defend) - Score: {bot1Score}-{bot2Score}");
                    roundResult = "Loss";
                }
                else
                {
                    matchLog.Add($"Round {round}: No score ({move1} vs {move2}) - Score: {bot1Score}-{bot2Score}");
                    roundResult = "Draw";
                }
            }
            else if (valid1 && !valid2)
            {
                bot1Score++;
                matchLog.Add($"Round {round}: {bot1.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
                roundResult = "Win";
            }
            else if (!valid1 && valid2)
            {
                bot2Score++;
                matchLog.Add($"Round {round}: {bot2.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
                roundResult = "Loss";
            }
            else
            {
                // Both invalid
                roundResult = "Draw";
            }
            
            // Record round history
            roundHistory.Add(new RoundHistory
            {
                Round = round,
                MyMove = move1 ?? "ERROR",
                OpponentMove = move2 ?? "ERROR",
                Result = roundResult,
                Role = null  // No roles in Security Game
            });
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

    private static GameState CreateGameState(int currentRound, int maxRounds, List<RoundHistory> roundHistory)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.SecurityGame
            },
            MoveHistory = new List<string>(),
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

    private static bool IsValidMove(string move)
    {
        return ValidMoves.Contains(move);
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

