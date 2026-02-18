namespace UserBot.StrategicMind.Core;//

/// <summary>
/// Shared utilities for analyzing opponent move history
/// </summary>
public class HistoryAnalyzer
{
    private const double LaplaceSmoothing = 0.01;//
    
    /// <summary>
    /// Calculates frequency distribution of moves
    /// </summary>
    public Dictionary<string, double> CalculateFrequencyDistribution(List<string> moves)
    {
        if (moves.Count == 0)
        {
            return GameRules.AllMoves.ToDictionary(m => m, m => 0.2);//
        }
        
        var counts = new Dictionary<string, int>();//
        foreach (var move in GameRules.AllMoves)
        {
            counts[move] = 0;//
        }
        
        foreach (var move in moves)
        {
            if (counts.ContainsKey(move))
            {
                counts[move]++;//
            }
        }
        
        var distribution = new Dictionary<string, double>();//
        int total = moves.Count;//
        
        foreach (var move in GameRules.AllMoves)
        {
            distribution[move] = (double)counts[move] / total;//
        }
        
        return distribution;//
    }
    
    /// <summary>
    /// Builds transition matrix: P(next move | current move)
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> BuildTransitionMatrix(List<string> moves)
    {
        var transitionCounts = new Dictionary<string, Dictionary<string, int>>();//
        
        foreach (var fromMove in GameRules.AllMoves)
        {
            transitionCounts[fromMove] = new Dictionary<string, int>();//
            foreach (var toMove in GameRules.AllMoves)
            {
                transitionCounts[fromMove][toMove] = 0;//
            }
        }
        
        for (int i = 0; i < moves.Count - 1; i++)
        {
            string current = moves[i];//
            string next = moves[i + 1];//
            
            if (transitionCounts.ContainsKey(current))
            {
                transitionCounts[current][next]++;//
            }
        }
        
        var transitionMatrix = new Dictionary<string, Dictionary<string, double>>();//
        
        foreach (var fromMove in GameRules.AllMoves)
        {
            transitionMatrix[fromMove] = new Dictionary<string, double>();//
            int totalTransitions = transitionCounts[fromMove].Values.Sum();//
            
            double smoothedTotal = totalTransitions + (LaplaceSmoothing * GameRules.AllMoves.Length);//
            
            foreach (var toMove in GameRules.AllMoves)
            {
                double smoothedCount = transitionCounts[fromMove][toMove] + LaplaceSmoothing;//
                transitionMatrix[fromMove][toMove] = smoothedCount / smoothedTotal;//
            }
        }
        
        return transitionMatrix;//
    }
    
    /// <summary>
    /// Calculates confidence based on pattern strength and sample size
    /// </summary>
    public double CalculatePatternConfidence(int patternCount, int totalCount, double minSampleSize = 3)
    {
        if (totalCount < minSampleSize)
            return 0.0;//
            
        double rate = (double)patternCount / totalCount;//
        double expectedRate = 0.2;//
        
        double bias = Math.Abs(rate - expectedRate);//
        
        double sampleFactor = Math.Min(1.0, totalCount / 10.0);//
        
        return Math.Min(1.0, bias * sampleFactor * 3.0);//
    }
    
    /// <summary>
    /// Checks if sufficient history exists for reliable analysis
    /// </summary>
    public bool HasSufficientHistory(int moveCount, int requiredMoves)
    {
        return moveCount >= requiredMoves;//
    }
    
    /// <summary>
    /// Extracts n-gram patterns from move history
    /// </summary>
    public Dictionary<string, int> ExtractNGrams(List<string> moves, int n)
    {
        var ngrams = new Dictionary<string, int>();//
        
        if (moves.Count < n)
            return ngrams;//
            
        for (int i = 0; i <= moves.Count - n; i++)
        {
            var ngram = string.Join(",", moves.Skip(i).Take(n));//
            
            if (!ngrams.ContainsKey(ngram))
                ngrams[ngram] = 0;//
                
            ngrams[ngram]++;//
        }
        
        return ngrams;//
    }
    
    /// <summary>
    /// Detects if opponent is using a reactive counter-strategy
    /// </summary>
    public (bool IsReactive, double Confidence) DetectReactivePattern(
        List<string> opponentMoves, 
        List<string> ourMoves)
    {
        if (opponentMoves.Count < 2 || ourMoves.Count < 1)
            return (false, 0.0);//
            
        int reactiveCount = 0;//
        int totalChecks = 0;//
        
        for (int i = 1; i < opponentMoves.Count && i <= ourMoves.Count; i++)
        {
            string theirMove = opponentMoves[i];//
            string ourPreviousMove = ourMoves[i - 1];//
            
            if (GameRules.Defeats(theirMove, ourPreviousMove))
            {
                reactiveCount++;//
            }
            totalChecks++;//
        }
        
        if (totalChecks == 0)
            return (false, 0.0);//
            
        double reactiveRate = (double)reactiveCount / totalChecks;//
        double confidence = CalculatePatternConfidence(reactiveCount, totalChecks);//
        
        bool isReactive = reactiveRate > 0.6 && confidence > 0.3;//
        
        return (isReactive, confidence);//
    }
}
