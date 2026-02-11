using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Core.BotLoader;

/// <summary>
/// Loads and compiles bot code from files using Roslyn compilation.
/// Supports single-file and multi-file bots.
/// </summary>
public class BotLoader : IBotLoader
{
    public Task<List<BotInfo>> LoadBotsFromDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("LoadBotsFromDirectoryAsync not yet implemented");
    }

    public async Task<BotInfo> LoadBotFromFolderAsync(string teamFolder, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        try
        {
            // Extract team name from folder path (e.g., "TeamRocket_v1" -> "TeamRocket")
            var folderName = Path.GetFileName(teamFolder);
            var teamName = folderName.Split('_')[0];

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

            // 3. Parse each file into a syntax tree
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in csFiles)
            {
                var code = await File.ReadAllTextAsync(filePath, cancellationToken);
                var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath, cancellationToken: cancellationToken);
                syntaxTrees.Add(syntaxTree);
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
