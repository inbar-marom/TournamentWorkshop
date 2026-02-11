namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;

/// <summary>
/// Executor for Rock-Paper-Scissors-Lizard-Spock game
/// </summary>
public class RpslsExecutor : IGameExecutor
{
    private static readonly string[] ValidMoves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };
    
    // Define winning combinations: key beats all values in the set
    private static readonly Dictionary<string, HashSet<string>> WinningMoves = new()
    {
        ["Rock"] = new HashSet<string> { "Scissors", "Lizard" },
        ["Paper"] = new HashSet<string> { "Rock", "Spock" },
        ["Scissors"] = new HashSet<string> { "Paper", "Lizard" },
        ["Lizard"] = new HashSet<string> { "Spock", "Paper" },
        ["Spock"] = new HashSet<string> { "Scissors", "Rock" }
    };

    public GameType GameType => GameType.RPSLS;

    public async Task<MatchResult> Execute(IBot bot1, IBot bot2, TournamentConfig config, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var matchLog = new List<string>();
        var errors = new List<string>();
        
        int bot1Score = 0;
        int bot2Score = 0;
        int bot1Errors = 0;
        int bot2Errors = 0;
        
        var maxRounds = config.MaxRoundsRPSLS;
        
        matchLog.Add($"=== RPSLS Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Max Rounds: {maxRounds}");
        matchLog.Add("");

        // Execute rounds
        for (int round = 1; round <= maxRounds; round++)
        {
            var gameState = CreateGameState(round, maxRounds, bot1Score, bot2Score);
            
            // Get moves from both bots with timeout
            var (move1, error1) = await GetBotMoveWithTimeout(bot1, gameState, config.MoveTimeout, cancellationToken);
            var (move2, error2) = await GetBotMoveWithTimeout(bot2, gameState, config.MoveTimeout, cancellationToken);
            
            // Track errors
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
            
            // Validate moves
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
            
            // Determine round winner
            if (valid1 && valid2)
            {
                var roundWinner = DetermineWinner(move1!, move2!);
                if (roundWinner == 1)
                {
                    bot1Score++;
                    matchLog.Add($"Round {round}: {bot1.TeamName} ({move1}) beats {bot2.TeamName} ({move2}) - Score: {bot1Score}-{bot2Score}");
                }
                else if (roundWinner == 2)
                {
                    bot2Score++;
                    matchLog.Add($"Round {round}: {bot2.TeamName} ({move2}) beats {bot1.TeamName} ({move1}) - Score: {bot1Score}-{bot2Score}");
                }
                else
                {
                    matchLog.Add($"Round {round}: Draw - {move1} vs {move2} - Score: {bot1Score}-{bot2Score}");
                }
            }
            else if (valid1 && !valid2)
            {
                bot1Score++;
                matchLog.Add($"Round {round}: {bot1.TeamName} wins by default (opponent error) - Score: {bot1Score}-{bot2Score}");
            }
            else if (!valid1 && valid2)
            {
                bot2Score++;
                matchLog.Add($"Round {round}: {bot2.TeamName} wins by default (opponent error) - Score: {bot1Score}-{bot2Score}");
            }
            else
            {
                // Both invalid - random winner for this round (deterministic for tests)
                var randomWinner = (round % 2 == 0) ? 1 : 2;
                if (randomWinner == 1)
                {
                    bot1Score++;
                    matchLog.Add($"Round {round}: Both errors - {bot1.TeamName} wins (random) - Score: {bot1Score}-{bot2Score}");
                }
                else
                {
                    bot2Score++;
                    matchLog.Add($"Round {round}: Both errors - {bot2.TeamName} wins (random) - Score: {bot1Score}-{bot2Score}");
                }
            }
        }
        
        // Determine final outcome
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
            GameType = GameType.RPSLS,
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

    private static GameState CreateGameState(int currentRound, int maxRounds, int bot1Score, int bot2Score)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.RPSLS,
                ["Bot1Score"] = bot1Score,
                ["Bot2Score"] = bot2Score
            },
            MoveHistory = new List<string>(),
            CurrentRound = currentRound,
            MaxRounds = maxRounds,
            IsGameOver = false,
            Winner = null
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
            
            var move = await bot.MakeMove(gameState, cts.Token);
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

    private static int DetermineWinner(string move1, string move2)
    {
        if (move1 == move2) return 0; // Draw
        
        if (WinningMoves.TryGetValue(move1, out var move1Beats) && move1Beats.Contains(move2))
            return 1; // Bot 1 wins
        
        if (WinningMoves.TryGetValue(move2, out var move2Beats) && move2Beats.Contains(move1))
            return 2; // Bot 2 wins
        
        return 0; // Should not happen with valid moves
    }

    private static MatchOutcome DetermineOutcome(int bot1Score, int bot2Score, int bot1Errors, int bot2Errors)
    {
        // Check for error-based outcomes
        if (bot1Errors > 0 && bot2Errors > 0)
            return bot1Score > bot2Score ? MatchOutcome.Player1Wins : 
                   bot2Score > bot1Score ? MatchOutcome.Player2Wins : MatchOutcome.BothError;
        
        if (bot1Errors > 0 && bot2Errors == 0)
            return MatchOutcome.Player2Wins;
        
        if (bot2Errors > 0 && bot1Errors == 0)
            return MatchOutcome.Player1Wins;
        
        // Normal scoring outcome
        if (bot1Score > bot2Score)
            return MatchOutcome.Player1Wins;
        else if (bot2Score > bot1Score)
            return MatchOutcome.Player2Wins;
        else
            return MatchOutcome.Draw;
    }
}
