namespace TournamentEngine.Core.GameRunner;

using Common;
using Executors;

/// <summary>
/// Main game runner implementation that orchestrates match execution
/// </summary>
public class GameRunner : IGameRunner
{
    private readonly Dictionary<GameType, IGameExecutor> _executors;
    private readonly TournamentConfig _config;

    public GameRunner(TournamentConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Register all game executors
        _executors = new Dictionary<GameType, IGameExecutor>
        {
            [GameType.RPSLS] = new RpslsExecutor(),
            [GameType.ColonelBlotto] = new BlottoExecutor(),
            [GameType.PenaltyKicks] = new PenaltyExecutor(),
            [GameType.SecurityGame] = new SecurityExecutor()
        };
    }

    public async Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, GameType gameType, CancellationToken cancellationToken)
    {
        if (bot1 == null) throw new ArgumentNullException(nameof(bot1));
        if (bot2 == null) throw new ArgumentNullException(nameof(bot2));
        
        if (!_executors.TryGetValue(gameType, out var executor))
        {
            throw new ArgumentException($"No executor found for game type: {gameType}", nameof(gameType));
        }
        
        return await executor.Execute(bot1, bot2, _config, cancellationToken);
    }

    public async Task<MatchResult> ExecuteMatch(IBot bot1, IBot bot2, IGame game, CancellationToken cancellationToken)
    {
        if (bot1 == null) throw new ArgumentNullException(nameof(bot1));
        if (bot2 == null) throw new ArgumentNullException(nameof(bot2));
        if (game == null) throw new ArgumentNullException(nameof(game));
        
        // For now, delegate to the GameType-based executor
        // In the future, this could use the IGame interface for custom game logic
        var gameType = DetermineGameType(game);
        return await ExecuteMatch(bot1, bot2, gameType, cancellationToken);
    }

    public async Task<bool> ValidateBot(IBot bot, GameType gameType)
    {
        if (bot is null) return false;
        
        try
        {
            var gameState = CreateValidationGameState(gameType);
            var timeout = TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource(timeout);
            
            switch (gameType)
            {
                case GameType.RPSLS:
                    var move = await bot.MakeMove(gameState, cts.Token);
                    return !string.IsNullOrEmpty(move);
                case GameType.ColonelBlotto:
                    var allocation = await bot.AllocateTroops(gameState, cts.Token);
                    // allocation is non-null per IBot contract; verify length only
                    return allocation.Length == 5;
                case GameType.PenaltyKicks:
                    var decision = await bot.MakePenaltyDecision(gameState, cts.Token);
                    return !string.IsNullOrEmpty(decision);
                case GameType.SecurityGame:
                    var securityMove = await bot.MakeSecurityMove(gameState, cts.Token);
                    return !string.IsNullOrEmpty(securityMove);
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static GameType DetermineGameType(IGame game)
    {
        // Simple heuristic based on game type name or properties
        // In a full implementation, IGame would expose a GameType property
        var typeName = game.GetType().Name.ToLower();
        
        if (typeName.Contains("rpsls") || typeName.Contains("rock"))
            return GameType.RPSLS;
        if (typeName.Contains("blotto") || typeName.Contains("colonel"))
            return GameType.ColonelBlotto;
        if (typeName.Contains("penalty") || typeName.Contains("kick"))
            return GameType.PenaltyKicks;
        if (typeName.Contains("security") || typeName.Contains("hacker"))
            return GameType.SecurityGame;
        
        // Default fallback
        return GameType.RPSLS;
    }

    private static GameState CreateValidationGameState(GameType gameType)
    {
        return new GameState
        {
            State = new Dictionary<string, object>
            {
                ["GameType"] = gameType,
                ["Validation"] = true
            },
            MoveHistory = new List<string>(),
            CurrentRound = 1,
            MaxRounds = 1,
            IsGameOver = false,
            Winner = null
        };
    }
}
