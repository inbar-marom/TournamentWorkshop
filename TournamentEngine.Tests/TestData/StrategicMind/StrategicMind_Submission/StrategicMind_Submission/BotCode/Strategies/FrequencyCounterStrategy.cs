using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// Frequency Counter strategy: exploits opponent's move frequency biases
/// </summary>
public class FrequencyCounterStrategy : IStrategy
{
    private readonly HistoryAnalyzer _analyzer = new();//
    private readonly SecureRandom _random = new();//
    
    public string Name => "Frequency Counter";//
    public int MinimumHistoryRequired => 3;//
    
    /// <summary>
    /// Predicts next move based on opponent frequency bias
    /// </summary>
    public PredictionResult Predict(GameState gameState)
    {
        if (gameState.OpponentMoveHistory.Count < MinimumHistoryRequired)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var frequencies = _analyzer.CalculateFrequencyDistribution(gameState.OpponentMoveHistory);//
        
        var mostFrequent = frequencies.MaxBy(kvp => kvp.Value);//
        
        double bias = mostFrequent.Value - 0.2;//
        double confidence = Math.Min(1.0, Math.Max(0.0, bias * 2.5));//
        
        string counter = GameRules.GetBestCounterTo(mostFrequent.Key, new Random());//
        
        return new PredictionResult
        {
            PredictedOpponentMove = mostFrequent.Key,
            RecommendedCounterMove = counter,
            Confidence = confidence,
            StrategyName = Name,
            Reasoning = $"Opponent plays {mostFrequent.Key} {mostFrequent.Value:P0} of the time"
        };//
    }
    
    /// <summary>
    /// Resets strategy state
    /// </summary>
    public void Reset()
    {
    }
    
    private PredictionResult CreateLowConfidencePrediction()
    {
        string move = _random.SelectRandom(GameRules.AllMoves);//
        
        return new PredictionResult
        {
            PredictedOpponentMove = "Unknown",
            RecommendedCounterMove = move,
            Confidence = 0.0,
            StrategyName = Name,
            Reasoning = "Insufficient history for frequency analysis"
        };//
    }
}
