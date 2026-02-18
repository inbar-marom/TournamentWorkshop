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
    public async Task<List<BotInfo>> LoadBotsFromDirectoryAsync(string directory, TournamentConfig? config = null, CancellationToken cancellationToken = default)
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
                    var botInfo = await LoadBotFromFolderAsync(teamFolder, config, ct);
                    results.Add(botInfo);
                }
                catch (Exception ex)
                {
                    // If loading fails catastrophically, create a BotInfo with error
                    var folderName = Path.GetFileName(teamFolder);
                    var teamName = Regex.Replace(folderName, @"_v\\d+$", "");
                    
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

    public async Task<BotInfo> LoadBotFromFolderAsync(string teamFolder, TournamentConfig? config = null, CancellationToken cancellationToken = default)
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

            var csFiles = Directory.GetFiles(teamFolder, "*.cs", SearchOption.AllDirectories);
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

            // 2. Validate total file size (500KB limit - aligned with API submission limit)
            const long MaxTotalSizeBytes = 500 * 1024; // 500KB
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
                        $"Total bot code size ({totalSize / 1024}KB) exceeds the 500KB limit" 
                    }
                };
            }

            // 3. Add implicit usings as .NET 6+ does with <ImplicitUsings>enable</ImplicitUsings>
            var implicitUsings = @"using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
