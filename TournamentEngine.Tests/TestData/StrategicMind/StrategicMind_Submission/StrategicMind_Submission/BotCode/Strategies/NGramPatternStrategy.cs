using UserBot.Core;//
using UserBot.StrategicMind.Core;//

namespace UserBot.StrategicMind.Strategies;//

/// <summary>
/// N-Gram Pattern strategy: detects multi-move sequences and predicts continuations
/// </summary>
public class NGramPatternStrategy : IStrategy
{
    private readonly HistoryAnalyzer _analyzer = new();//
    private readonly SecureRandom _random = new();//
    private const int NGramSize = 3;//
    
    public string Name => "N-Gram Pattern";//
    public int MinimumHistoryRequired => 6;//
    
    /// <summary>
    /// Predicts next move based on n-gram pattern matching
    /// </summary>
    public PredictionResult Predict(GameState gameState)
    {
        if (gameState.OpponentMoveHistory.Count < MinimumHistoryRequired)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var ngrams = _analyzer.ExtractNGrams(gameState.OpponentMoveHistory, NGramSize);//
        
        if (ngrams.Count == 0)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var recentMoves = gameState.OpponentMoveHistory
            .Skip(gameState.OpponentMoveHistory.Count - (NGramSize - 1))
            .Take(NGramSize - 1)
            .ToList();//
        
        if (recentMoves.Count < NGramSize - 1)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var recentPattern = string.Join(",", recentMoves);//
        var matchingPatterns = ngrams
            .Where(kvp => kvp.Key.StartsWith(recentPattern))
            .ToList();//
        
        if (matchingPatterns.Count == 0)
        {
            return CreateLowConfidencePrediction();//
        }
        
        var mostFrequent = matchingPatterns.MaxBy(kvp => kvp.Value);//
        var completionMove = mostFrequent.Key.Split(',').Last();//
        
        int totalPatterns = matchingPatterns.Sum(kvp => kvp.Value);//
        double confidence = _analyzer.CalculatePatternConfidence(
            mostFrequent.Value, 
            totalPatterns + 1
        );//
        
        string counter = GameRules.GetBestCounterTo(completionMove, new Random());//
        
        return new PredictionResult
        {
            PredictedOpponentMove = completionMove,
            RecommendedCounterMove = counter,
            Confidence = confidence,
            StrategyName = Name,
            Reasoning = $"Pattern {recentPattern} → {completionMove} seen {mostFrequent.Value} times"
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
            Reasoning = "No n-gram patterns detected"
        };//
    }
}
