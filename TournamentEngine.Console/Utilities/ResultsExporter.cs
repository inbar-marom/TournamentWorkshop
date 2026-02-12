namespace TournamentEngine.Console.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TournamentEngine.Console.Configuration;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Tournament;

/// <summary>
/// Exports tournament series results to JSON files for archiving, audit trails, and reproducibility
/// </summary>
public class ResultsExporter
{
    private readonly ILogger<ResultsExporter> _logger;
    private readonly TournamentConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public ResultsExporter(ILogger<ResultsExporter> logger, TournamentConfiguration config)
    {
        _logger = logger;
        _config = config;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Export a completed tournament series to JSON file
    /// </summary>
    public async Task<bool> ExportSeriesAsync(TournamentSeriesInfo seriesInfo)
    {
        if (seriesInfo == null)
        {
            _logger.LogError("Cannot export null tournament series");
            return false;
        }

        if (!_config.TournamentSeries?.ExportResults ?? false)
        {
            _logger.LogInformation("Results export is disabled in configuration");
            return true;
        }

        try
        {
            var resultsDir = _config.TournamentEngine?.ResultsDirectory ?? "./results";
            Directory.CreateDirectory(resultsDir);

            var seriesExport = CreateSeriesExport(seriesInfo);
            var filename = GenerateFilename(seriesInfo.SeriesId);
            var filepath = Path.Combine(resultsDir, filename);

            var json = JsonSerializer.Serialize(seriesExport, _jsonOptions);
            await File.WriteAllTextAsync(filepath, json);

            _logger.LogInformation("Tournament series results exported to: {FilePath}", filepath);
            _logger.LogInformation("Series ID: {SeriesId}, Champion: {Champion}, Total Matches: {TotalMatches}",
                seriesInfo.SeriesId, 
                seriesInfo.Tournaments.LastOrDefault()?.Champion ?? "N/A",
                seriesInfo.Tournaments.Sum(t => t.MatchResults.Count));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export tournament series results");
            return false;
        }
    }

    /// <summary>
    /// Create structured export object from series info
    /// </summary>
    private SeriesExport CreateSeriesExport(TournamentSeriesInfo seriesInfo)
    {
        var tournaments = seriesInfo.Tournaments.Select((t, index) =>
        {
            var standings = new List<StandingExport>();
            // Create standings based on match results
            foreach (var match in t.MatchResults)
            {
                AddToStandings(standings, match.Bot1Name, match.Outcome == MatchOutcome.Player1Wins ? 3 : (match.Outcome == MatchOutcome.Draw ? 1 : 0));
                AddToStandings(standings, match.Bot2Name, match.Outcome == MatchOutcome.Player2Wins ? 3 : (match.Outcome == MatchOutcome.Draw ? 1 : 0));
            }

            return new TournamentExport
            {
                TournamentIndex = index + 1,
                TournamentId = t.TournamentId,
                GameType = t.GameType.ToString(),
                Champion = t.Champion,
                TotalMatches = t.MatchResults.Count,
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                Matches = t.MatchResults.Select(m => new MatchExport
                {
                    MatchId = Guid.NewGuid().ToString(),
                    Bot1Name = m.Bot1Name,
                    Bot2Name = m.Bot2Name,
                    Bot1Score = m.Bot1Score,
                    Bot2Score = m.Bot2Score,
                    Outcome = m.Outcome.ToString(),
                    WinnerName = m.WinnerName,
                    Duration = m.Duration.TotalSeconds
                }).ToList(),
                FinalStandings = standings.OrderByDescending(s => s.Points).ToList()
            };
        }).ToList();

        // Calculate overall standings across all tournaments
        var overallStandings = CalculateOverallStandings(tournaments);

        return new SeriesExport
        {
            SeriesId = seriesInfo.SeriesId,
            SeriesName = _config.TournamentSeries?.Name ?? "Tournament Series",
            ExportedAt = DateTime.UtcNow,
            StartTime = seriesInfo.Tournaments.FirstOrDefault()?.StartTime,
            EndTime = seriesInfo.Tournaments.LastOrDefault()?.EndTime,
            TotalMatches = seriesInfo.Tournaments.Sum(t => t.MatchResults.Count),
            Champion = seriesInfo.Tournaments.LastOrDefault()?.Champion,
            TournamentCount = seriesInfo.Tournaments.Count,
            GameTypes = seriesInfo.Tournaments.Select(t => t.GameType.ToString()).Distinct().ToList(),
            Tournaments = tournaments,
            OverallStandings = overallStandings
        };
    }

    /// <summary>
    /// Calculate cumulative standings across all tournaments
    /// </summary>
    private List<StandingExport> CalculateOverallStandings(List<TournamentExport> tournaments)
    {
        var standings = new Dictionary<string, StandingExport>();

        foreach (var tournament in tournaments)
        {
            if (tournament.FinalStandings == null)
                continue;

            foreach (var standing in tournament.FinalStandings)
            {
                if (standing.BotName == null)
                    continue;

                if (!standings.ContainsKey(standing.BotName))
                {
                    standings[standing.BotName] = new StandingExport
                    {
                        BotName = standing.BotName,
                        Points = 0,
                        Wins = 0,
                        Losses = 0,
                        Draws = 0,
                        TournamentWins = 0
                    };
                }

                standings[standing.BotName].Points += standing.Points;
                standings[standing.BotName].Wins += standing.Wins;
                standings[standing.BotName].Losses += standing.Losses;
                standings[standing.BotName].Draws += standing.Draws;

                if (standing.BotName == tournament.Champion)
                {
                    standings[standing.BotName].TournamentWins++;
                }
            }
        }

        return standings.Values.OrderByDescending(s => s.Points).ToList();
    }

    /// <summary>
    /// Add or update bot standings
    /// </summary>
    private void AddToStandings(List<StandingExport> standings, string botName, int points)
    {
        var standing = standings.FirstOrDefault(s => s.BotName == botName);
        if (standing == null)
        {
            standings.Add(new StandingExport
            {
                BotName = botName,
                Points = points,
                Wins = points == 3 ? 1 : 0,
                Draws = points == 1 ? 1 : 0,
                Losses = points == 0 ? 1 : 0,
                TournamentWins = 0
            });
        }
        else
        {
            standing.Points += points;
            if (points == 3) standing.Wins++;
            else if (points == 1) standing.Draws++;
            else standing.Losses++;
        }
    }

    /// <summary>
    /// Generate timestamped filename for results
    /// </summary>
    private string GenerateFilename(string seriesId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var shortId = seriesId.Substring(0, 8);
        return $"tournament_series_{timestamp}_{shortId}.json";
    }
}

/// <summary>
/// Structured export format for a complete tournament series
/// </summary>
public class SeriesExport
{
    [JsonPropertyName("seriesId")]
    public string? SeriesId { get; set; }