";

            var syntaxTrees = new List<SyntaxTree>();

            // 3.1. Add UserBot.Core interface definitions if bot uses UserBot.Core namespace
            var userBotCorePath = ResolveUserBotCorePath(teamFolder);
            
            if (userBotCorePath != null && Directory.Exists(userBotCorePath))
            {
                var userBotCoreFiles = new[] { "IBot.cs", "GameState.cs", "GameType.cs" };
                foreach (var fileName in userBotCoreFiles)
                {
                    var filePath = Path.Combine(userBotCorePath, fileName);
                    if (File.Exists(filePath))
                    {
                        var userBotCode = await File.ReadAllTextAsync(filePath, cancellationToken);
                        var codeWithUsings = implicitUsings + "\n" + userBotCode;
                        syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeWithUsings, path: fileName, cancellationToken: cancellationToken));
                    }
                }
            }

            // 4. Parse each file into a syntax tree and validate namespaces
            foreach (var filePath in csFiles)
            {
                var code = await File.ReadAllTextAsync(filePath, cancellationToken);
                
                // Prepend implicit usings to bot files (simulates <ImplicitUsings>enable</ImplicitUsings>)
                var codeWithUsings = implicitUsings + "\n" + code;
                
                var syntaxTree = CSharpSyntaxTree.ParseText(codeWithUsings, path: filePath, cancellationToken: cancellationToken);
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
            
            // CRITICAL: Use IBot type from compiled assembly, not from TournamentEngine.Core
            // The bot may have compiled its own UserBot.Core.IBot which is a different type
            var iBotType = assembly.GetTypes().FirstOrDefault(t => 
                t.Name == "IBot" && (t.Namespace == "UserBot.Core" || t.FullName == "TournamentEngine.Core.Common.IBot"));
            
            if (iBotType == null)
            {
                // Fallback: use TournamentEngine.Core.Common.IBot for old-style bots
                iBotType = typeof(IBot);
            }
            
            var botTypes = assembly.GetTypes()
                .Where(t => iBotType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
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
            var rawBotInstance = Activator.CreateInstance(botType);
            if (rawBotInstance == null)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { "Failed to create bot instance" }
                };
            }

            // 8.1. Use BotAdapterFactory to automatically detect and wrap bots from different namespaces
            // This supports bots using UserBot.Core, CustomBot.Core, or any other IBot implementation
            IBot botInstance;
            try
            {
                botInstance = BotAdapterFactory.CreateAdapterIfNeeded(rawBotInstance);
            }
            catch (Exception ex)
            {
                return new BotInfo
                {
                    TeamName = teamName,
                    FolderPath = teamFolder,
                    IsValid = false,
                    ValidationErrors = new List<string> { $"Failed to create bot adapter: {ex.Message}" }
                };
            }

            // 9. Wrap bot in memory monitor if config provided
            MemoryMonitoredBot? monitoredBot = null;
            if (config != null)
            {
                var memoryLimitBytes = config.MemoryLimitMB * 1024L * 1024L;
                monitoredBot = new MemoryMonitoredBot(botInstance, memoryLimitBytes);
            }

            // 10. Return successful BotInfo
            return new BotInfo
            {
                TeamName = teamName,
                FolderPath = teamFolder,
                BotInstance = botInstance,
                MonitoredInstance = monitoredBot,
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
        refs.Add(MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location)); // System.Net.Http
        
        // Add runtime assemblies
        refs.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
        refs.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
        
        // Add TournamentEngine.Core (for IBot, GameState, etc.)
        refs.Add(MetadataReference.CreateFromFile(typeof(IBot).Assembly.Location));

        return refs;
    }

    /// <summary>
    /// Reloads all bots from their original folder paths.
    /// This resets memory tracking counters and creates fresh bot instances.
    /// </summary>
    /// <param name="existingBots">List of BotInfo instances to reload</param>
    /// <param name="config">Tournament configuration containing memory limits</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of reloaded BotInfo instances</returns>
    public async Task<List<BotInfo>> ReloadAllBotsAsync(List<BotInfo> existingBots, TournamentConfig? config = null, CancellationToken cancellationToken = default)
    {
        var reloadedBots = new List<BotInfo>();

        foreach (var bot in existingBots)
        {
            // Reload bot from its original folder
            var reloadedBot = await LoadBotFromFolderAsync(bot.FolderPath, config, cancellationToken);
            reloadedBots.Add(reloadedBot);
        }

        // Force garbage collection to reclaim memory from old bot instances
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return reloadedBots;
    }

    private static string? ResolveUserBotCorePath(string teamFolder)
    {
        var candidates = new List<string>();

        foreach (var ancestor in EnumerateDirectoryAncestors(teamFolder))
        {
            candidates.Add(Path.Combine(ancestor.FullName, "UserBot", "UserBot.Core"));

            var isBotsDir = string.Equals(ancestor.Name, "bots", StringComparison.OrdinalIgnoreCase);
            if (isBotsDir && ancestor.Parent != null)
            {
                candidates.Add(Path.Combine(ancestor.Parent.FullName, "UserBot", "UserBot.Core"));
            }

            if (File.Exists(Path.Combine(ancestor.FullName, "TournamentEngine.sln")))
            {
                candidates.Add(Path.Combine(ancestor.FullName, "UserBot", "UserBot.Core"));
                break;
            }
        }

        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "UserBot", "UserBot.Core"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "UserBot", "UserBot.Core"));

        foreach (var candidate in candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectoryAncestors(string path)
    {
        var current = new DirectoryInfo(path);

        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Adapter that bridges UserBot.Core.IBot to TournamentEngine.Core.Common.IBot
    /// </summary>
    private class UserBotCoreAdapter : IBot
    {
        private readonly dynamic _bot;

        public UserBotCoreAdapter(object bot)
        {
            _bot = bot;
        }

        public string TeamName => _bot.TeamName;
        public GameType GameType => (GameType)(int)_bot.GameType;

        public Task<string> MakeMove(GameState gameState, CancellationToken cancellationToken)
        {
            return _bot.MakeMove(gameState, cancellationToken);
        }

        public Task<int[]> AllocateTroops(GameState gameState, CancellationToken cancellationToken)
        {
            return _bot.AllocateTroops(gameState, cancellationToken);
        }

        public Task<string> MakePenaltyDecision(GameState gameState, CancellationToken cancellationToken)
        {
            return _bot.MakePenaltyDecision(gameState, cancellationToken);
        }

        public Task<string> MakeSecurityMove(GameState gameState, CancellationToken cancellationToken)
        {
            return _bot.MakeSecurityMove(gameState, cancellationToken);
        }
    }
}
