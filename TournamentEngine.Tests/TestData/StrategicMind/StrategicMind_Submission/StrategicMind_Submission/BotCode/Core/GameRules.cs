namespace UserBot.StrategicMind.Core;//

/// <summary>
/// Encapsulates all RPSLS game logic and move relationships
/// </summary>
public static class GameRules
{
    /// <summary>
    /// All valid moves in RPSLS
    /// </summary>
    public static readonly string[] AllMoves = { "Rock", "Paper", "Scissors", "Lizard", "Spock" };//
    
    /// <summary>
    /// Win relationships: each move defeats exactly 2 others
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> WinRelationships = new()
    {
        { "Rock", new HashSet<string> { "Scissors", "Lizard" } },
        { "Paper", new HashSet<string> { "Rock", "Spock" } },
        { "Scissors", new HashSet<string> { "Paper", "Lizard" } },
        { "Lizard", new HashSet<string> { "Paper", "Spock" } },
        { "Spock", new HashSet<string> { "Rock", "Scissors" } }
    };//
    
    /// <summary>
    /// Determines if move1 defeats move2
    /// </summary>
    public static bool Defeats(string move1, string move2)
    {
        if (!WinRelationships.ContainsKey(move1))
            return false;//
            
        return WinRelationships[move1].Contains(move2);//
    }
    
    /// <summary>
    /// Gets all moves that counter (defeat) the given move
    /// </summary>
    public static List<string> GetCountersTo(string move)
    {
        return WinRelationships
            .Where(kvp => kvp.Value.Contains(move))
            .Select(kvp => kvp.Key)
            .ToList();//
    }
    
    /// <summary>
    /// Gets the best counter to a predicted move (randomly selects from valid counters)
    /// </summary>
    public static string GetBestCounterTo(string predictedMove, Random? random = null)
    {
        var counters = GetCountersTo(predictedMove);//
        if (counters.Count == 0)
            return AllMoves[0];//
            
        random ??= new Random();//
        return counters[random.Next(counters.Count)];//
    }
    
    /// <summary>
    /// Calculates expected value of playing a specific move against an opponent distribution
    /// </summary>
    public static double CalculateExpectedValue(string ourMove, Dictionary<string, double> opponentDistribution)
    {
        double expectedValue = 0.0;//
        
        foreach (var (opponentMove, probability) in opponentDistribution)
        {
            if (Defeats(ourMove, opponentMove))
            {
                expectedValue += probability;//
            }
            else if (Defeats(opponentMove, ourMove))
            {
                expectedValue -= probability;//
            }
        }
        
        return expectedValue;//
    }
    
    /// <summary>
    /// Determines the optimal move against a predicted opponent distribution
    /// </summary>
    public static string GetOptimalMove(Dictionary<string, double> opponentDistribution, Random? random = null)
    {
        var moveValues = new Dictionary<string, double>();//
        
        foreach (var move in AllMoves)
        {
            moveValues[move] = CalculateExpectedValue(move, opponentDistribution);//
        }
        
        var maxValue = moveValues.Values.Max();//
        var bestMoves = moveValues.Where(kvp => Math.Abs(kvp.Value - maxValue) < 0.001).Select(kvp => kvp.Key).ToList();//
        
        random ??= new Random();//
        return bestMoves[random.Next(bestMoves.Count)];//
    }
}
