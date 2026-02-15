namespace TournamentEngine.Core.GameRunner.Executors;

using Common;
using System.Linq;


/// <summary>
/// Executor for Penalty Kicks game
/// Roles (Shooter/Goalkeeper) are randomly assigned at the start of each match.
/// - Shooter scores 1 point when choosing a different direction than the goalkeeper
/// - Goalkeeper scores 2 points when matching the shooter's direction (saving the shot)
/// - 9 rounds total
/// </summary>
public class PenaltyExecutor : IGameExecutor
{
    private static readonly string[] ValidDecisions = { "Left", "Center", "Right" };
    private const int MaxRounds = 9;
    private static readonly Random _random = new Random();

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
        
        // Randomly assign roles
        bool bot1IsShooter = _random.Next(2) == 0;
        var shooterBot = bot1IsShooter ? bot1 : bot2;
        var goalkeeperBot = bot1IsShooter ? bot2 : bot1;
        
        matchLog.Add($"=== Penalty Kicks Match: {bot1.TeamName} vs {bot2.TeamName} ===");
        matchLog.Add($"Shooter: {shooterBot.TeamName}, Goalkeeper: {goalkeeperBot.TeamName}");
        matchLog.Add($"Max Rounds: {MaxRounds}");
        matchLog.Add("");

        // Track round history for each bot separately (they have different roles)
        var shooterHistory = new List<RoundHistory>();
        var goalkeeperHistory = new List<RoundHistory>();

        // Execute rounds with assigned roles
        for (int round = 1; round <= MaxRounds; round++)
        {
            var shooterGameState = CreateGameState(round, MaxRounds, "Shooter", shooterHistory);
            var goalkeeperGameState = CreateGameState(round, MaxRounds, "Goalkeeper", goalkeeperHistory);
            
            var (shooterDecision, shooterError) = await GetBotDecisionWithTimeout(shooterBot, shooterGameState, config.MoveTimeout, cancellationToken);
            var (goalkeeperDecision, goalkeeperError) = await GetBotDecisionWithTimeout(goalkeeperBot, goalkeeperGameState, config.MoveTimeout, cancellationToken);
            
            if (shooterError != null)
            {
                if (bot1IsShooter) bot1Errors++; else bot2Errors++;
                errors.Add($"Round {round}: {shooterBot.TeamName} (Shooter) - {shooterError}");
                matchLog.Add($"Round {round}: {shooterBot.TeamName} (Shooter) ERROR - {shooterError}");
            }
            
            if (goalkeeperError != null)
            {
                if (bot1IsShooter) bot2Errors++; else bot1Errors++;
                errors.Add($"Round {round}: {goalkeeperBot.TeamName} (Goalkeeper) - {goalkeeperError}");
                matchLog.Add($"Round {round}: {goalkeeperBot.TeamName} (Goalkeeper) ERROR - {goalkeeperError}");
            }
            
            bool validShooter = shooterDecision != null && IsValidDecision(shooterDecision);
            bool validGoalkeeper = goalkeeperDecision != null && IsValidDecision(goalkeeperDecision);
            
            if (!validShooter && shooterError == null)
            {
                var invalidError = $"Invalid decision: '{shooterDecision}'";
                if (bot1IsShooter) bot1Errors++; else bot2Errors++;
                errors.Add($"Round {round}: {shooterBot.TeamName} (Shooter) - {invalidError}");
                matchLog.Add($"Round {round}: {shooterBot.TeamName} (Shooter) - {invalidError}");
            }
            
            if (!validGoalkeeper && goalkeeperError == null)
            {
                var invalidError = $"Invalid decision: '{goalkeeperDecision}'";
                if (bot1IsShooter) bot2Errors++; else bot1Errors++;
                errors.Add($"Round {round}: {goalkeeperBot.TeamName} (Goalkeeper) - {invalidError}");
                matchLog.Add($"Round {round}: {goalkeeperBot.TeamName} (Goalkeeper) - {invalidError}");
            }
            
            // Penalty kicks scoring: shooter scores 1 if different, goalkeeper scores 2 if same
            string shooterResult, goalkeeperResult;
            if (validShooter && validGoalkeeper)
            {
                if (shooterDecision != goalkeeperDecision)
                {
                    // Shooter scores
                    if (bot1IsShooter) bot1Score++; else bot2Score++;
                    matchLog.Add($"Round {round}: GOAL! {shooterBot.TeamName} (Shooter: {shooterDecision}) vs {goalkeeperBot.TeamName} (Goalkeeper: {goalkeeperDecision}) - Score: {bot1Score}-{bot2Score}");
                    shooterResult = "Win";
                    goalkeeperResult = "Loss";
                }
                else
                {
                    // Goalkeeper scores
                    if (bot1IsShooter) bot2Score += 2; else bot1Score += 2;
                    matchLog.Add($"Round {round}: SAVE! {goalkeeperBot.TeamName} blocks {shooterBot.TeamName} (both chose {shooterDecision}) - Score: {bot1Score}-{bot2Score}");
                    shooterResult = "Loss";
                    goalkeeperResult = "Win";
                }
            }
            else if (validShooter && !validGoalkeeper)
            {
                if (bot1IsShooter) bot1Score++; else bot2Score++;
                matchLog.Add($"Round {round}: {shooterBot.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
                shooterResult = "Win";
                goalkeeperResult = "Loss";
            }
            else if (!validShooter && validGoalkeeper)
            {
                if (bot1IsShooter) bot2Score++; else bot1Score++;
                matchLog.Add($"Round {round}: {goalkeeperBot.TeamName} scores by default - Score: {bot1Score}-{bot2Score}");
                shooterResult = "Loss";
                goalkeeperResult = "Win";
            }
            else
            {
                // Both invalid
                shooterResult = "Draw";
                goalkeeperResult = "Draw";
            }
            
            // Record round history from each bot's perspective
            shooterHistory.Add(new RoundHistory
            {
                Round = round,
                MyMove = shooterDecision ?? "ERROR",
                OpponentMove = goalkeeperDecision ?? "ERROR",
                Result = shooterResult,
                Role = "Shooter"
            });
            
            goalkeeperHistory.Add(new RoundHistory
            {
                Round = round,
                MyMove = goalkeeperDecision ?? "ERROR",
                OpponentMove = shooterDecision ?? "ERROR",
                Result = goalkeeperResult,
                Role = "Goalkeeper"
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

    private static GameState CreateGameState(int currentRound = 1, int maxRounds = MaxRounds, string role = "Shooter", List<RoundHistory>? roundHistory = null)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = GameType.PenaltyKicks,
                ["Role"] = role  // "Shooter" or "Goalkeeper"
            },
            MoveHistory = new List<string>(),
            CurrentRound = currentRound,
            MaxRounds = maxRounds,
            IsGameOver = false,
            Winner = null,
            RoundHistory = roundHistory != null ? new List<RoundHistory>(roundHistory) : new List<RoundHistory>()
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

