using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// Reactive Counter strategy: detects and exploits opponents who counter our previous moves
/// </summary>
public class ReactiveCounterStrategy : IStrategy
{
    private readonly HistoryAnalyzer _analyzer = new();//
    private readonly SecureRandom _random = new();//
    
    public string Name => "Reactive Counter";//
    public int MinimumHistoryRequired => 4;//
    
    /// <summary>
    /// Predicts next move based on reactive counter pattern detection
    /// </summary>
    public PredictionResult Predict(GameState gameState)
    {
        if (gameState.OpponentMoveHistory.Count < MinimumHistoryRequired)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var (isReactive, confidence) = _analyzer.DetectReactivePattern(
            gameState.OpponentMoveHistory,
            gameState.MyMoveHistory
        );//
        
        if (!isReactive || gameState.MyMoveHistory.Count == 0)
        {
            return CreateLowConfidencePrediction();//
        }
        
        string ourLastMove = gameState.MyMoveHistory.Last();//
        
        var theirPredictedCounters = GameRules.GetCountersTo(ourLastMove);//
        string theirPredictedMove = theirPredictedCounters.Count > 0 
            ? theirPredictedCounters[new Random().Next(theirPredictedCounters.Count)]
            : GameRules.AllMoves[0];//
        
        string ourCounterCounter = GameRules.GetBestCounterTo(theirPredictedMove, new Random());//
        
        return new PredictionResult
        {
            PredictedOpponentMove = theirPredictedMove,
            RecommendedCounterMove = ourCounterCounter,
            Confidence = confidence,
            StrategyName = Name,
            Reasoning = $"Opponent is reactive (confidence: {confidence:P0}), countering their counter"
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
            Reasoning = "No reactive pattern detected"
        };//
    }
}
