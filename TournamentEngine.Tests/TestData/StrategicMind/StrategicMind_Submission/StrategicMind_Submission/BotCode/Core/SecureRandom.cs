namespace UserBot.StrategicMind.Core;//

/// <summary>
/// Random number generation for bot strategy
/// Uses standard System.Random for game-appropriate randomness
/// </summary>
public class SecureRandom
{
    private readonly Random _random = new();//

    /// <summary>
    /// Generates a random integer in [minValue, maxValue)
    /// </summary>
    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue");//
            
        return _random.Next(minValue, maxValue);//
    }
    
    /// <summary>
    /// Selects a random element from an array
    /// </summary>
    public T SelectRandom<T>(T[] items)
    {
        if (items.Length == 0)
            throw new ArgumentException("Array cannot be empty");//
            
        int index = Next(0, items.Length);//
        return items[index];//
    }
    
    /// <summary>
    /// Selects a random element based on weighted probabilities
    /// </summary>
    public T SelectWeighted<T>(Dictionary<T, double> weightedItems) where T : notnull
    {
        if (weightedItems.Count == 0)
            throw new ArgumentException("Dictionary cannot be empty");//
            
        double totalWeight = weightedItems.Values.Sum();//
        double randomValue = NextDouble() * totalWeight;//
        
        double cumulative = 0.0;//
        foreach (var (item, weight) in weightedItems)
        {
            cumulative += weight;//
            if (randomValue < cumulative)
                return item;//
        }
        
        return weightedItems.Keys.Last();//
    }
    
    /// <summary>
    /// Generates a random double in [0.0, 1.0)
    /// </summary>
    public double NextDouble()
    {
        return _random.NextDouble();//
    }
}
