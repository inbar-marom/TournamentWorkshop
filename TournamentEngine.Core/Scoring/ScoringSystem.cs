namespace TournamentEngine.Core.Scoring;

using System;
using System.Collections.Generic;
using System.Linq;
using TournamentEngine.Core.Common;

/// <summary>
/// Default scoring system implementation
/// </summary>
public sealed class ScoringSystem : IScoringSystem
{
    public (int player1Score, int player2Score) CalculateMatchScore(MatchResult matchResult)
    {
        if (matchResult == null)
            throw new ArgumentNullException(nameof(matchResult));

        return matchResult.Outcome switch
        {
            MatchOutcome.Player1Wins => (3, 0),
            MatchOutcome.Player2Wins => (0, 3),
            MatchOutcome.Draw => (1, 1),
            MatchOutcome.Player1Error => (0, 3),
            MatchOutcome.Player2Error => (3, 0),
            MatchOutcome.BothError => (0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(matchResult.Outcome), matchResult.Outcome, "Unknown match outcome")
        };
    }

    public Dictionary<string, TournamentStanding> UpdateStandings(MatchResult matchResult, Dictionary<string, TournamentStanding> currentStandings)
    {
        if (matchResult == null)
            throw new ArgumentNullException(nameof(matchResult));
        if (currentStandings == null)
            throw new ArgumentNullException(nameof(currentStandings));

        var bot1 = GetOrCreateStanding(currentStandings, matchResult.Bot1Name);
        var bot2 = GetOrCreateStanding(currentStandings, matchResult.Bot2Name);

        var (player1Score, player2Score) = CalculateMatchScore(matchResult);
        bot1.TotalScore += player1Score;
        bot1.TotalOpponentScore += player2Score;
        bot2.TotalScore += player2Score;
        bot2.TotalOpponentScore += player1Score;

        switch (matchResult.Outcome)
        {
            case MatchOutcome.Player1Wins:
            case MatchOutcome.Player2Error:
                bot1.Wins++;
                bot2.Losses++;
                break;
            case MatchOutcome.Player2Wins:
            case MatchOutcome.Player1Error:
                bot2.Wins++;
                bot1.Losses++;
                break;
            case MatchOutcome.Draw:
            case MatchOutcome.BothError:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(matchResult.Outcome), matchResult.Outcome, "Unknown match outcome");
        }

        AddOpponent(bot1.OpponentsPlayed, matchResult.Bot2Name);
        AddOpponent(bot2.OpponentsPlayed, matchResult.Bot1Name);

