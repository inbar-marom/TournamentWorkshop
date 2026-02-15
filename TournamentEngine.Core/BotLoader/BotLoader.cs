using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Loads and compiles bot code from files using Roslyn compilation.
/// Supports single-file and multi-file bots with parallel loading.
/// </summary>
public class BotLoader : IBotLoader
{
    private readonly int _maxDegreeOfParallelism;

    /// <summary>
    /// Creates a new BotLoader with default configuration.
    /// </summary>
    public BotLoader() : this(maxDegreeOfParallelism: 4)
    {
    }

    /// <summary>
    /// Creates a new BotLoader with custom configuration.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">Maximum number of bots to load concurrently (default: 4)</param>
    public BotLoader(int maxDegreeOfParallelism = 4)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }
    public async Task<List<BotInfo>> LoadBotsFromDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate directory exists
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Bot directory not found: {directory}");
            }

            // Get all subdirectories (each represents a team folder)
            var teamFolders = Directory.GetDirectories(directory);

            // If directory is empty, return empty list
            if (teamFolders.Length == 0)
            {
                return new List<BotInfo>();
            }

            // Use thread-safe collection for parallel loading results
            var results = new ConcurrentBag<BotInfo>();

            // Configure parallel options
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            // Load each bot folder in parallel
            await Parallel.ForEachAsync(teamFolders, parallelOptions, async (teamFolder, ct) =>
            {
                try
                {
                    var botInfo = await LoadBotFromFolderAsync(teamFolder, ct);
                    results.Add(botInfo);
                }
                catch (Exception ex)
                {
                    // If loading fails catastrophically, create a BotInfo with error
                    var folderName = Path.GetFileName(teamFolder);
                    var teamName = Regex.Replace(folderName, @"_v\d+$", "");
                    
                    results.Add(new BotInfo
                    {
                        TeamName = teamName,
                        FolderPath = teamFolder,
                        IsValid = false,
                        ValidationErrors = new List<string> { $"Failed to load bot: {ex.Message}" }
                    });
                }
            });

            return results.ToList();
        }
        catch (DirectoryNotFoundException)
        {
            // Re-throw directory not found
            throw;
        }
        catch (Exception ex)
        {
            // Wrap other exceptions with generic team name
            throw new BotLoadException("Unknown", $"Failed to load bots from directory: {ex.Message}", ex);
        }
    }

    public async Task<BotInfo> LoadBotFromFolderAsync(string teamFolder, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        try
        {
            // Extract team name from folder path (e.g., "Alpha_Team_001_v2" -> "Alpha_Team_001")
            var folderName = Path.GetFileName(teamFolder);
            var teamName = Regex.Replace(folderName, @"_v\d+$", "");

            // 1. Collect all .cs files from team folder
            if (!Directory.Exists(teamFolder))
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { $"Team folder not found: {teamFolder}" }
                };
            }

            var csFiles = Directory.GetFiles(teamFolder, "*.cs");
            if (csFiles.Length == 0)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { "No .cs files found in team folder" }
                };
            }

            // 2. Validate total file size (200KB limit)
            const long MaxTotalSizeBytes = 200 * 1024; // 200KB
            long totalSize = 0;
            foreach (var filePath in csFiles)
            {
                var fileInfo = new FileInfo(filePath);
                totalSize += fileInfo.Length;
            }

            if (totalSize > MaxTotalSizeBytes)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> 
                    { 
                        $"Total bot code size ({totalSize / 1024}KB) exceeds the 200KB limit" 
                    }
                };
            }

            // 3. Parse each file into a syntax tree and validate namespaces
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in csFiles)
            {
                var code = await File.ReadAllTextAsync(filePath, cancellationToken);
                var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath, cancellationToken: cancellationToken);
                syntaxTrees.Add(syntaxTree);

                // Validate namespaces in this file
                var namespaceErrors = ValidateNamespaces(code, Path.GetFileName(filePath));
                if (namespaceErrors.Count > 0)
                {
                    return new BotInfo
                    {
                        TeamName = teamName,
                        FolderPath = teamFolder,
                        IsValid = false,
                        ValidationErrors = namespaceErrors
                    };
                }
            }

            // 4. Get metadata references for compilation
            var references = GetMetadataReferences();

            // 5. Create compilation with ALL files together
            var assemblyName = $"{teamName}_Bot_{Guid.NewGuid()}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: false
                )
            );

            // 6. Compile to in-memory assembly
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

            if (!emitResult.Success)
            {
                // Collect compilation errors
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        errors.Add($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                    }
                }

                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = errors
                };
            }

            // 7. Load assembly and find IBot implementation
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            
            var botTypes = assembly.GetTypes()
                .Where(t => typeof(IBot).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (botTypes.Count == 0)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { "No class implementing IBot found in bot code" }
                };
            }

            if (botTypes.Count > 1)
            {
                var typeNames = string.Join(", ", botTypes.Select(t => t.Name));
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> 
                    { 
                        $"Multiple IBot implementations found: {typeNames}. Only one IBot implementation is allowed." 
                    }
                };
            }

            var botType = botTypes[0];

            // 8. Create bot instance
            var botInstance = (IBot?)Activator.CreateInstance(botType);
            if (botInstance == null)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { "Failed to create bot instance" }
                };
            }

            // 9. Return successful BotInfo
            return new BotInfo
            {
                TeamName = teamName,
                FolderPath = teamFolder,
                BotInstance = botInstance,
                IsValid = true,
                ValidationErrors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Exception during bot loading: {ex.Message}");
            return new BotInfo
            {
                TeamName = Path.GetFileName(teamFolder).Split('_')[0],
                FolderPath = teamFolder,
                IsValid = false,
                ValidationErrors = errors
            };
        }
    }

    public BotValidationResult ValidateBotCode(Dictionary<string, string> files)
    {
        throw new NotImplementedException("ValidateBotCode not yet implemented");
    }

    /// <summary>
    /// Validates that the code doesn't use blocked namespaces
    /// </summary>
    private static List<string> ValidateNamespaces(string code, string fileName)
    {
        var errors = new List<string>();
        
        // List of blocked namespaces
        var blockedNamespaces = new[]
        {
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Runtime.InteropServices"
        };

        // Check for using statements with blocked namespaces
        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Check if line is a using statement
            if (line.StartsWith("using ") && line.Contains(";"))
            {
                foreach (var blockedNs in blockedNamespaces)
                {
                    // Check if the using statement references the blocked namespace
                    // Pattern: "using System.IO;" or "using System.IO.File;"
                    if (line.Contains(blockedNs))
                    {
                        errors.Add($"Blocked namespace '{blockedNs}' detected in file '{fileName}' at line {i + 1}");
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Gets metadata references needed for bot compilation
    /// </summary>
    private static List<MetadataReference> GetMetadataReferences()
    {
        var refs = new List<MetadataReference>();

        // Add core assemblies
        refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)); // System.Private.CoreLib
        refs.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)); // System.Console
        refs.Add(MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location)); // System.Collections
        refs.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)); // System.Linq
        
        // Add runtime assemblies
        refs.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
        refs.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
        
        // Add TournamentEngine.Core (for IBot, GameState, etc.)
        refs.Add(MetadataReference.CreateFromFile(typeof(IBot).Assembly.Location));

        return refs;
    }
}
