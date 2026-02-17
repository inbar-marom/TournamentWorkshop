using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// Interface that all RPSLS strategies must implement
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// Name of this strategy
    /// </summary>
    string Name { get; }//
    
    /// <summary>
    /// Minimum number of opponent moves required for this strategy to work effectively
    /// </summary>
    int MinimumHistoryRequired { get; }//
    
    /// <summary>
    /// Makes a prediction based on the current game state
    /// </summary>
    PredictionResult Predict(GameState gameState);//
    
    /// <summary>
    /// Resets any cached state between matches
    /// </summary>
    void Reset();//
}
