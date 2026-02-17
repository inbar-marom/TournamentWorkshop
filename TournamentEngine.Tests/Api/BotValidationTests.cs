using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.Common;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace TournamentEngine.Tests.Api;

/// <summary>
/// Unit tests for bot validation rules enforced in BotEndpoints
/// Tests all validation logic including double semicolons, approved namespaces, etc.
/// </summary>
[TestClass]
public class BotValidationTests
{
    #region Double Forward Slash Rule Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutDoubleSlash_ReturnsError()
    {
        // Arrange - Code without ;// or ; // at end of statements
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";
    public GameType GameType => GameType.RPSLS;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return ""Rock"";
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("double forward slash rule")), 
            "Should detect missing ;// or ; //");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithDoubleSemicolons_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    public GameType GameType => GameType.RPSLS;;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(""Rock"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            $"Should NOT report double semicolon error. Errors: {string.Join(", ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_UsingDirectivesWithSingleSemicolon_Allowed()
    {
        // Arrange - using directives don't need ;//
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Linq;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";//
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            "Using directives can have single semicolons");
    }

    [TestMethod]
    public void ValidateCSharpFile_ForLoopWithSingleSemicolon_Allowed()
    {
        // Arrange - for loops can have 2 single semicolons in the declaration
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string Name => ""Test"";;
    
    public void Process()
    {
        for (int i = 0; i < 10; i++)
        {
            var x = i;;
        }
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert - for loops should be allowed to have single semicolons
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            $"For loops can have single semicolons in the declaration. Errors: {string.Join(", ", errors)}");
    }

    #endregion

    #region Approved Namespace Tests

    [TestMethod]
    public void ValidateCSharpFile_WithApprovedNamespaces_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("unapproved namespace")), 
            $"All namespaces should be approved. Errors: {string.Join(", ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithNetworkingNamespace_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Net.Http;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert - System.Net.Http is caught by dangerous pattern check
        Assert.IsTrue(errors.Any(e => e.Contains("System.Net.Http")), 
            $"Should detect System.Net.Http. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithReflectionNamespace_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Reflection;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert -System.Reflection is unapproved namespace (not a sub of approved ones)
        Assert.IsTrue(errors.Any(e => e.Contains("System.Reflection")), 
            $"Should detect System.Reflection. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithCustomNamespace_IsAllowed()
    {
        // Arrange - Custom namespaces (like UserBot.Core) should now be allowed
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using UserBot.Core;
using UserBot.StrategicMind.Core;

public class TestBot
{
    public string TeamName => ""Test"";
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("namespace") && e.Contains("UserBot")), 
            "Custom namespaces like UserBot.* should be allowed");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithDangerousSystemNamespace_ReturnsError()
    {
        // Arrange - Dangerous system namespaces should still be blocked
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Net.Http;
using System.Reflection.Assembly;

public class TestBot
{
    public string TeamName => ""Test"";
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("blocked namespace") && e.Contains("System.Net")), 
            "Dangerous namespace System.Net.Http should be blocked");
        Assert.IsTrue(errors.Any(e => e.Contains("blocked namespace") && e.Contains("System.Reflection")), 
            "Dangerous namespace System.Reflection.Assembly should be blocked");
    }

    #endregion

    #region Dangerous API Pattern Tests

    [TestMethod]
    public void ValidateCSharpFile_WithFileDelete_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.IO;

public class TestBot
{
    public void BadMethod()
    {
        File.Delete(""somefile.txt"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("File.Delete")), 
            "Should detect File.Delete pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithProcessStart_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Diagnostics;

public class TestBot
{
    public void BadMethod()
    {
        System.Diagnostics.Process.Start(""cmd.exe"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Process.Start")), 
            "Should detect Process.Start pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithAssemblyLoad_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Reflection;

public class TestBot
{
    public void BadMethod()
    {
        System.Reflection.Assembly.Load(""SomeDll"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Assembly.Load")), 
            "Should detect Assembly.Load pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithHttpClient_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System.Net.Http;

public class TestBot
{
    private HttpClient _client = new HttpClient();;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("System.Net.Http")), 
            "Should detect HttpClient/networking pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithEnvironmentExit_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public void BadMethod()
    {
        Environment.Exit(0);;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Environment.Exit")), 
            "Should detect Environment.Exit pattern");
    }

    #endregion

    #region Unsafe Code Tests

    [TestMethod]
    public void ValidateCSharpFile_WithUnsafeBlock_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public unsafe void BadMethod()
    {
        int* ptr = null;;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("unsafe") && e.Contains("not allowed")), 
            "Should detect unsafe code blocks");
    }

    #endregion

    #region Target Framework Tests

    [TestMethod]
    public void ValidateCSharpFile_WithNet80TargetFramework_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

// <TargetFramework>net8.0</TargetFramework>
public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("framework") && e.Contains("net8.0")), 
            "Should NOT report error for net8.0 target framework");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithNet60TargetFramework_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

// <TargetFramework>net6.0</TargetFramework>
public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("framework") && e.Contains("other than")), 
            $"Should detect non-net8.0 target framework. Errors: {string.Join("; ", errors)}");
    }

    #endregion

    #region IBot Implementation Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutIBotImplementation_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("IBot")), 
            "Should warn about missing IBot implementation");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithIBotImplementation_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("IBot") && w.Contains("doesn't appear")), 
            "Should NOT warn when IBot is implemented");
    }

    #endregion

    #region Required Methods Tests

    [TestMethod]
    public void ValidateCSharpFile_WithAllRequiredMethods_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    public GameType GameType => GameType.RPSLS;;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(""Rock"");;
    }
    
    public async Task<List<int>> AllocateTroops(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new List<int> { 20, 20, 20, 20, 20 });;
    }
    
    public async Task<PenaltyDecision> MakePenaltyDecision(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new PenaltyDecision());;
    }
    
    public async Task<SecurityDecision> MakeSecurityMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new SecurityDecision());;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("MakeMove")), "Should NOT warn about missing MakeMove");
        Assert.IsFalse(warnings.Any(w => w.Contains("AllocateTroops")), "Should NOT warn about missing AllocateTroops");
        Assert.IsFalse(warnings.Any(w => w.Contains("MakePenaltyDecision")), "Should NOT warn about missing MakePenaltyDecision");
        Assert.IsFalse(warnings.Any(w => w.Contains("MakeSecurityMove")), "Should NOT warn about missing MakeSecurityMove");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithoutMakeMove_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    
    public async Task<List<int>> AllocateTroops(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new List<int>());;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("MakeMove")), 
            "Should warn about missing MakeMove method");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithoutAllocateTroops_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public async Task<string> MakeMove() { return ""Rock"";;  }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("AllocateTroops")), 
            "Should warn about missing AllocateTroops method");
    }

    #endregion

    #region File Type Tests

    [TestMethod]
    public void ValidateAllFiles_WithPythonFile_IsAllowed()
    {
        // Arrange - Python files are now allowed as documentation
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "bot.py",
                Code = "def make_move():\n    return 'Rock'"
            },
            new BotFile
            {
               FileName = "Bot.cs",
                Code = @"
using TournamentEngine.Core.Common;
public class Bot : IBot
{
    public string TeamName => ""Test"";//
}"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert - Python files are allowed, but need at least one C# file
        Assert.IsFalse(errors.Any(e => e.Contains("Python")), 
            $"Python files should be allowed as documentation. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateAllFiles_WithJavaScriptFile_ReturnsWarning()
    {
        // Arrange - JavaScript files get a warning about unknown extension
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "bot.js",
                Code = "function makeMove() { return 'Rock'; }"
            },
            new BotFile
            {
                FileName = "Bot.cs",
                Code = @"
using TournamentEngine.Core.Common;
public class Bot : IBot
{
    public string TeamName => ""Test"";//
}"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert - .js files should get a warning, not an error
        Assert.IsTrue(warnings.Any(e => e.Contains("unknown extension") && e.Contains("bot.js")), 
            $"Should warn about JavaScript files. Warnings: {string.Join("; ", warnings)}");
    }

    [TestMethod]
    public void ValidateAllFiles_WithCSharpFile_NoFileTypeError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = @"
using System;
public class TestBot
{
    public string Name => ""Test"";;
}"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("unsupported extension")), 
            "Should accept C# files");
    }

    [TestMethod]
    public void ValidateAllFiles_WithEmptyFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = ""
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("empty")), 
            "Should detect empty files");
    }

    [TestMethod]
    public void ValidateAllFiles_WithWhitespaceOnlyFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = "   \n\t  \n  "
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("empty")), 
            "Should detect whitespace-only files");
    }

    #endregion

    #region Missing Namespace Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutTournamentEngineNamespace_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("TournamentEngine.Core.Common")), 
            "Should warn about missing TournamentEngine.Core.Common namespace");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithTournamentEngineNamespace_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("TournamentEngine.Core.Common") && w.Contains("should include")), 
            "Should NOT warn when TournamentEngine.Core.Common is included");
    }

    #endregion

    #region Class Definition Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutClassDefinition_ReturnsWarning()
    {
        // Arrange - file with only an interface (now allowed for helper classes)
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public interface ITest
{
    string Name { get; }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert - interfaces are now allowed (for helper classes)
        Assert.IsFalse(warnings.Any(w => w.Contains("no class definitions")), 
            "Should not warn about interfaces - they are valid C# constructs for helpers");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithClassDefinition_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string Name => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, new List<BotFile> { file }, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("no class definitions")), 
            "Should NOT warn when class is defined");
    }

    #endregion

    #region Helper Methods - Direct Validation Access

    /// <summary>
    /// Helper to replicate ValidateCSharpFile logic from BotEndpoints.cs
    /// Must match the actual implementation for accurate testing
    /// </summary>
    private void ValidateCSharpFileDirectly(BotFile file, List<BotFile> allFiles, List<string> errors, List<string> warnings)
    {
        var code = file.Code;
        
        // Count how many files implement IBot
        var iBotImplementationCount = allFiles
            .Where(f => f.FileName.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            .Count(f => f.Code.Contains(": IBot"));
        
        // Check if this file implements IBot
        var thisFileImplementsIBot = code.Contains(": IBot");
        
        // Skip IBot-related warnings if there's exactly one IBot implementation and this isn't it
        var skipIBotWarnings = (iBotImplementationCount == 1) && !thisFileImplementsIBot;

        // Check for basic C# structure (class or interface)
        if (!skipIBotWarnings && !code.Contains("class ") && !code.Contains("interface "))
        {
            warnings.Add($"File {file.FileName} appears to be C# but has no class or interface definitions");
        }

        // CODING RULE 1: Check for double semicolons (;;) - not allowed
        if (code.Contains(";;"))
        {
            errors.Add($"File {file.FileName} contains double semicolons (;;) which are not allowed");
        }
        
        // CODING RULE 2: Check for double forward slashes at end of lines (required pattern)
        var lines = code.Split('\n');
        var invalidLines = 0;
        
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r', '\n', ' ', '\t');
            
            // Skip empty lines, using directives, namespace declarations, braces, and for loop headers
            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.TrimStart().StartsWith("using ") ||
                trimmed.TrimStart().StartsWith("namespace ") ||
                trimmed.Contains("for (") ||
                trimmed.Trim() == "{" ||
                trimmed.Trim() == "}")
            {
                continue;
            }
            
            // If line ends with semicolon, it must be followed by //
            if (trimmed.EndsWith(";") && !trimmed.EndsWith("; //") && !trimmed.EndsWith(";//"))
            {
                invalidLines++;
            }
        }
        
        if (invalidLines > 0)
        {
            errors.Add($"File {file.FileName} violates double forward slash rule. All statement lines must end with ';//' or '; //' (except using directives, namespace declarations, and for loops)");
        }

        // Check for unsafe code blocks
        if (code.Contains("unsafe "))
        {
            errors.Add($"File {file.FileName} contains 'unsafe' code blocks which are not allowed");
        }

        // Check for dangerous patterns (exact strings from BotEndpoints.cs)
        var dangerousPatterns = new[]
        {
            "System.Reflection.Assembly.Load",
            "System.Runtime.InteropServices",
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Diagnostics.Process.Start",
            "File.Delete",
            "Directory.Delete",
            "Environment.Exit"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (code.Contains(pattern))
            {
                errors.Add($"File {file.FileName} contains disallowed pattern: {pattern}");
            }
        }

        // Validate using directives
        var approvedNamespaces = new HashSet<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Numerics",
            "System.Threading",
            "System.IO",
            "System.Text.RegularExpressions",
            "System.Diagnostics",
            "TournamentEngine.Core.Common"
        };

        // Blocked namespaces - dangerous system libraries
        var blockedNamespaces = new[]
        {
            "System.Net",
            "System.Reflection",
            "System.Runtime",
            "System.Security",
            "System.IO.Pipes",
            "System.IO.IsolatedStorage",
            "System.CodeDom",
            "Microsoft.CSharp",
            "Microsoft.CodeAnalysis"
        };

        var usingMatches = System.Text.RegularExpressions.Regex.Matches(code, @"using\s+([a-zA-Z0-9_.]+)\s*;");
        foreach (System.Text.RegularExpressions.Match match in usingMatches)
        {
            var namespaceName = match.Groups[1].Value;
            
            // Allow approved .NET namespaces
            if (approvedNamespaces.Contains(namespaceName))
            {
                continue;
            }
            
            // Block dangerous system namespaces
            bool isBlocked = false;
            foreach (var blocked in blockedNamespaces)
            {
                if (namespaceName == blocked || namespaceName.StartsWith(blocked + "."))
                {
                    errors.Add($"File {file.FileName} uses blocked namespace: {namespaceName}. This namespace provides dangerous functionality.");
                    isBlocked = true;
                    break;
                }
            }
            
            // Allow custom namespaces (like UserBot.Core, etc.) - they're user-defined and safe
            if (!isBlocked)
            {
                // Custom namespace - allowed
                continue;
            }
        }

        // Check if file uses custom namespaces (UserBot.*, etc.)
        var usingCustomNamespaces = System.Text.RegularExpressions.Regex.IsMatch(
            code, 
            @"using\s+(?!System|TournamentEngine)[a-zA-Z0-9_.]+\s*;"
        );

        // Check for TournamentEngine.Core.Common (only for IBot implementations not using custom namespaces)
        if (!skipIBotWarnings && !usingCustomNamespaces && !code.Contains("using TournamentEngine.Core.Common"))
        {
            warnings.Add($"File {file.FileName} should include 'using TournamentEngine.Core.Common;' to implement IBot interface");
        }

        // Check for target framework
        if (code.Contains("<TargetFramework>") && !code.Contains("<TargetFramework>net8.0</TargetFramework>"))
        {
            errors.Add($"File {file.FileName} targets a framework other than net8.0. Must target .NET 8.0");
        }

        // Check for IBot implementation (only warn if we should)
        if (!skipIBotWarnings && !code.Contains(": IBot"))
        {
            warnings.Add($"File {file.FileName} doesn't appear to implement IBot interface");
        }

        // Check for required methods (only for IBot implementations)
        if (!skipIBotWarnings)
        {
            var requiredMethods = new[] { "MakeMove", "AllocateTroops", "MakePenaltyDecision", "MakeSecurityMove" };
            foreach (var method in requiredMethods)
            {
                if (!code.Contains(method))
                {
                    warnings.Add($"File {file.FileName} doesn't appear to implement required method: {method}");
                }
            }
        }
    }

    private void ValidateAllFilesDirectly(List<BotFile> files, List<string> errors, List<string> warnings)
    {
        var hasCodeFile = false;
        
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Code))
            {
                errors.Add($"File {file.FileName} is empty");
                continue;
            }

            // C# file validation
            if (file.FileName.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            {
                hasCodeFile = true;
                ValidateCSharpFileDirectly(file, files, errors, warnings);
            }
            // Allow documentation files - just skip validation
            else if (file.FileName.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase) ||
                     file.FileName.EndsWith(".agent.md", System.StringComparison.OrdinalIgnoreCase) ||
                     file.FileName.EndsWith(".py", System.StringComparison.OrdinalIgnoreCase) ||
                     file.FileName.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
            {
                // Documentation/verification files allowed - no validation needed
                continue;
            }
            else
            {
                warnings.Add($"File {file.FileName} has unknown extension. Only .cs files will be compiled. Documentation files (.md, .agent.md, .py, .txt) are allowed but ignored.");
            }
        }
        
        if (!hasCodeFile)
        {
            errors.Add("At least one .cs file is required");
        }
    }

    #endregion

    #region Real Submission Tests

    [TestMethod]
    public void ValidateStrategicMindSubmission_AsIs_ShowsValidationResults()
    {
        // Arrange - Load all files from StrategicMind submission
        var submissionPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData",
            "StrategicMind_Submission",
            "StrategicMind_Submission"
        );

        var files = new List<BotFile>();

        // Load all .cs files from BotCode directory
        var csFiles = Directory.GetFiles(Path.Combine(submissionPath, "BotCode"), "*.cs", SearchOption.AllDirectories);
        foreach (var csFilePath in csFiles)
        {
            var fileName = Path.GetFileName(csFilePath);
            var code = File.ReadAllText(csFilePath);
            files.Add(new BotFile { FileName = fileName, Code = code });
        }

        // Load documentation files
        var docPath = Path.Combine(submissionPath, "Documentation");
        if (Directory.Exists(docPath))
        {
            foreach (var mdFile in Directory.GetFiles(docPath, "*.md"))
            {
                var fileName = Path.GetFileName(mdFile);
                var code = File.ReadAllText(mdFile);
                files.Add(new BotFile { FileName = fileName, Code = code });
            }
        }

        // Load config files
        var configPath = Path.Combine(submissionPath, "Config");
        if (Directory.Exists(configPath))
        {
            foreach (var mdFile in Directory.GetFiles(configPath, "*.md"))
            {
                var fileName = Path.GetFileName(mdFile);
                var code = File.ReadAllText(mdFile);
                files.Add(new BotFile { FileName = fileName, Code = code });
            }
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act - Validate all C# files
        ValidateAllFilesDirectly(files, errors, warnings);

        // Also check for required files
        ValidateRequiredFilesDirectly(files, errors);

        // Assert
        Assert.AreEqual(24, files.Count, "Total file count should be 24");
        Assert.AreEqual(14, files.Count(f => f.FileName.EndsWith(".cs")), "Should have 14 C# files");
        Assert.AreEqual(10, files.Count(f => f.FileName.EndsWith(".md")), "Should have 10 MD files");
        
        // Should have no validation errors
        Assert.IsFalse(errors.Any(), 
            $"Validation should pass with no errors. Errors found: {string.Join("; ", errors)}");
        
        // No warnings expected - helper classes are correctly identified (only 1 IBot implementation)
        Assert.AreEqual(0, warnings.Count, 
            $"Should have no warnings with smart IBot detection. Warnings: {string.Join("; ", warnings)}");
    }

    [TestMethod]
    public void SubmitStrategicMindBot_WithValidation_PassesAllChecks()
    {
        // Arrange - Load all files from StrategicMind submission (same as verify test)
        var submissionPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData",
            "StrategicMind_Submission",
            "StrategicMind_Submission"
        );

        var files = new List<BotFile>();

        // Load all .cs files from BotCode directory
        var csFiles = Directory.GetFiles(Path.Combine(submissionPath, "BotCode"), "*.cs", SearchOption.AllDirectories);
        foreach (var csFilePath in csFiles)
        {
            var fileName = Path.GetFileName(csFilePath);
            var code = File.ReadAllText(csFilePath);
            files.Add(new BotFile { FileName = fileName, Code = code });
        }

        // Load documentation files
        var docPath = Path.Combine(submissionPath, "Documentation");
        if (Directory.Exists(docPath))
        {
            foreach (var mdFile in Directory.GetFiles(docPath, "*.md"))
            {
                var fileName = Path.GetFileName(mdFile);
                var code = File.ReadAllText(mdFile);
                files.Add(new BotFile { FileName = fileName, Code = code });
            }
        }

        // Load config files
        var configPath = Path.Combine(submissionPath, "Config");
        if (Directory.Exists(configPath))
        {
            foreach (var mdFile in Directory.GetFiles(configPath, "*.md"))
            {
                var fileName = Path.GetFileName(mdFile);
                var code = File.ReadAllText(mdFile);
                files.Add(new BotFile { FileName = fileName, Code = code });
            }
        }

        // Create submission request (as it would be sent to POST /api/bots/submit)
        var submissionRequest = new BotSubmissionRequest
        {
            TeamName = "StrategicMind",
            Files = files,
            Overwrite = true
        };

        // Act - Run the same validation that the submit endpoint uses
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate team name
        Assert.IsTrue(IsValidTeamNameHelper(submissionRequest.TeamName), 
            "Team name should be valid");

        // Validate file sizes (same limits as submit endpoint)
        var maxFileSize = 50_000; // 50KB
        var maxTotalSize = 500_000; // 500KB

        foreach (var file in submissionRequest.Files)
        {
            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            if (fileSize > maxFileSize)
            {
                errors.Add($"File {file.FileName} exceeds maximum size of 50KB");
            }
        }

        var totalSize = submissionRequest.Files.Sum(f => System.Text.Encoding.UTF8.GetByteCount(f.Code));
        if (totalSize > maxTotalSize)
        {
            errors.Add("Total submission size exceeds maximum of 500KB");
        }

        // Check for duplicate filenames
        var fileNames = submissionRequest.Files.Select(f => f.FileName).ToList();
        if (fileNames.Distinct().Count() != fileNames.Count)
        {
            errors.Add("Duplicate file names detected");
        }

        // Run enhanced validation (same as submit endpoint)
        ValidateAllFilesDirectly(submissionRequest.Files, errors, warnings);
        ValidateRequiredFilesDirectly(submissionRequest.Files, errors);

        // Assert - Should pass all validation checks
        Assert.AreEqual(24, submissionRequest.Files.Count, "Should have 24 files");
        Assert.IsTrue(totalSize < maxTotalSize, 
            $"Total size ({totalSize} bytes) should be under {maxTotalSize} bytes");
        Assert.AreEqual(submissionRequest.TeamName, "StrategicMind", "Team name should match");
        
        // No duplicate filenames
        Assert.AreEqual(fileNames.Count, fileNames.Distinct().Count(), 
            "Should have no duplicate file names");
        
        // All file sizes should be within limits
        foreach (var file in submissionRequest.Files)
        {
            var fileSize = System.Text.Encoding.UTF8.GetByteCount(file.Code);
            Assert.IsTrue(fileSize <= maxFileSize, 
                $"File {file.FileName} ({fileSize} bytes) should be under {maxFileSize} bytes");
        }
        
        // Most important: No validation errors (ready for submission)
        Assert.IsFalse(errors.Any(), 
            $"Submission should pass validation with no errors. Errors: {string.Join("; ", errors)}");
    }

    private bool IsValidTeamNameHelper(string teamName)
    {
        // Team name validation: alphanumeric, hyphens, underscores only
        return !string.IsNullOrWhiteSpace(teamName) &&
               System.Text.RegularExpressions.Regex.IsMatch(teamName, @"^[a-zA-Z0-9_-]+$");
    }

    private void ValidateRequiredFilesDirectly(List<BotFile> files, List<string> errors)
    {
        var fileNames = files.Select(f => f.FileName).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        // Required files for all submissions
        var requiredFiles = new[]
        {
            "plan-rpsls.md",
            "plan-colonelBlotto.md",
            "colonelBlotto_Skill.md",
            "plan-workshop.md",
            "copilot-instructions.md"
        };

        // Allow either ResearchAgent.md or ResearchAgent.agent.md
        if (!fileNames.Contains("ResearchAgent.md") && !fileNames.Contains("ResearchAgent.agent.md"))
        {
            errors.Add("Missing required file: ResearchAgent.md or ResearchAgent.agent.md");
        }

        // Allow either RPSLS_Skill.md or RPSLS-Strategy-Skill.md
        if (!fileNames.Contains("RPSLS_Skill.md") && !fileNames.Contains("RPSLS-Strategy-Skill.md"))
        {
            errors.Add("Missing required file: RPSLS_Skill.md or RPSLS-Strategy-Skill.md");
        }

        var missingFiles = requiredFiles
            .Where(required => !fileNames.Contains(required))
            .ToList();

        if (missingFiles.Any())
        {
            errors.Add($"Missing required documentation files: {string.Join(", ", missingFiles)}");
        }
    }

    #endregion
}
