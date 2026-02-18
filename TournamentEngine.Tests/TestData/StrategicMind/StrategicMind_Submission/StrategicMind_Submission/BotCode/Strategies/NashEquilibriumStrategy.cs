using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// Nash Equilibrium strategy: uniformly random, unexploitable baseline
/// </summary>
public class NashEquilibriumStrategy : IStrategy
{
    private readonly SecureRandom _random = new();//
    
    public string Name => "Nash Equilibrium";//
    public int MinimumHistoryRequired => 0;//
    
    /// <summary>
    /// Predicts next move using uniform random distribution
    /// </summary>
    public PredictionResult Predict(GameState gameState)
    {
        string move = _random.SelectRandom(GameRules.AllMoves);//
        
        return new PredictionResult
        {
            PredictedOpponentMove = "Unknown",
            RecommendedCounterMove = move,
            Confidence = 0.4,
            StrategyName = Name,
            Reasoning = "Uniform random - unexploitable baseline"
        };//
    }
    
    /// <summary>
    /// Resets strategy state
    /// </summary>
    public void Reset()
    {
    }
}
