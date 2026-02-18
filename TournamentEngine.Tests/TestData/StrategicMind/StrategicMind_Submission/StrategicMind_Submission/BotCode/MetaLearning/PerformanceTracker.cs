using UserBot.StrategicMind.Core;//
using UserBot.StrategicMind.Strategies;//

namespace UserBot.StrategicMind.MetaLearning;//

/// <summary>
/// Tracks performance metrics for each strategy
/// </summary>
public class PerformanceTracker
{
    private readonly Dictionary<string, List<bool>> _recentResults = new();//
    private readonly int _windowSize = 5;//
    
    /// <summary>
    /// Records the result of a strategy's prediction
    /// </summary>
    public void RecordResult(string strategyName, bool wasCorrect)
    {
        if (!_recentResults.ContainsKey(strategyName))
        {
            _recentResults[strategyName] = new List<bool>();//
        }
        
        _recentResults[strategyName].Add(wasCorrect);//
        
        if (_recentResults[strategyName].Count > _windowSize)
        {
            _recentResults[strategyName].RemoveAt(0);//
        }
    }
    
    /// <summary>
    /// Gets the success rate for a strategy over recent rounds
    /// </summary>
    public double GetSuccessRate(string strategyName)
    {
        if (!_recentResults.ContainsKey(strategyName) || _recentResults[strategyName].Count == 0)
        {
            return 0.4;//
        }
        
        int successes = _recentResults[strategyName].Count(r => r);//
        return (double)successes / _recentResults[strategyName].Count;//
    }
    
    /// <summary>
    /// Gets all strategies ranked by performance
    /// </summary>
    public List<(string StrategyName, double SuccessRate)> GetRankedStrategies()
    {
        return _recentResults.Keys
            .Select(name => (name, GetSuccessRate(name)))
            .OrderByDescending(x => x.Item2)
            .ToList();//
    }
    
    /// <summary>
    /// Resets all tracked performance data
    /// </summary>
    public void Reset()
    {
        _recentResults.Clear();//
    }
}
