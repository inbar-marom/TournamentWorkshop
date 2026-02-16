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

    /// <summary>
    /// Selects the top N scorers from aggregate standings.
    /// If there's a tie at the cutoff position, includes all tied bots.
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <param name="topN">Number of top scorers to select</param>
    /// <returns>List of bot names for top scorers (may be more than topN if tied)</returns>
    public static List<string> SelectTopScorers(Dictionary<string, int> standings, int topN)
    {
        if (standings == null || standings.Count == 0 || topN <= 0)
        {
            return new List<string>();
        }

        // Sort by score descending
        var sorted = standings
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        // If requesting more than available, return all
        if (topN >= sorted.Count)
        {
            return sorted.Select(kvp => kvp.Key).ToList();
        }

        // Get the score of the Nth bot (cutoff score)
        var cutoffScore = sorted[topN - 1].Value;

        // Include all bots with score >= cutoff (handles ties)
        var topScorers = sorted
            .Where(kvp => kvp.Value >= cutoffScore)
            .Select(kvp => kvp.Key)
            .ToList();

        return topScorers;
    }

    /// <summary>
    /// Ranks all bots by their scores in descending order.
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <returns>Ranked list of (BotName, Score) tuples</returns>
    public static List<(string BotName, int Score)> RankByScore(Dictionary<string, int> standings)
    {
        if (standings == null || standings.Count == 0)
        {
            return new List<(string, int)>();
        }

        return standings
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    /// <summary>
    /// Determines the champion from aggregate scores.
    /// Returns null if there's a tie for first place (requiring tiebreaker).
    /// </summary>
    /// <param name="standings">Dictionary mapping bot names to their scores</param>
    /// <returns>Champion bot name, or null if tied</returns>
    public static string? DetermineChampion(Dictionary<string, int> standings)
    {
        if (standings == null || standings.Count == 0)
        {
            return null;
        }

        var maxScore = standings.Values.Max();
        var topScorers = standings.Where(kvp => kvp.Value == maxScore).ToList();

        // If multiple bots tied for first, need tiebreaker
        if (topScorers.Count > 1)
        {
            return null;
        }

        return topScorers.First().Key;
    }
}