    [JsonPropertyName("seriesName")]
    public string? SeriesName { get; set; }

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("champion")]
    public string? Champion { get; set; }

    [JsonPropertyName("tournamentCount")]
    public int TournamentCount { get; set; }

    [JsonPropertyName("gameTypes")]
    public List<string>? GameTypes { get; set; }

    [JsonPropertyName("tournaments")]
    public List<TournamentExport>? Tournaments { get; set; }

    [JsonPropertyName("overallStandings")]
    public List<StandingExport>? OverallStandings { get; set; }
}

/// <summary>
/// Structured export format for a single tournament
/// </summary>
public class TournamentExport
{
    [JsonPropertyName("tournamentIndex")]
    public int TournamentIndex { get; set; }

    [JsonPropertyName("tournamentId")]
    public string? TournamentId { get; set; }

    [JsonPropertyName("gameType")]
    public string? GameType { get; set; }

    [JsonPropertyName("champion")]
    public string? Champion { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("matches")]
    public List<MatchExport>? Matches { get; set; }

    [JsonPropertyName("finalStandings")]
    public List<StandingExport>? FinalStandings { get; set; }
}

/// <summary>
/// Structured export format for a single match result
/// </summary>
public class MatchExport
{
    [JsonPropertyName("matchId")]
    public string? MatchId { get; set; }

    [JsonPropertyName("bot1Name")]
    public string? Bot1Name { get; set; }

    [JsonPropertyName("bot2Name")]
    public string? Bot2Name { get; set; }

    [JsonPropertyName("bot1Score")]
    public int Bot1Score { get; set; }

    [JsonPropertyName("bot2Score")]
    public int Bot2Score { get; set; }

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("winnerName")]
    public string? WinnerName { get; set; }

    [JsonPropertyName("durationSeconds")]
    public double Duration { get; set; }
}

/// <summary>
/// Structured export format for standings
/// </summary>
public class StandingExport
{
    [JsonPropertyName("botName")]
    public string? BotName { get; set; }

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    [JsonPropertyName("losses")]
    public int Losses { get; set; }

    [JsonPropertyName("draws")]
    public int Draws { get; set; }

    [JsonPropertyName("tournamentWins")]
    public int TournamentWins { get; set; }
}
