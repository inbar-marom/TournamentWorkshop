using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// Markov Chain strategy: exploits sequential patterns in opponent moves
/// </summary>
public class MarkovChainStrategy : IStrategy
{
    private readonly HistoryAnalyzer _analyzer = new();//
    private readonly SecureRandom _random = new();//
    
    public string Name => "Markov Chain";//
    public int MinimumHistoryRequired => 5;//
    
    /// <summary>
    /// Predicts next move based on Markov transition matrix
    /// </summary>
    public PredictionResult Predict(GameState gameState)
    {
        if (gameState.OpponentMoveHistory.Count < MinimumHistoryRequired)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var transitionMatrix = _analyzer.BuildTransitionMatrix(gameState.OpponentMoveHistory);//
        
        string lastMove = gameState.OpponentMoveHistory.Last();//
        
        if (!transitionMatrix.ContainsKey(lastMove))
        {
            return CreateLowConfidencePrediction();//
        }
        
        var transitions = transitionMatrix[lastMove];//
        var mostLikely = transitions.MaxBy(kvp => kvp.Value);//
        
        double bias = mostLikely.Value - 0.2;//
        double confidence = Math.Min(1.0, Math.Max(0.0, bias * 3.0));//
        
        string counter = GameRules.GetBestCounterTo(mostLikely.Key, new Random());//
        
        return new PredictionResult
        {
            PredictedOpponentMove = mostLikely.Key,
            RecommendedCounterMove = counter,
            Confidence = confidence,
            StrategyName = Name,
            Reasoning = $"After {lastMove}, opponent plays {mostLikely.Key} {mostLikely.Value:P0}"
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
            Reasoning = "Insufficient history for Markov analysis"
        };//
    }
}