        return currentStandings;
    }

    public List<BotRanking> GenerateFinalRankings(TournamentInfo tournamentInfo)
    {
        if (tournamentInfo == null)
            throw new ArgumentNullException(nameof(tournamentInfo));

        var current = GetCurrentRankings(tournamentInfo);
        var finalRankings = new List<BotRanking>(current.Count);

        for (int i = 0; i < current.Count; i++)
        {
            var ranking = current[i];
            finalRankings.Add(new BotRanking
            {
                BotName = ranking.BotName,
                FinalPlacement = i + 1,
                Wins = ranking.Wins,
                Losses = ranking.Losses,
                TotalScore = ranking.TotalScore,
                EliminationRound = ranking.EliminationRound,
                EliminatedBy = ranking.EliminatedBy,
                TotalPlayTime = ranking.TotalPlayTime,
                OpponentsDefeated = ranking.OpponentsDefeated
            });
        }

        return finalRankings;
    }

    public TournamentStatistics CalculateStatistics(TournamentInfo tournamentInfo)
    {
        if (tournamentInfo == null)
            throw new ArgumentNullException(nameof(tournamentInfo));

        var matchResults = tournamentInfo.MatchResults ?? new List<MatchResult>();
        var totalMatches = matchResults.Count;
        var totalRounds = tournamentInfo.TotalRounds > 0 ? tournamentInfo.TotalRounds : tournamentInfo.CurrentRound;

        var tournamentDuration = tournamentInfo.EndTime.HasValue
            ? tournamentInfo.EndTime.Value - tournamentInfo.StartTime
            : TimeSpan.Zero;

        var averageDuration = totalMatches == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)matchResults.Average(m => m.Duration.Ticks));

        var totalErrors = matchResults.Sum(m => m.Errors.Count);
        var totalTimeouts = matchResults.Sum(m => m.Errors.Count(e => e.Contains("timeout", StringComparison.OrdinalIgnoreCase)));

        var matchesByGame = matchResults
            .GroupBy(m => m.GameType)
            .ToDictionary(g => g.Key, g => g.Count());

        var activityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matchResults)
        {
            IncrementCount(activityCounts, match.Bot1Name);
            IncrementCount(activityCounts, match.Bot2Name);
        }

        var mostActiveBot = activityCounts.Count == 0
            ? null
            : activityCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase).First().Key;

        var standings = new Dictionary<string, TournamentStanding>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matchResults)
        {
            if (IsTiebreakMatch(match))
                continue;

            UpdateStandings(match, standings);
        }

        var highestScoringBot = standings.Count == 0
            ? null
            : standings.Values
                .OrderByDescending(s => s.TotalScore)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.BotName, StringComparer.OrdinalIgnoreCase)
                .First().BotName;

        var errorsByBot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matchResults)
        {
            switch (match.Outcome)
            {
                case MatchOutcome.Player1Error:
                    IncrementCount(errorsByBot, match.Bot1Name);
                    break;
                case MatchOutcome.Player2Error:
                    IncrementCount(errorsByBot, match.Bot2Name);
                    break;
                case MatchOutcome.BothError:
                    IncrementCount(errorsByBot, match.Bot1Name);
                    IncrementCount(errorsByBot, match.Bot2Name);
                    break;
            }
        }

        return new TournamentStatistics
        {
            TotalMatches = totalMatches,
            TotalRounds = totalRounds,
            TournamentDuration = tournamentDuration,
            AverageMatchDuration = averageDuration,
            TotalErrors = totalErrors,
            TotalTimeouts = totalTimeouts,
            MostActiveBot = mostActiveBot,
            HighestScoringBot = highestScoringBot,
            MatchesByGame = matchesByGame,
            ErrorsByBot = errorsByBot
        };
    }

    public List<BotRanking> GetCurrentRankings(TournamentInfo tournamentInfo)
    {
        if (tournamentInfo == null)
            throw new ArgumentNullException(nameof(tournamentInfo));

        var standings = new Dictionary<string, TournamentStanding>(StringComparer.OrdinalIgnoreCase);
        foreach (var bot in tournamentInfo.Bots)
        {
            if (!standings.ContainsKey(bot.TeamName))
                standings[bot.TeamName] = new TournamentStanding { BotName = bot.TeamName };
        }

        foreach (var match in tournamentInfo.MatchResults)
        {
            if (IsTiebreakMatch(match))
                continue;

            UpdateStandings(match, standings);
        }

        var ordered = standings.Values
            .OrderByDescending(s => s.TotalScore)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.Losses)
            .ThenBy(s => s.BotName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rankings = new List<BotRanking>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var standing = ordered[i];
            rankings.Add(new BotRanking
            {
                BotName = standing.BotName,
                FinalPlacement = i + 1,
                Wins = standing.Wins,
                Losses = standing.Losses,
                TotalScore = standing.TotalScore,
                EliminationRound = standing.EliminationRound,
                TotalPlayTime = TimeSpan.Zero
            });
        }

        return rankings;
    }

    private static TournamentStanding GetOrCreateStanding(Dictionary<string, TournamentStanding> standings, string botName)
    {
        if (!standings.TryGetValue(botName, out var standing))
        {
            standing = new TournamentStanding { BotName = botName };
            standings[botName] = standing;
        }

        return standing;
    }

    private static void AddOpponent(List<string> opponents, string opponentName)
    {
        if (!opponents.Any(name => string.Equals(name, opponentName, StringComparison.OrdinalIgnoreCase)))
            opponents.Add(opponentName);
    }

    private static void IncrementCount(Dictionary<string, int> counts, string key)
    {
        if (counts.TryGetValue(key, out var current))
            counts[key] = current + 1;
        else
            counts[key] = 1;
    }

    private static bool IsTiebreakMatch(MatchResult match)
    {
        return !string.IsNullOrWhiteSpace(match.GroupLabel)
            && match.GroupLabel.IndexOf("tiebreak", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
