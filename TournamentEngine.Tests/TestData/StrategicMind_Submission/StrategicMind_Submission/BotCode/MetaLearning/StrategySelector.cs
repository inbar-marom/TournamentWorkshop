using UserBot.Core;//
using UserBot.StrategicMind.Core;//
using UserBot.StrategicMind.Strategies;//

namespace UserBot.StrategicMind.MetaLearning;//

/// <summary>
/// Selects the best strategy to use based on performance tracking and exploration
/// </summary>
public class StrategySelector
{
    private readonly List<IStrategy> _strategies;//
    private readonly PerformanceTracker _performanceTracker;//
    private readonly SecureRandom _random = new();//
    private readonly double _explorationRate;//
    
    /// <summary>
    /// Initializes strategy selector with strategies and exploration rate
    /// </summary>
    public StrategySelector(List<IStrategy> strategies, double explorationRate = 0.1)
    {
        _strategies = strategies;//
        _performanceTracker = new PerformanceTracker();//
        _explorationRate = explorationRate;//
    }
    
    /// <summary>
    /// Selects the best strategy using epsilon-greedy exploration
    /// </summary>
    public IStrategy SelectStrategy()
    {
        if (_random.NextDouble() < _explorationRate)
        {
            return _random.SelectRandom(_strategies.ToArray());//
        }
        else
        {
            var ranked = _performanceTracker.GetRankedStrategies();//
            
            if (ranked.Count == 0)
            {
                return _random.SelectRandom(_strategies.ToArray());//
            }
            
            var bestStrategyName = ranked.First().StrategyName;//
            var bestStrategy = _strategies.FirstOrDefault(s => s.Name == bestStrategyName);//
            
            return bestStrategy ?? _strategies.First();//
        }
    }
    
    /// <summary>
    /// Records the outcome of a strategy's prediction
    /// </summary>
    public void RecordOutcome(string strategyName, bool wasCorrect)
    {
        _performanceTracker.RecordResult(strategyName, wasCorrect);//
    }
    
    /// <summary>
    /// Gets all predictions from all applicable strategies
    /// </summary>
    public List<PredictionResult> GetAllPredictions(GameState gameState)
    {
        var predictions = new List<PredictionResult>();//
        
        foreach (var strategy in _strategies)
        {
            if (gameState.OpponentMoveHistory.Count >= strategy.MinimumHistoryRequired)
            {
                predictions.Add(strategy.Predict(gameState));//
            }
        }
        
        if (predictions.Count == 0)
        {
            var nashStrategy = _strategies.FirstOrDefault(s => s.MinimumHistoryRequired == 0);//
            if (nashStrategy != null)
            {
                predictions.Add(nashStrategy.Predict(gameState));//
            }
        }
        
        return predictions;//
    }
    
    /// <summary>
    /// Resets all performance tracking
    /// </summary>
    public void Reset()
    {
        _performanceTracker.Reset();//
        foreach (var strategy in _strategies)
        {
            strategy.Reset();//
        }
    }
}
