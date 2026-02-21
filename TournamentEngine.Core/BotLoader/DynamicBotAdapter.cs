using System.Reflection;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Dynamic adapter that uses reflection to wrap bots with different type systems
/// Supports bots that use different IBot/GameState/GameType definitions
/// </summary>
public class DynamicBotAdapter : IBotAdapter
{
    private readonly object _wrappedBot;
    private readonly Type _wrappedBotType;
    private readonly string _supportedNamespace;
    
    // Cached method info for performance
    private readonly MethodInfo _makeMoveMethod;
    private readonly MethodInfo? _allocateTroopsMethod;
    private readonly MethodInfo? _makePenaltyDecisionMethod;
    private readonly MethodInfo? _makeSecurityMoveMethod;
    private readonly PropertyInfo _teamNameProperty;
    private readonly PropertyInfo _gameTypeProperty;
    
    // Type mapping for GameState conversion
    private readonly Type? _wrappedGameStateType;
    private readonly Dictionary<string, PropertyInfo> _gameStateProperties = new();
    
    public object WrappedBot => _wrappedBot;
    public string SupportedNamespace => _supportedNamespace;
    
    public DynamicBotAdapter(object bot, string supportedNamespace)
    {
        _wrappedBot = bot ?? throw new ArgumentNullException(nameof(bot));
        _wrappedBotType = bot.GetType();
        _supportedNamespace = supportedNamespace;
        
        // Cache reflection info
        _teamNameProperty = _wrappedBotType.GetProperty("TeamName") 
            ?? throw new InvalidOperationException($"Bot type {_wrappedBotType.Name} missing TeamName property");
        _gameTypeProperty = _wrappedBotType.GetProperty("GameType")
            ?? throw new InvalidOperationException($"Bot type {_wrappedBotType.Name} missing GameType property");
        
        _makeMoveMethod = _wrappedBotType.GetMethod("MakeMove")
            ?? throw new InvalidOperationException($"Bot type {_wrappedBotType.Name} missing MakeMove method");
        _allocateTroopsMethod = _wrappedBotType.GetMethod("AllocateTroops");
        _makePenaltyDecisionMethod = _wrappedBotType.GetMethod("MakePenaltyDecision");
        _makeSecurityMoveMethod = _wrappedBotType.GetMethod("MakeSecurityMove");
        
        // Find the GameState type from the wrapped bot's namespace
        var gameStateParam = _makeMoveMethod.GetParameters().FirstOrDefault();
        if (gameStateParam != null)
        {
            _wrappedGameStateType = gameStateParam.ParameterType;
            CacheGameStateProperties(_wrappedGameStateType);
        }
    }
    
    public string TeamName => _teamNameProperty.GetValue(_wrappedBot)?.ToString() ?? "Unknown";
    
    public GameType GameType
    {
        get
        {
            var value = _gameTypeProperty.GetValue(_wrappedBot);
            if (value == null) return Common.GameType.RPSLS;
            
            // Convert from wrapped GameType to TournamentEngine.Core.Common.GameType
            var enumName = value.ToString();
            return Enum.TryParse<GameType>(enumName, out var result) ? result : Common.GameType.RPSLS;
        }
    }
    
