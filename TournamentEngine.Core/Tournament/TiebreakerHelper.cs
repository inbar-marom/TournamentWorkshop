namespace TournamentEngine.Core.Tournament;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Helper class for detecting and resolving tiebreakers in tournament standings
/// </summary>
public static class TiebreakerHelper
{
    /// <summary>
    /// Detects groups of bots that are tied (have the same score).
    /// Returns a list of tie groups, where each group contains 2+ bots with identical scores.
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <returns>List of tie groups, where each group is a list of bot names with the same score</returns>
    public static List<List<string>> DetectTies(Dictionary<string, int> standings)
    {
        if (standings == null || standings.Count == 0)
        {
            return new List<List<string>>();
        }

        // Group bots by their scores
        var groupedByScore = standings
            .GroupBy(kvp => kvp.Value)  // Group by score
            .Where(g => g.Count() >= 2)  // Only keep groups with 2+ bots (ties)
            .Select(g => g.Select(kvp => kvp.Key).ToList())  // Extract bot names
            .ToList();

        return groupedByScore;
    }

    /// <summary>
    /// Determines if a tiebreaker is needed among the top scorers.
    /// This is specifically for determining the winner when multiple bots have the highest score.
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <returns>True if top scorers are tied, false otherwise</returns>
    public static bool IsTopScoreTied(Dictionary<string, int> standings)
    {
        if (standings == null || standings.Count < 2)
        {
            return false;
        }

        var maxScore = standings.Values.Max();
        var topScorers = standings.Where(kvp => kvp.Value == maxScore).ToList();

        return topScorers.Count >= 2;
    }

    /// <summary>
    /// Gets the list of bot names that are tied for the top score.
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <returns>List of bot names tied for first place, or empty if no tie</returns>
    public static List<string> GetTopScorersTied(Dictionary<string, int> standings)
    {
        if (standings == null || standings.Count == 0)
        {
            return new List<string>();
        }

        var maxScore = standings.Values.Max();
        var topScorers = standings
            .Where(kvp => kvp.Value == maxScore)
            .Select(kvp => kvp.Key)
            .ToList();

        // Only return if there's actually a tie (2+ bots)
        return topScorers.Count >= 2 ? topScorers : new List<string>();
    }
}
