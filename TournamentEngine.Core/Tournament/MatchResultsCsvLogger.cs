namespace TournamentEngine.Core.Tournament;

using System.Text;
using System.Text.Json;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Utilities;

public sealed class MatchResultsCsvLogger : IMatchResultsLogger
{
    private const string Header = "GameType,PlayerA,PlayerB,Group,StartTimeIsrael,DurationMs,MatchOutcome,SubActsJson";
    private readonly object _fileLock = new();
    private readonly string _outputDirectory;
    private readonly string _filePrefix;
    private string? _currentFilePath;

    public MatchResultsCsvLogger(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("CSV file path cannot be empty", nameof(filePath));

        _outputDirectory = Path.GetDirectoryName(filePath) ?? ".";
        _filePrefix = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(_filePrefix))
        {
            _filePrefix = "match-results";
        }
    }

    public void StartTournamentRun(string tournamentId, GameType gameType)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new ArgumentException("Tournament ID cannot be empty", nameof(tournamentId));

        lock (_fileLock)
        {
            Directory.CreateDirectory(_outputDirectory);

            var safeTournamentId = tournamentId.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var fileName = $"{_filePrefix}_{gameType}_{safeTournamentId}_{timestamp}.csv";
            _currentFilePath = Path.Combine(_outputDirectory, fileName);
        }
    }

    public void AppendMatchResult(MatchResult matchResult, string groupLabel)
    {
        if (matchResult == null)
            throw new ArgumentNullException(nameof(matchResult));

        lock (_fileLock)
        {
            EnsureRunFileInitialized(matchResult.GameType);

            using var fileStream = new FileStream(_currentFilePath!, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var writer = new StreamWriter(fileStream, Encoding.UTF8, leaveOpen: true);

            if (fileStream.Length == 0)
            {
                writer.WriteLine(Header);
            }

            fileStream.Seek(0, SeekOrigin.End);

            var subActs = JsonSerializer.Serialize(matchResult.MatchLog ?? new List<string>());
            var israelTime = TimezoneHelper.FormatIsraelTimeForCsv(matchResult.StartTime.ToUniversalTime());
            var row = string.Join(',', new[]
            {
                EscapeCsv(matchResult.GameType.ToString()),
                EscapeCsv(matchResult.Bot1Name),
                EscapeCsv(matchResult.Bot2Name),
                EscapeCsv(groupLabel),
                EscapeCsv(israelTime),
                EscapeCsv(((long)matchResult.Duration.TotalMilliseconds).ToString()),
                EscapeCsv(((int)matchResult.Outcome).ToString()),
                EscapeCsv(subActs)
            });

            writer.WriteLine(row);
            writer.Flush();
        }
    }

    private void EnsureRunFileInitialized(GameType gameType)
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
            return;

        var fallbackRunId = $"adhoc_{Guid.NewGuid():N}";
        StartTournamentRun(fallbackRunId, gameType);
    }

    private static string EscapeCsv(string? value)
    {
        if (value == null)
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
