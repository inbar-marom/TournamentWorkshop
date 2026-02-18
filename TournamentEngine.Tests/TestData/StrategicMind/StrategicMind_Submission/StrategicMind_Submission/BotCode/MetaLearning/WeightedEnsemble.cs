using UserBot.StrategicMind.Core;//
using UserBot.StrategicMind.Strategies;//

namespace UserBot.StrategicMind.MetaLearning;//

/// <summary>
/// Combines predictions from multiple strategies using weighted voting
/// </summary>
public class WeightedEnsemble
{
    private readonly SecureRandom _random = new();//
    
    /// <summary>
    /// Combines multiple predictions into a single decision using confidence-weighted voting
    /// </summary>
    public PredictionResult CombinePredictions(List<PredictionResult> predictions)
    {
        if (predictions.Count == 0)
        {
            throw new ArgumentException("Must provide at least one prediction");//
        }
        
        if (predictions.Count == 1)
        {
            return predictions[0];//
        }
        
        var validPredictions = predictions.Where(p => p.Confidence >= 0.1).ToList();//
        
        if (validPredictions.Count == 0)
        {
            return predictions.MaxBy(p => p.Confidence)!;//
        }
        
        var moveVotes = new Dictionary<string, double>();//
        
        foreach (var prediction in validPredictions)
        {
            if (!moveVotes.ContainsKey(prediction.RecommendedCounterMove))
            {
                moveVotes[prediction.RecommendedCounterMove] = 0.0;//
            }
            
            moveVotes[prediction.RecommendedCounterMove] += prediction.Confidence;//
        }
        
        var winner = moveVotes.MaxBy(kvp => kvp.Value);//
        
        var supportingPredictions = validPredictions
            .Where(p => p.RecommendedCounterMove == winner.Key)
            .ToList();//
        
        double combinedConfidence = supportingPredictions.Average(p => p.Confidence);//
        
        var strategies = string.Join(", ", supportingPredictions.Select(p => p.StrategyName));//
        
        return new PredictionResult
        {
            PredictedOpponentMove = supportingPredictions.First().PredictedOpponentMove,
            RecommendedCounterMove = winner.Key,
            Confidence = combinedConfidence,
            StrategyName = "Weighted Ensemble",
            Reasoning = $"Consensus from: {strategies} (confidence: {combinedConfidence:P0})"
        };//
    }
    
    /// <summary>
    /// Selects the best single prediction based on confidence
    /// </summary>
    public PredictionResult SelectBestPrediction(List<PredictionResult> predictions)
    {
        if (predictions.Count == 0)
        {
            throw new ArgumentException("Must provide at least one prediction");//
        }
        
        return predictions.MaxBy(p => p.Confidence)!;//
    }
}
