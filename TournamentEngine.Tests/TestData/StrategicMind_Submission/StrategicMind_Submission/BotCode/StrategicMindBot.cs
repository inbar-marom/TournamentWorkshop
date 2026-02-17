using UserBot.Core;//
using UserBot.StrategicMind.Strategies;//
using UserBot.StrategicMind.MetaLearning;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind;//

/// <summary>
/// Strategic RPSLS bot using multiple pattern detection strategies with meta-learning
/// </summary>
public class StrategicMindBot : IBot
{
    public string TeamName => "StrategicMind";//
    public GameType GameType => UserBot.Core.GameType.RPSLS;//
    
    private readonly StrategySelector _strategySelector;//
    private readonly WeightedEnsemble _ensemble;//
    private readonly SecureRandom _random = new();//
    
    private string? _lastPredictedOpponentMove;//
    private string? _lastUsedStrategy;//
    
    /// <summary>
    /// Initializes bot with all strategies and meta-learning components
    /// </summary>
    public StrategicMindBot()
    {
        var strategies = new List<IStrategy>
        {
            new NashEquilibriumStrategy(),
            new FrequencyCounterStrategy(),
            new MarkovChainStrategy(),
            new ReactiveCounterStrategy(),
            new NGramPatternStrategy()
        };//
        
        _strategySelector = new StrategySelector(strategies, explorationRate: 0.1);//
        _ensemble = new WeightedEnsemble();//
    }
    
    /// <summary>
    /// Makes a move decision based on ensemble voting from all strategies
    /// </summary>
    public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
    {
        UpdatePerformanceTracking(gameState);//
        
        var allPredictions = _strategySelector.GetAllPredictions(gameState);//
        
        var finalPrediction = _ensemble.CombinePredictions(allPredictions);//
        
        _lastPredictedOpponentMove = finalPrediction.PredictedOpponentMove;//
        _lastUsedStrategy = finalPrediction.StrategyName;//
        
        if (gameState.RoundNumber <= 2)
        {
            var nash = new NashEquilibriumStrategy();//
            var nashPrediction = nash.Predict(gameState);//
            return Task.FromResult(nashPrediction.RecommendedCounterMove);//
        }
        
        return Task.FromResult(finalPrediction.RecommendedCounterMove);//
    }
    
    /// <summary>
    /// Updates performance tracking based on how well we predicted the opponent's move
    /// </summary>
    private void UpdatePerformanceTracking(GameState gameState)
    {
        if (_lastPredictedOpponentMove != null && 
            _lastUsedStrategy != null && 
            gameState.OpponentMoveHistory.Count > 0)
        {
            string actualOpponentMove = gameState.OpponentMoveHistory.Last();//
            bool wasCorrect = _lastPredictedOpponentMove == actualOpponentMove;//
            
            _strategySelector.RecordOutcome(_lastUsedStrategy, wasCorrect);//
        }
    }
    
    /// <summary>
    /// Not implemented for RPSLS bot
    /// </summary>
    public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This bot only plays RPSLS");//
    }
    
    /// <summary>
    /// Not implemented for RPSLS bot
    /// </summary>
    public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This bot only plays RPSLS");//
    }
    
    /// <summary>
    /// Not implemented for RPSLS bot
    /// </summary>
    public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This bot only plays RPSLS");//
    }
}
