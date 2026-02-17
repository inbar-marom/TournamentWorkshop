namespace UserBot.StrategicMind.Core;//

/// <summary>
/// Standardized prediction output from all strategies
/// </summary>
public class PredictionResult
{
    /// <summary>
    /// The predicted move the opponent will make
    /// </summary>
    public string PredictedOpponentMove { get; set; } = string.Empty;//
    
    /// <summary>
    /// The recommended counter move to play
    /// </summary>
    public string RecommendedCounterMove { get; set; } = string.Empty;//
    
    /// <summary>
    /// Confidence in this prediction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }//
    
    /// <summary>
    /// Name of the strategy that made this prediction
    /// </summary>
    public string StrategyName { get; set; } = string.Empty;//
    
    /// <summary>
    /// Reasoning behind the prediction (for debugging/logging)
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;//
}