    public async Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        var wrappedGameState = ConvertGameState(gameState);
        var task = (Task<string>)_makeMoveMethod.Invoke(_wrappedBot, new[] { wrappedGameState, cancellationToken });
        return await task;
    }
    
    public async Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        if (_allocateTroopsMethod == null)
            throw new NotImplementedException($"Bot {TeamName} does not implement AllocateTroops");
        
        var wrappedGameState = ConvertGameState(gameState);
        var task = (Task<int[]>)_allocateTroopsMethod.Invoke(_wrappedBot, new[] { wrappedGameState, cancellationToken });
        return await task;
    }
    
    public async Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        if (_makePenaltyDecisionMethod == null)
            throw new NotImplementedException($"Bot {TeamName} does not implement MakePenaltyDecision");
        
        var wrappedGameState = ConvertGameState(gameState);
        var task = (Task<string>)_makePenaltyDecisionMethod.Invoke(_wrappedBot, new[] { wrappedGameState, cancellationToken });
        return await task;
    }
    
    public async Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        if (_makeSecurityMoveMethod == null)
            throw new NotImplementedException($"Bot {TeamName} does not implement MakeSecurityMove");
        
        var wrappedGameState = ConvertGameState(gameState);
        var task = (Task<string>)_makeSecurityMoveMethod.Invoke(_wrappedBot, new[] { wrappedGameState, cancellationToken });
        return await task;
    }
    
    private void CacheGameStateProperties(Type gameStateType)
    {
        var properties = gameStateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            _gameStateProperties[prop.Name] = prop;
        }
    }
    
    private object ConvertGameState(TournamentEngine.Core.Common.GameState engineGameState)
    {
        if (_wrappedGameStateType == null)
            throw new InvalidOperationException("Wrapped GameState type not found");
        
        // Create instance of the wrapped GameState type
        var wrappedGameState = Activator.CreateInstance(_wrappedGameStateType);
        
        // Map properties from engine GameState to wrapped GameState
        // TournamentEngine.Core.Common.GameState uses different property names
        MapProperty(wrappedGameState, "RoundNumber", engineGameState.CurrentRound);
        MapProperty(wrappedGameState, "TotalRounds", engineGameState.MaxRounds);
        
        // Extract scores from State dictionary if available
        int currentScore = engineGameState.State.TryGetValue("CurrentScore", out var cs) ? Convert.ToInt32(cs) : 0;
        int opponentScore = engineGameState.State.TryGetValue("OpponentScore", out var os) ? Convert.ToInt32(os) : 0;
        MapProperty(wrappedGameState, "CurrentScore", currentScore);
        MapProperty(wrappedGameState, "OpponentScore", opponentScore);
        
        // Convert move history - TournamentEngine uses combined MoveHistory, need to split
        var myMoves = new List<string>();
        var oppMoves = new List<string>();
        foreach (var round in engineGameState.RoundHistory)
        {
            if (round.MyMove != null) myMoves.Add(round.MyMove);
            if (round.OpponentMove != null) oppMoves.Add(round.OpponentMove);
        }
        MapProperty(wrappedGameState, "MyMoveHistory", myMoves);
        MapProperty(wrappedGameState, "OpponentMoveHistory", oppMoves);
        
        // Extract metadata from State dictionary
        string matchId = engineGameState.State.TryGetValue("MatchId", out var mid) ? mid?.ToString() ?? "" : "";
        string opponentName = engineGameState.State.TryGetValue("OpponentName", out var on) ? on?.ToString() ?? "" : "";
        MapProperty(wrappedGameState, "MatchId", matchId);
        MapProperty(wrappedGameState, "OpponentName", opponentName);
        
        // Territory-specific properties (if applicable)
        if (engineGameState.State.TryGetValue("TerritoryCount", out var tc))
            MapProperty(wrappedGameState, "TerritoryCount", Convert.ToInt32(tc));
        if (engineGameState.State.TryGetValue("MyTerritories", out var mt))
            MapProperty(wrappedGameState, "MyTerritories", mt);
        if (engineGameState.State.TryGetValue("OpponentTerritories", out var ot))
            MapProperty(wrappedGameState, "OpponentTerritories", ot);
        if (engineGameState.State.TryGetValue("AvailableTroops", out var at))
            MapProperty(wrappedGameState, "AvailableTroops", Convert.ToInt32(at));
        
        // Map GameType
        if (engineGameState.State.TryGetValue("GameType", out var gt))
        {
            MapProperty(wrappedGameState, "GameType", gt);
        }
        
        // Map the entire State dictionary to AdditionalData so bots can access
        // game-specific data like Role, TargetValues, TotalDefenseUnits, etc.
        if (_gameStateProperties.TryGetValue("AdditionalData", out var additionalDataProp) && additionalDataProp.CanWrite)
        {
            var additionalData = new Dictionary<string, object>(engineGameState.State);
            additionalDataProp.SetValue(wrappedGameState, additionalData);
        }
        
        return wrappedGameState;
    }
    
    private void MapProperty(object target, string propertyName, object value)
    {
        if (_gameStateProperties.TryGetValue(propertyName, out var prop) && prop.CanWrite)
        {
            try
            {
                // Handle list conversions
                if (value is List<string> stringList && prop.PropertyType.IsGenericType)
                {
                    var listType = typeof(List<>).MakeGenericType(typeof(string));
                    if (prop.PropertyType.IsAssignableFrom(listType))
                    {
                        prop.SetValue(target, new List<string>(stringList));
                        return;
                    }
                }
                
                // Handle array conversions
                if (value is int[] intArray && prop.PropertyType == typeof(int[]))
                {
                    prop.SetValue(target, (int[])intArray.Clone());
                    return;
                }
                
                // Direct assignment for compatible types
                if (value != null && prop.PropertyType.IsAssignableFrom(value.GetType()))
                {
                    prop.SetValue(target, value);
                }
                else if (value != null)
                {
                    // Try conversion for value types
                    var convertedValue = Convert.ChangeType(value, prop.PropertyType);
                    prop.SetValue(target, convertedValue);
                }
            }
            catch
            {
                // Skip properties that can't be mapped
            }
        }
    }
}
