namespace TournamentEngine.Api.Utilities;

using System.Text.RegularExpressions;
using Models;

public static class BotFilePathNormalizer
{
    public static List<BotFile> NormalizeAndEnsureUnique(
        IEnumerable<BotFile> files,
        string teamName,
        List<string>? warnings = null)
    {
        var normalizedFiles = new List<BotFile>();
        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var normalizedPath = NormalizePath(file.FileName);

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                normalizedPath = BuildFallbackPath(file, teamName);
            }

            if (!normalizedPath.Contains('/') && normalizedPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var namespacePath = ExtractNamespacePath(file.Code);
                if (!string.IsNullOrWhiteSpace(namespacePath))
                {
                    normalizedPath = $"{namespacePath}/{normalizedPath}";
                }
            }

            normalizedPath = EnsureUniquePath(normalizedPath, usedPaths);

            if (!string.Equals(file.FileName, normalizedPath, StringComparison.Ordinal))
            {
                warnings?.Add($"Normalized file path '{file.FileName}' to '{normalizedPath}'");
            }

            normalizedFiles.Add(new BotFile
            {
                FileName = normalizedPath,
                Code = file.Code
            });
        }

        return normalizedFiles;
    }

    private static string NormalizePath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var normalized = fileName.Replace('\\', '/').Trim();
        normalized = normalized.TrimStart('/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var pathSegments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .Select(segment => segment.Replace(':', '_'))
            .ToList();

        return string.Join('/', pathSegments);
    }

    private static string BuildFallbackPath(BotFile file, string teamName)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = file.Code.Contains("namespace", StringComparison.OrdinalIgnoreCase) ? ".cs" : ".txt";
        }

        return $"{teamName}/Source/Unknown{extension}";
    }

    private static string? ExtractNamespacePath(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var match = Regex.Match(code, @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_\.]*)");
        if (!match.Success)
            return null;

        var namespaceValue = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(namespaceValue))
            return null;

        return namespaceValue.Replace('.', '/');
    }

    private static string EnsureUniquePath(string path, HashSet<string> usedPaths)
    {
        if (usedPaths.Add(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var counter = 2;

        while (true)
        {
            var candidateFile = $"{fileNameWithoutExtension}_{counter}{extension}";
            var candidate = string.IsNullOrWhiteSpace(directory)
                ? candidateFile
                : $"{directory}/{candidateFile}";

            if (usedPaths.Add(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }
}